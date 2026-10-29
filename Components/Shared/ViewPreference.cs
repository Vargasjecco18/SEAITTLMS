using Microsoft.AspNetCore.Http;

namespace SEAITT_LMS.Components.Shared;

/// <summary>
/// The way a person looks at a page (Cards or List for the courses, Month or Week for the calendar) is kept in a cookie by wwwroot/app.js whenever a link
/// with <c>view=</c> is followed, so the next page that is opened without a <c>view=</c> in its address (a tab of the courses, the Courses or Calendar
/// link of the menu) shows it the same way, and the person does not have to choose it again.
/// </summary>
public static class ViewPreference
{
    /// <summary>What the address says (<paramref name="fromAddress"/>) when it is one of the <paramref name="allowed"/> values, else what the person chose last, else the first of <paramref name="allowed"/>.</summary>
    public static string Resolve(HttpContext? http, string page, string? fromAddress, params string[] allowed)
    {
        if (fromAddress is not null && allowed.Contains(fromAddress, StringComparer.Ordinal))
        {
            return fromAddress;
        }

        var remembered = http?.Request.Cookies["seaitt-view-" + page];
        return remembered is not null && allowed.Contains(remembered, StringComparer.Ordinal) ? remembered : allowed[0];
    }
}
