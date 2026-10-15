namespace SEAITT_LMS.Auth;

/// <summary>Settings for the custom login, bound from the "Auth" section of appsettings.json.</summary>
public class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>Wrong passwords in a row before the account is locked.</summary>
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>How long an account stays locked after too many wrong passwords.</summary>
    public int LockoutMinutes { get; set; } = 5;

    /// <summary>How long a normal sign-in lasts (sliding).</summary>
    public int SessionHours { get; set; } = 8;

    /// <summary>How long "keep me signed in" lasts.</summary>
    public int RememberMeDays { get; set; } = 14;

    /// <summary>
    /// A signed-in user is re-checked against the database this often, so a deactivated account or a
    /// changed security stamp stops working within this many minutes.
    /// </summary>
    public int RevalidateMinutes { get; set; } = 5;
}
