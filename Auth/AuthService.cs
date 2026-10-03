using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Auth;

public enum LoginFailure
{
    None,
    InvalidCredentials,
    LockedOut,
    Inactive,
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
    ILogger<AuthService> logger)
{
    public async Task<LoginResult> LoginAsync(string? email, string? password, CancellationToken ct = default)
    {
        var settings = options.Value;
        var normalizedEmail = EmailAddresses.Normalize(email);

        // Never run PBKDF2 on absurd input, but still take about as long as a real attempt.
        if (normalizedEmail is null || string.IsNullOrEmpty(password) || password.Length > PasswordPolicy.MaxLength)
        {
            hasher.BurnTime(string.Empty);
            return LoginResult.Fail(LoginFailure.InvalidCredentials);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        // The name lives in the profile table of the user's role, so load those too (only one of them has a row).
        var user = await db.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .Include(u => u.Administrator)
            .Include(u => u.GuidanceCounselor)
            .Include(u => u.Registrar)
            .Include(u => u.Teacher)
            .Include(u => u.Student)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

        if (user is null)
        {
            hasher.BurnTime(password);
            return LoginResult.Fail(LoginFailure.InvalidCredentials);
        }

        var now = clock.GetUtcNow().UtcDateTime;

        if (user.LockoutEndUtc is { } lockoutEnd && lockoutEnd > now)
        {
            hasher.BurnTime(password);
            logger.LogWarning("Sign-in refused for user {UserId}: account is locked until {LockoutEnd:u}.", user.Id, lockoutEnd);
            return LoginResult.Fail(LoginFailure.LockedOut);
        }

        if (!hasher.Verify(password, user.PasswordHash))
        {
            await RegisterFailedAttemptAsync(db, user.Id, settings, now, ct);
            return LoginResult.Fail(LoginFailure.InvalidCredentials);
        }

        if (!user.IsActive)
        {
            logger.LogWarning("Sign-in refused for user {UserId}: account is inactive.", user.Id);
            return LoginResult.Fail(LoginFailure.Inactive);
        }

        var newHash = hasher.NeedsRehash(user.PasswordHash) ? hasher.Hash(password) : null;

        await db.Users
            .Where(u => u.Id == user.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.FailedLoginCount, 0)
                .SetProperty(u => u.LockoutEndUtc, (DateTime?)null)
                .SetProperty(u => u.LastLoginAtUtc, now)
                .SetProperty(u => u.PasswordHash, u => newHash ?? u.PasswordHash), ct);

        return new LoginResult(true, CreatePrincipal(user), LoginFailure.None);
    }

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

    private async Task RegisterFailedAttemptAsync(AppDbContext db, int userId, AuthOptions settings, DateTime now, CancellationToken ct)
    {
        // Counted in the database so several simultaneous guesses can't overwrite each other's count.
        await db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.FailedLoginCount, u => u.FailedLoginCount + 1), ct);

        var lockoutEnd = now.AddMinutes(settings.LockoutMinutes);
        var locked = await db.Users
            .Where(u => u.Id == userId && u.FailedLoginCount >= settings.MaxFailedAttempts)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.FailedLoginCount, 0)
                .SetProperty(u => u.LockoutEndUtc, (DateTime?)lockoutEnd), ct);

        if (locked > 0)
        {
            logger.LogWarning("User {UserId} locked until {LockoutEnd:u} after {Attempts} failed sign-ins.", userId, lockoutEnd, settings.MaxFailedAttempts);
        }
    }
}
