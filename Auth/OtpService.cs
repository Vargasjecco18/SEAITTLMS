using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;
using SEAITT_LMS.Services;

namespace SEAITT_LMS.Auth;

/// <summary>A code was sent: what the page needs to wait for it.</summary>
/// <param name="Token">The random token the browser keeps (in a cookie) until the code is typed. Only its hash is in the database.</param>
/// <param name="SentTo">The number written to, with the middle hidden (+63977***6918).</param>
public sealed record OtpChallenge(string Token, string SentTo, DateTime ExpiresAtUtc, DateTime ResendAtUtc, int AttemptsLeft);

public enum OtpFailure
{
    None,

    /// <summary>The code is not there any more (it was used, cancelled, or the browser lost it): log in again with the password.</summary>
    NotFound,

    /// <summary>The code lived too long: ask for a new one.</summary>
    Expired,

    WrongCode,

    /// <summary>Too many wrong codes: the code was cancelled.</summary>
    TooManyTries,

    /// <summary>A new code was asked for too soon, or too many times.</summary>
    Wait,

    /// <summary>UniSMS did not send the message.</summary>
    CouldNotSend,
}

public sealed record OtpResult(bool Succeeded, OtpFailure Failure = OtpFailure.None, string? Message = null, OtpChallenge? Challenge = null, int UserId = 0, int AttemptsLeft = 0)
{
    public static OtpResult Fail(OtpFailure failure, string message, int attemptsLeft = 0) => new(false, failure, message, null, 0, attemptsLeft);
}

/// <summary>Is a code asked for this person when they sign in?</summary>
public enum OtpNeed
{
    /// <summary>No: the setting is off, or the account is not an administrator's.</summary>
    No,

    /// <summary>Yes: send the code to <c>Phone</c>.</summary>
    Yes,

    /// <summary>The setting is on but the key or the number of the administrator is missing: the person signs in with the password only and the security log says so (a missing setting can't lock the only administrator out).</summary>
    SkippedNotSetUp,
}

