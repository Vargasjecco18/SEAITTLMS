using System.Security.Claims;
using SEAITT_LMS.Auth;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <param name="Key">What is saved with the role ("calendar").</param>
/// <param name="Path">The address of the page, without the query ("analytics").</param>
public sealed record StaffPage(string Key, string Title, string Path, string Icon, string Hint, string Group);

/// <summary>
/// The pages the administrator can tick for a role of their own (Add staff, a new role). A person with such a role signs in to their own page (<c>/staff</c>) and opens only the ticked pages;
/// the menu shows only those too. The accounts of the usual roles (registrar, guidance counselor ...) are not affected.
/// </summary>
public static class StaffPages
{
    public const string General = "Every account";
    public const string Analytics = "Analytics";

    public static readonly IReadOnlyList<StaffPage> All =
    [
        new("calendar", "Calendar", "/calendar", "calendar-days", "Month and week, with the announcements and the holidays", General),
        new("users", "Users", "/users", "users-round", "The students, the teachers and the staff of the school", General),
        new("groups", "Groups", "/groups", "users", "The groups of the person", General),
        new("analytics-school", "Analytics · the school", "/analytics", "chart-column", "How the students do, by grade level and strand", Analytics),
        new("analytics-guidance", "Analytics · guidance", "/analytics", "heart-handshake", "Attendance and the students with a concern", Analytics),
        new("analytics-library", "Analytics · course library", "/provider/analytics", "folder-open", "What the teachers and students opened of the library", Analytics),
        new("analytics-evaluation", "Analytics · teacher evaluation", "/coordinator/analytics", "clipboard-check", "The ratings of the teachers", Analytics),
    ];

    public static StaffPage? Find(string key) => All.FirstOrDefault(p => p.Key == key);

    /// <summary>The keys of a saved list ("calendar,users"), only the ones that exist, no repeats, always in the order of <see cref="All"/> (so the same pages are always saved the same way).</summary>
    public static IReadOnlyList<string> Parse(string? saved)
    {
        var wanted = (saved ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();
        return [.. All.Where(p => wanted.Contains(p.Key)).Select(p => p.Key)];
    }

    public static string Join(IEnumerable<string> keys) => string.Join(',', Parse(string.Join(',', keys)));
}

/// <summary>Who may open which page, for the accounts that have a role of their own. Used by the layout (every page goes through it) and by the menu.</summary>
public static class StaffAccess
{
    /// <summary>True for the account of a role the administrator made up in Add staff.</summary>
    public static bool IsCustom(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true && user.HasClaim(c => c.Type == AppClaimTypes.RoleTitle);

    /// <summary>The pages that were ticked for the role of the person.</summary>
    public static IReadOnlyList<string> PagesOf(ClaimsPrincipal user) => StaffPages.Parse(user.FindFirstValue(AppClaimTypes.Pages));

    /// <summary>The pages every account has, whatever its role.</summary>
    private static readonly string[] Always = ["/staff", "/account", "/help", "/files", "/photos", "/messages", "/notifications", "/access-denied", "/not-found", "/error"];

    private static readonly string[] CrossRolePages = ["/analytics", "/coordinator/analytics", "/provider/analytics"];

    private static string Clean(string? path)
    {
        var text = (path ?? "/").Split('?', '#')[0].TrimEnd('/').ToLowerInvariant();
        return text.Length == 0 ? "/" : text.StartsWith('/') ? text : "/" + text;
    }

    private static bool Under(string path, string root) => path == root || path.StartsWith(root + "/", StringComparison.Ordinal);

    /// <summary>May the person open this address (the path of the page)?</summary>
    public static bool Allows(ClaimsPrincipal user, string? path)
    {
        var clean = Clean(path);
        if (user.Identity?.IsAuthenticated != true)
        {
            return true;
        }

        if (!IsCustom(user))
        {
            // The pages that the other roles open through the analytics of another role (the pages of the role that were ticked) are not for the academic providers that have no role of their own.
            return !(user.IsInRole(AppRoles.AcademicProvider) && CrossRolePages.Where(p => p != "/provider/analytics").Any(p => Under(clean, p)));
        }

        if (Always.Any(p => Under(clean, p)))
        {
            return true;
        }

        var pages = PagesOf(user);
        return StaffPages.All.Any(p => pages.Contains(p.Key) && Under(clean, p.Path));
    }
}
