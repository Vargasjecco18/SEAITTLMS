namespace SEAITT_LMS.Auth;

public static class LocalUrl
{
    /// <summary>
    /// Returns <paramref name="url"/> only when it is a path on this site ("/lessons/1"), otherwise
    /// <paramref name="fallback"/>. Stops "?returnUrl=https://evil.example" open-redirect tricks after login.
    /// </summary>
    public static string Sanitize(string? url, string fallback = "/")
    {
        if (string.IsNullOrEmpty(url) || url[0] != '/')
        {
            return fallback;
        }

        // "//host" and "/\host" are treated as other sites by browsers.
        if (url.Length > 1 && (url[1] == '/' || url[1] == '\\'))
        {
            return fallback;
        }

        // CR/LF, tabs and other control characters have no business in a redirect target.
        if (url.Any(char.IsControl))
        {
            return fallback;
        }

        return url;
    }

    /// <summary>The pages every signed-in person has (not the pages of one role).</summary>
    private static readonly string[] SharedPages = ["/calendar", "/users", "/groups", "/resources", "/analytics", "/help", "/messages", "/notifications"];

    /// <summary>
    /// True when <paramref name="path"/> is a page of the role whose home page is <paramref name="home"/> ("/teacher/subjects/5" is a page of "/teacher"), or a page everybody has. After a sign-in
    /// it keeps a person from being sent to a page of another role.
    /// </summary>
    public static bool IsPageOf(string path, string home)
    {
        var clean = path.Split('?', '#')[0];
        bool Under(string root) => clean.Equals(root, StringComparison.OrdinalIgnoreCase) || clean.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);
        return clean.Length > 1 && (Under(home) || SharedPages.Any(Under));
    }
}
