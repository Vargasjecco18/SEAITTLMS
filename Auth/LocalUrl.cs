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
}
