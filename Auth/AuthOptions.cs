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

    /// <summary>
    /// A signed-in user is re-checked against the database this often, so a deactivated account or a
    /// changed security stamp stops working within this many minutes.
    /// </summary>
    public int RevalidateMinutes { get; set; } = 5;

    /// <summary>
    /// One sign-in at a time for each account: while someone is using the account in a browser or on a device, signing in with it somewhere else is refused
    /// (the login page says so at the top). Turn it off to allow several.
    /// </summary>
    public bool SingleSession { get; set; } = true;

    /// <summary>
    /// A sign-in is taken as ended when it was not seen for this many minutes (a page that is open says it is there every minute). So after a browser was closed without
    /// pressing Log out, the account can be used somewhere else after this long.
    /// </summary>
    public int SessionIdleMinutes { get; set; } = 3;

    /// <summary>
    /// A person who does nothing for this many minutes (no mouse, key, touch or scroll in any page of the app) sees a message at the top center: the account will be logged out
    /// automatically after <see cref="AfkLogoutMinutes"/> more minutes (wwwroot/app.js). Any click or key press, or the button in the message, keeps the account signed in.
    /// </summary>
    public int AfkWarnMinutes { get; set; } = 30;

    /// <summary>How many minutes after the message the account is logged out by itself, if the person did nothing.</summary>
    public int AfkLogoutMinutes { get; set; } = 10;
}
