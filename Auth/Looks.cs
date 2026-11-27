namespace SEAITT_LMS.Auth;

/// <summary>
/// Round 48: the looks (appearances) a person can choose in Settings, and the cookie that tells the server which one to draw. Nobody who is not signed in has a look: the login page is always
/// the Default look; an account that never chose one opens with Default; an account that did opens with the last one it chose (it is kept in <see cref="Data.Entities.User.Look"/>).
/// </summary>
public static class Looks
{
    public const string CookieName = "seaitt-theme";

    public const string Default = "default";

    /// <summary>Follows the setting of the computer (light or dark): the page has no data-theme then.</summary>
    public const string Auto = "auto";

    public static readonly IReadOnlyList<string> All = ["default", "light", "dark", "midnight", "paper", "auto"];

    /// <summary>The look written the usual way (lower case), or null when it is not one of the six.</summary>
    public static string? Normalize(string? value)
    {
        var text = value?.Trim().ToLowerInvariant();
        return text is not null && All.Contains(text) ? text : null;
    }

    /// <summary>The look the page is drawn with for what the cookie says: Default when there is no cookie or it is not a look; null (no data-theme) for Auto.</summary>
    public static string? ThemeOf(string? cookie) => Normalize(cookie) switch
    {
        null => Default,
        Auto => null,
        var look => look,
    };
}
