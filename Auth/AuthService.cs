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
    InvalidCredentials,
    LockedOut,
    Inactive,

    /// <summary>The right password, but the system is closed for maintenance and the account is not an administrator's.</summary>
    Maintenance,
}

/// <param name="Principal">The identity to put in the sign-in cookie, only set when the login succeeded.</param>
public sealed record LoginResult(bool Succeeded, ClaimsPrincipal? Principal, LoginFailure Failure)
{
    public static LoginResult Fail(LoginFailure failure) => new(false, null, failure);
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

        if (user is null)
        {
            hasher.BurnTime(password);
            await LogAsync(SecurityEvents.SignInFailed, null, normalizedEmail, "There is no account with this email.", client, ct);
            return LoginResult.Fail(LoginFailure.InvalidCredentials);
        }

        var now = clock.GetUtcNow().UtcDateTime;

        if (user.LockoutEndUtc is { } lockoutEnd && lockoutEnd > now)
        {
            hasher.BurnTime(password);
            logger.LogWarning("Sign-in refused for user {UserId}: account is locked until {LockoutEnd:u}.", user.Id, lockoutEnd);
            await LogAsync(SecurityEvents.SignInRefused, user.Id, user.Email, "The account is locked.", client, ct);
            return LoginResult.Fail(LoginFailure.LockedOut);
        }

        if (!hasher.Verify(password, user.PasswordHash))
        {
            var lockedNow = await RegisterFailedAttemptAsync(db, user.Id, maxFailed, lockoutMinutes, now, ct);
            await LogAsync(SecurityEvents.SignInFailed, user.Id, user.Email, "Wrong password.", client, ct);
            if (lockedNow)
            {
                await LogAsync(SecurityEvents.AccountLocked, user.Id, user.Email, $"Locked for {lockoutMinutes} minutes after {maxFailed} wrong passwords.", client, ct);
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

        await db.Users
            .Where(u => u.Id == user.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.FailedLoginCount, 0)
                .SetProperty(u => u.LockoutEndUtc, (DateTime?)null)
                .SetProperty(u => u.LastLoginAtUtc, now)
                .SetProperty(u => u.PasswordHash, u => newHash ?? u.PasswordHash), ct);

        await LogAsync(SecurityEvents.SignInSucceeded, user.Id, user.Email, null, client, ct);
        return new LoginResult(true, CreatePrincipal(user), LoginFailure.None);
    }

    private Task LogAsync(string kind, int? userId, string? email, string? details, ClientInfo? client, CancellationToken ct) =>
        securityLog is null ? Task.CompletedTask : securityLog.AddAsync(kind, userId, email, details, client, ct: ct);

    public static ClaimsPrincipal CreatePrincipal(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.Name),
            new(AppClaimTypes.SecurityStamp, user.SecurityStamp),
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme, ClaimTypes.Name, ClaimTypes.Role);
        return new ClaimsPrincipal(identity);
    }

    /// <summary>Counts a wrong password. Gives back true when this one locked the account.</summary>
    private async Task<bool> RegisterFailedAttemptAsync(AppDbContext db, int userId, int maxFailedAttempts, int lockoutMinutes, DateTime now, CancellationToken ct)
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

        return locked > 0;
    }
}
