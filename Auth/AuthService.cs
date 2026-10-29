using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;
using SEAITT_LMS.Services;

namespace SEAITT_LMS.Auth;

public enum LoginFailure
{
    None,
    /// <summary>The email is fine but the password is wrong (or what was typed can not be an email and a password).</summary>
    InvalidCredentials,

    /// <summary>No account uses this email (the login page says "User Not Found").</summary>
    UserNotFound,

    LockedOut,
    Inactive,

    /// <summary>The right password, but the system is closed for maintenance and the account is not an administrator's.</summary>
    Maintenance,

    /// <summary>The right password, but somebody is signed in with the account on another browser or device (one sign-in at a time).</summary>
    AlreadySignedIn,
}

/// <param name="Principal">The identity to put in the sign-in cookie, only set when the login succeeded.</param>
/// <param name="LockedUntilUtc">When the account is locked (the sign-in was refused because of it, or the wrong try that just locked it): the moment the lock ends.</param>
public sealed record LoginResult(bool Succeeded, ClaimsPrincipal? Principal, LoginFailure Failure, DateTime? LockedUntilUtc = null)
{
    public static LoginResult Fail(LoginFailure failure) => new(false, null, failure);

    public static LoginResult Locked(DateTime until) => new(false, null, LoginFailure.LockedOut, until);
}