/// <summary>
/// The one-time code of the login (round 42). It is asked from an administrator who typed the right password, when OTP is on in System Settings: a code of a few digits goes to the phone number of
/// the account through UniSMS, and the sign-in is finished when the code is typed. A code lives <see cref="OtpOptions.ExpiryMinutes"/> minutes, takes <see cref="OtpOptions.MaxAttempts"/> wrong tries
/// and can be sent again after <see cref="OtpOptions.ResendSeconds"/> seconds (<see cref="OtpOptions.MaxSends"/> codes for one sign-in at most). The code is made here and never kept: only a hash is.
/// </summary>
public sealed class OtpService(
    IDbContextFactory<AppDbContext> dbFactory,
    UniSmsClient sms,
    IOptions<OtpOptions> options,
    TimeProvider clock,
    SystemSettingsService systemSettings,
    SecurityLogService? securityLog = null)
{
    /// <summary>A sign-in that waits for its code is forgotten after this long, whatever the code says (the person types the password again).</summary>
    public const int ChallengeMinutes = 15;

    public bool IsSetUp => sms.IsConfigured;

    /// <summary>Does this account need a code now? Only an administrator, only when OTP is on, and only when UniSMS and the number of the account are set up.</summary>
    public (OtpNeed Need, string? Phone) Decide(User user)
    {
        if (user.RoleId != AppRoles.AdminId || !systemSettings.Current.OtpEnabled)
        {
            return (OtpNeed.No, null);
        }

        var phone = PhoneNumbers.Normalize(user.PhoneNumber);
        return sms.IsConfigured && phone is not null ? (OtpNeed.Yes, phone) : (OtpNeed.SkippedNotSetUp, null);
    }

    /// <summary>Makes a code for the account, writes it to the phone and keeps its hash. When the message can't be sent nothing is kept.</summary>
    public async Task<OtpResult> StartAsync(User user, string phone, ClientInfo? client = null, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var o = options.Value;
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        // Old codes of this account and the ones that are long over are not needed.
        await db.LoginOtps.Where(x => x.UserId == user.Id || x.CreatedAtUtc < now.AddDays(-1)).ExecuteDeleteAsync(ct);

        var token = NewToken();
        var code = NewCode(o.CodeLength);
        var row = new LoginOtp
        {
            UserId = user.Id,
            ChallengeHash = Hash(token),
            CodeHash = Hash(token + ":" + code),
            ExpiresAtUtc = now.AddMinutes(o.ExpiryMinutes),
            SentAtUtc = now,
            Sends = 1,
            SentTo = UniSmsClient.Mask(phone),
        };
        db.LoginOtps.Add(row);
        await db.SaveChangesAsync(ct);

        var sent = await sms.SendAsync(phone, Message(code, o.ExpiryMinutes), ct);
        if (!sent.Succeeded)
        {
            db.LoginOtps.Remove(row);
            await db.SaveChangesAsync(ct);
            await LogAsync(SecurityEvents.OtpFailed, user, $"The code could not be sent: {sent.Error}", client, ct);
            return OtpResult.Fail(OtpFailure.CouldNotSend, "The code could not be sent to your phone. " + sent.Error);
        }

        await LogAsync(SecurityEvents.OtpSent, user, $"A code was sent to {row.SentTo}.", client, ct);
        return new OtpResult(true, Challenge: Challenge(token, row, o), UserId: user.Id);
    }

    /// <summary>What the page shows while it waits for the code, or null when the token is not good any more.</summary>
    public async Task<OtpChallenge?> GetAsync(string? token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var row = await db.LoginOtps.AsNoTracking().FirstOrDefaultAsync(x => x.ChallengeHash == Hash(token), ct);
        return row is null || row.ConsumedAtUtc is not null || now > row.CreatedAtUtc.AddMinutes(ChallengeMinutes) ? null : Challenge(token, row, options.Value);
    }

    /// <summary>A new code for the same sign-in (the wait must be over and the codes of this sign-in not all used).</summary>
    public async Task<OtpResult> ResendAsync(string? token, ClientInfo? client = null, CancellationToken ct = default)
    {
        var o = options.Value;
        var now = clock.GetUtcNow().UtcDateTime;
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var row = string.IsNullOrWhiteSpace(token) ? null : await db.LoginOtps.Include(x => x.User).FirstOrDefaultAsync(x => x.ChallengeHash == Hash(token), ct);
        if (row is null || row.ConsumedAtUtc is not null || now > row.CreatedAtUtc.AddMinutes(ChallengeMinutes))
        {
            return OtpResult.Fail(OtpFailure.NotFound, "This sign-in is over. Type your email and password again.");
        }

        if (row.SentAtUtc.AddSeconds(o.ResendSeconds) > now)
        {
            var wait = (int)Math.Ceiling((row.SentAtUtc.AddSeconds(o.ResendSeconds) - now).TotalSeconds);
            return OtpResult.Fail(OtpFailure.Wait, $"Wait {wait} seconds before asking for a new code.");
        }

        if (row.Sends >= o.MaxSends)
        {
            return OtpResult.Fail(OtpFailure.Wait, "No more codes can be sent for this sign-in. Type your email and password again.");
        }

        var phone = PhoneNumbers.Normalize(row.User.PhoneNumber);
        if (phone is null)
        {
            return OtpResult.Fail(OtpFailure.CouldNotSend, "There is no phone number to write to.");
        }

        var code = NewCode(o.CodeLength);
        var sent = await sms.SendAsync(phone, Message(code, o.ExpiryMinutes), ct);
        if (!sent.Succeeded)
        {
            await LogAsync(SecurityEvents.OtpFailed, row.User, $"A new code could not be sent: {sent.Error}", client, ct);
            return OtpResult.Fail(OtpFailure.CouldNotSend, "The code could not be sent to your phone. " + sent.Error);
        }

        row.CodeHash = Hash(token + ":" + code);
        row.ExpiresAtUtc = now.AddMinutes(o.ExpiryMinutes);
        row.SentAtUtc = now;
        row.Sends++;
        row.Attempts = 0;
        await db.SaveChangesAsync(ct);
        await LogAsync(SecurityEvents.OtpSent, row.User, $"A new code was sent to {row.SentTo}.", client, ct);
        return new OtpResult(true, Challenge: Challenge(token!, row, o), UserId: row.UserId);
    }

    /// <summary>Checks the code the person typed. A good code is used up at once (it can't be used twice); wrong ones are counted.</summary>
    public async Task<OtpResult> VerifyAsync(string? token, string? typed, ClientInfo? client = null, CancellationToken ct = default)
    {
        var o = options.Value;
        var now = clock.GetUtcNow().UtcDateTime;
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var row = string.IsNullOrWhiteSpace(token) ? null : await db.LoginOtps.Include(x => x.User).AsNoTracking().FirstOrDefaultAsync(x => x.ChallengeHash == Hash(token), ct);
        if (row is null || row.ConsumedAtUtc is not null || now > row.CreatedAtUtc.AddMinutes(ChallengeMinutes))
        {
            return OtpResult.Fail(OtpFailure.NotFound, "This sign-in is over. Type your email and password again.");
        }

        var code = new string((typed ?? "").Where(char.IsAsciiDigit).ToArray());
        if (now > row.ExpiresAtUtc)
        {
            await LogAsync(SecurityEvents.OtpFailed, row.User, "The code had expired.", client, ct);
            return OtpResult.Fail(OtpFailure.Expired, "The code has expired. Ask for a new one.");
        }

        if (row.Attempts >= o.MaxAttempts)
        {
            return OtpResult.Fail(OtpFailure.TooManyTries, "Too many wrong codes. Type your email and password again.");
        }

        var good = code.Length == o.CodeLength
                   && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Hash(token + ":" + code)), Encoding.ASCII.GetBytes(row.CodeHash));
        if (good)
        {
            // one statement: two tries with the same code at the same moment can't both get in
            var used = await db.LoginOtps.Where(x => x.Id == row.Id && x.ConsumedAtUtc == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.ConsumedAtUtc, now), ct);
            if (used == 0)
            {
                return OtpResult.Fail(OtpFailure.NotFound, "This sign-in is over. Type your email and password again.");
            }

            await LogAsync(SecurityEvents.OtpVerified, row.User, "The code was accepted.", client, ct);
            return new OtpResult(true, UserId: row.UserId);
        }

        var tried = await db.LoginOtps.Where(x => x.Id == row.Id && x.ConsumedAtUtc == null && x.Attempts < o.MaxAttempts)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Attempts, x => x.Attempts + 1), ct);
        var left = Math.Max(0, o.MaxAttempts - (row.Attempts + (tried > 0 ? 1 : 0)));
        await LogAsync(SecurityEvents.OtpFailed, row.User, $"A wrong code was typed ({left} tries left).", client, ct);
        if (left == 0)
        {
            await db.LoginOtps.Where(x => x.Id == row.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.ConsumedAtUtc, now), ct);
            return OtpResult.Fail(OtpFailure.TooManyTries, "Too many wrong codes. Type your email and password again.");
        }

        return OtpResult.Fail(OtpFailure.WrongCode, $"That code is not right. {left} {(left == 1 ? "try" : "tries")} left.", left);
    }

    /// <summary>The person left the code box ("Use another account"): the code can't be used any more.</summary>
    public async Task CancelAsync(string? token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.LoginOtps.Where(x => x.ChallengeHash == Hash(token) && x.ConsumedAtUtc == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.ConsumedAtUtc, now), ct);
    }

    /// <summary>The words of a message that only tells the number works (the button of System Settings).</summary>
    public async Task<SmsResult> SendTestAsync(int adminUserId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var admin = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == adminUserId && u.IsActive && u.RoleId == AppRoles.AdminId, ct);
        if (admin is null)
        {
            return SmsResult.Fail("Only an administrator can send a test message.");
        }

        var phone = PhoneNumbers.Normalize(admin.PhoneNumber);
        if (phone is null)
        {
            return SmsResult.Fail("Your account has no phone number. Type it in System Settings, Login code (OTP).");
        }

        var result = await sms.SendAsync(phone, $"{systemSettings.Current.SchoolShortName} LMS: this is a test message. UniSMS works, so login codes can reach this phone.", ct);
        await LogAsync(result.Succeeded ? SecurityEvents.OtpSent : SecurityEvents.OtpFailed, admin, result.Succeeded ? $"A test message was sent to {UniSmsClient.Mask(phone)}." : $"The test message could not be sent: {result.Error}", null, ct);
        return result;
    }

    /// <summary>The phone number of the administrator's own account (where the codes go). It is kept as +639171234567; it can be typed as 09171234567. An empty box takes the number away.</summary>
    public async Task<ServiceResult> SetPhoneAsync(int adminUserId, string? typed, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var admin = await db.Users.FirstOrDefaultAsync(u => u.Id == adminUserId && u.IsActive && u.RoleId == AppRoles.AdminId, ct);
        if (admin is null)
        {
            return ServiceResult.Fail("Only an administrator can change the number of the codes.");
        }

        string? phone = null;
        if (!string.IsNullOrWhiteSpace(typed))
        {
            phone = PhoneNumbers.Normalize(typed);
            if (phone is null)
            {
                return ServiceResult.Fail("Type a phone number like 09171234567.");
            }
        }

        if (admin.PhoneNumber == phone)
        {
            return ServiceResult.Ok("Nothing changed.");
        }

        admin.PhoneNumber = phone;
        await db.SaveChangesAsync(ct);
        await LogAsync(SecurityEvents.AccountChanged, admin, phone is null ? "The phone number of the codes was taken away." : $"The phone number of the codes was set to {UniSmsClient.Mask(phone)}.", null, ct);
        return ServiceResult.Ok(phone is null ? "The number was taken away." : "The number was saved.");
    }

    /// <summary>The number of the account as people type it (09171234567), for the box of System Settings.</summary>
    public async Task<string> GetPhoneForBoxAsync(int adminUserId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var stored = await db.Users.AsNoTracking().Where(u => u.Id == adminUserId).Select(u => u.PhoneNumber).FirstOrDefaultAsync(ct);
        return PhoneNumbers.ToLocal(stored);
    }

    /// <summary>What is missing before OTP can be turned on (an empty list when it can).</summary>
    public async Task<List<string>> MissingForOtpAsync(int adminUserId, CancellationToken ct = default)
    {
        var missing = new List<string>();
        if (!sms.IsConfigured)
        {
            missing.Add("UniSMS is not set up: put the API key in the user secrets or in appsettings.Local.json (see the README), then start the app again.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var phone = await db.Users.AsNoTracking().Where(u => u.Id == adminUserId).Select(u => u.PhoneNumber).FirstOrDefaultAsync(ct);
        if (PhoneNumbers.Normalize(phone) is null)
        {
            missing.Add("Your account has no phone number. Type it in System Settings, Login code (OTP) (like 09171234567).");
        }

        return missing;
    }

    /// <summary>The message: short, with the school, the code, how long it lives and a warning.</summary>
    public string Message(string code, int minutes) =>
        $"{systemSettings.Current.SchoolShortName} LMS: your login code is {code}. It expires in {minutes} minutes. Never share this code with anyone.";

    // ---------------------------------------------------------------- helpers

    private static OtpChallenge Challenge(string token, LoginOtp row, OtpOptions o) =>
        new(token, row.SentTo, row.ExpiresAtUtc, row.SentAtUtc.AddSeconds(o.ResendSeconds), Math.Max(0, o.MaxAttempts - row.Attempts));

    private static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static string NewCode(int length)
    {
        length = Math.Clamp(length, 4, 9);
        var digits = new char[length];
        for (var i = 0; i < length; i++)
        {
            digits[i] = (char)('0' + RandomNumberGenerator.GetInt32(10));
        }

        return new string(digits);
    }

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private Task LogAsync(string kind, User user, string details, ClientInfo? client, CancellationToken ct) =>
        securityLog is null ? Task.CompletedTask : securityLog.AddAsync(kind, user.Id, user.Email, details, client, ct: ct);
}