/// <summary>
/// Checks an email and password against the users table. Writing the cookie is the caller's job
/// (a Blazor component can't set cookies from inside a live circuit, the login page does it while
/// handling the form post).
/// </summary>
public sealed class AuthService(
    IDbContextFactory<AppDbContext> dbFactory,
    PasswordHasher hasher,
    IOptions<AuthOptions> options,
    TimeProvider clock,
    ILogger<AuthService> logger,
    SystemSettingsService? systemSettings = null,
    SecurityLogService? securityLog = null)
{
    /// <param name="client">Where the sign-in came from (for the security log). Empty when it is not known.</param>
    public async Task<LoginResult> LoginAsync(string? email, string? password, ClientInfo? client = null, CancellationToken ct = default)
    {
        // The limits are the ones of System Settings; without them (a test) the ones of appsettings.json.
        var settings = options.Value;
        var maxFailed = systemSettings?.Current.MaxFailedAttempts ?? settings.MaxFailedAttempts;
        var lockoutMinutes = systemSettings?.Current.LockoutMinutes ?? settings.LockoutMinutes;
        var normalizedEmail = EmailAddresses.Normalize(email);

        // Never run PBKDF2 on absurd input, but still take about as long as a real attempt.
        if (normalizedEmail is null || string.IsNullOrEmpty(password) || password.Length > PasswordPolicy.MaxLength)
        {
            hasher.BurnTime(string.Empty);
            await LogAsync(SecurityEvents.SignInFailed, null, (email ?? "").Trim(), "Not a valid email and password.", client, ct);
            return LoginResult.Fail(LoginFailure.InvalidCredentials);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        // The name lives in the profile table of the user's role, so load those too (only one of them has a row).
        var user = await db.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .Include(u => u.Administrator)
            .Include(u => u.GuidanceCounselor)
            .Include(u => u.AcademicCoordinator)
            .Include(u => u.AcademicProvider)
            .Include(u => u.Registrar)
            .Include(u => u.Teacher)
            .Include(u => u.Student)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

        var now = clock.GetUtcNow().UtcDateTime;

        if (user is null)
        {
            hasher.BurnTime(password);
            await LogAsync(SecurityEvents.SignInFailed, null, normalizedEmail, "There is no account with this email.", client, ct);
            return LoginResult.Fail(LoginFailure.UserNotFound);
        }

        if (user.LockoutEndUtc is { } lockoutEnd && lockoutEnd > now)
        {
            hasher.BurnTime(password);
            logger.LogWarning("Sign-in refused for user {UserId}: account is locked until {LockoutEnd:u}.", user.Id, lockoutEnd);
            await LogAsync(SecurityEvents.SignInRefused, user.Id, user.Email, "The account is locked.", client, ct);
            return LoginResult.Locked(lockoutEnd);
        }

        if (!hasher.Verify(password, user.PasswordHash))
        {
            var lockedUntil = await RegisterFailedAttemptAsync(db, user.Id, maxFailed, lockoutMinutes, now, ct);
            await LogAsync(SecurityEvents.SignInFailed, user.Id, user.Email, "Wrong password.", client, ct);
            if (lockedUntil is { } until)
            {
                await LogAsync(SecurityEvents.AccountLocked, user.Id, user.Email, $"Locked for {lockoutMinutes} minutes after {maxFailed} wrong passwords.", client, ct);
                return LoginResult.Locked(until);
            }

            return LoginResult.Fail(LoginFailure.InvalidCredentials);
        }

        if (!user.IsActive)
        {
            logger.LogWarning("Sign-in refused for user {UserId}: account is inactive.", user.Id);
            await LogAsync(SecurityEvents.SignInRefused, user.Id, user.Email, "The account is deactivated.", client, ct);
            return LoginResult.Fail(LoginFailure.Inactive);
        }

        // While the system is closed for maintenance only an administrator gets in.
        if (systemSettings is { IsClosed: true } && user.RoleId != AppRoles.AdminId)
        {
            await LogAsync(SecurityEvents.SignInRefused, user.Id, user.Email, "The system is closed for maintenance.", client, ct);
            return LoginResult.Fail(LoginFailure.Maintenance);
        }

        var newHash = hasher.NeedsRehash(user.PasswordHash) ? hasher.Hash(password) : null;

        // One sign-in at a time: the update below only happens when nobody else is using the account (no sign-in is open, or it was not seen for a few minutes). It is one statement
        // in the database, so two people signing in at the same moment can't both get in.
        var single = settings.SingleSession;
        var sessionId = single ? Guid.NewGuid().ToString("N") : null;
        var seenAfter = now.AddMinutes(-Math.Max(1, settings.SessionIdleMinutes));
        var updated = await db.Users
            .Where(u => u.Id == user.Id && (!single || u.ActiveSessionId == null || u.ActiveSessionSeenUtc == null || u.ActiveSessionSeenUtc < seenAfter))
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.FailedLoginCount, 0)
                .SetProperty(u => u.LockoutEndUtc, (DateTime?)null)
                .SetProperty(u => u.LastLoginAtUtc, now)
                .SetProperty(u => u.ActiveSessionId, sessionId)
                .SetProperty(u => u.ActiveSessionSeenUtc, single ? now : (DateTime?)null)
                .SetProperty(u => u.PasswordHash, u => newHash ?? u.PasswordHash), ct);
        if (updated == 0)
        {
            logger.LogWarning("Sign-in refused for user {UserId}: the account is signed in somewhere else.", user.Id);
            await LogAsync(SecurityEvents.SignInRefused, user.Id, user.Email, "The account is signed in on another browser or device.", client, ct);
            return LoginResult.Fail(LoginFailure.AlreadySignedIn);
        }

        // (A sign-in is not written in the security log: the account keeps the day of its last login, and the Security Logs page lists them.)
        return new LoginResult(true, CreatePrincipal(user, sessionId), LoginFailure.None);
    }

    /// <summary>
    /// A signed-in person changes their own password (the Settings box). The current password must be right: a wrong one is counted like a wrong sign-in
    /// (the same limit and lock). A new security stamp ends every session of the account (this one is ended by /account/password-changed), so the person logs in again with the new password.
    /// </summary>
    public async Task<ServiceResult> ChangePasswordAsync(int userId, string? currentPassword, string? newPassword, string? confirmPassword, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(currentPassword))
        {
            return ServiceResult.Fail("Enter your current password.");
        }

        if (PasswordPolicy.Validate(newPassword) is { } problem)
        {
            return ServiceResult.Fail(problem.Replace("Password", "The new password", StringComparison.Ordinal));
        }

        if (newPassword != confirmPassword)
        {
            return ServiceResult.Fail("The two new passwords are not the same.");
        }

        if (newPassword == currentPassword)
        {
            return ServiceResult.Fail("The new password must be different from the current one.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, ct);
        if (user is null)
        {
            return ServiceResult.Fail("Your account was not found.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        if (user.LockoutEndUtc is { } lockoutEnd && lockoutEnd > now)
        {
            return ServiceResult.Fail($"Too many wrong passwords. Try again in {Math.Max(1, (int)Math.Ceiling((lockoutEnd - now).TotalMinutes))} minute(s).");
        }

        if (!hasher.Verify(currentPassword, user.PasswordHash))
        {
            var maxFailed = systemSettings?.Current.MaxFailedAttempts ?? options.Value.MaxFailedAttempts;
            var lockoutMinutes = systemSettings?.Current.LockoutMinutes ?? options.Value.LockoutMinutes;
            if (await RegisterFailedAttemptAsync(db, user.Id, maxFailed, lockoutMinutes, now, ct) is not null)
            {
                await LogAsync(SecurityEvents.AccountLocked, user.Id, user.Email, $"Locked for {lockoutMinutes} minutes after {maxFailed} wrong passwords (in Settings).", null, ct);
                return ServiceResult.Fail($"Too many wrong passwords. The account is locked for {lockoutMinutes} minutes.");
            }

            return ServiceResult.Fail("The current password is not right.");
        }

        user.PasswordHash = hasher.Hash(newPassword!);
        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.ActiveSessionId = null;   // every session ends, so the person can sign in again at once with the new password
        user.ActiveSessionSeenUtc = null;
        await db.SaveChangesAsync(ct);

        await LogAsync(SecurityEvents.PasswordChanged, user.Id, user.Email, "The person changed their own password.", null, ct);
        return ServiceResult.Ok("Your password was changed.");
    }

    private Task LogAsync(string kind, int? userId, string? email, string? details, ClientInfo? client, CancellationToken ct) =>
        securityLog is null ? Task.CompletedTask : securityLog.AddAsync(kind, userId, email, details, client, ct: ct);

    /// <summary>
    /// Says that the sign-in is still in use (a page of it is open): the account stays taken. False when this sign-in is not the open one any more (the account was signed in somewhere else
    /// after this one was not seen for a while, or its password changed): the page signs out. A cookie made before sessions were kept (no number) is always fine.
    /// </summary>
    public async Task<bool> TouchSessionAsync(int userId, string? sessionId, CancellationToken ct = default)
    {
        if (!options.Value.SingleSession || string.IsNullOrEmpty(sessionId))
        {
            return true;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var touched = await db.Users.Where(u => u.Id == userId && u.ActiveSessionId == sessionId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.ActiveSessionSeenUtc, now), ct);
        return touched > 0;
    }

    /// <summary>The person pressed Log out: the account is free for the next sign-in.</summary>
    public async Task EndSessionAsync(int userId, string? sessionId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            return;
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.Users.Where(u => u.Id == userId && u.ActiveSessionId == sessionId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.ActiveSessionId, (string?)null).SetProperty(u => u.ActiveSessionSeenUtc, (DateTime?)null), ct);
    }

    public static ClaimsPrincipal CreatePrincipal(User user, string? sessionId = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.Name),
            new(AppClaimTypes.SecurityStamp, user.SecurityStamp),
        };
        if (sessionId is not null)
        {
            claims.Add(new Claim(AppClaimTypes.SessionId, sessionId));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme, ClaimTypes.Name, ClaimTypes.Role);
        return new ClaimsPrincipal(identity);
    }

    /// <summary>Counts a wrong password. Gives back the moment the lock ends when this one locked the account, otherwise null.</summary>
    private async Task<DateTime?> RegisterFailedAttemptAsync(AppDbContext db, int userId, int maxFailedAttempts, int lockoutMinutes, DateTime now, CancellationToken ct)
    {
        // Counted in the database so several simultaneous guesses can't overwrite each other's count.
        await db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.FailedLoginCount, u => u.FailedLoginCount + 1), ct);

        var lockoutEnd = now.AddMinutes(lockoutMinutes);
        var locked = await db.Users
            .Where(u => u.Id == userId && u.FailedLoginCount >= maxFailedAttempts)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.FailedLoginCount, 0)
                .SetProperty(u => u.LockoutEndUtc, (DateTime?)lockoutEnd), ct);

        if (locked > 0)
        {
            logger.LogWarning("User {UserId} locked until {LockoutEnd:u} after {Attempts} failed sign-ins.", userId, lockoutEnd, maxFailedAttempts);
        }

        return locked > 0 ? lockoutEnd : null;
    }
}
