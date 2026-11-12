using System.Security.Claims;
using SEAITT_LMS.Auth;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <param name="Key">What is saved with the role ("calendar").</param>
/// <param name="Path">The address of the page, without the query ("analytics").</param>
/// <param name="Area">The staff role the page belongs to (<see cref="AppRoles.Registrar"/> ...), or null for the pages every account has and the analytics. A person who has a page of an area
/// is given the work of that area for it (see <see cref="StaffAreas"/>).</param>
/// <param name="Exact">True for the dashboard of an area: only this address, not the pages under it.</param>
public sealed record StaffPage(string Key, string Title, string Path, string Icon, string Hint, string Group, string? Area = null, bool Exact = false);

/// <summary>
/// The pages the administrator can tick for a role of their own (Add staff, a new role). A person with such a role signs in to their own page (<c>/staff</c>) and opens only the ticked pages;
/// the menu shows only those too. The accounts of the usual roles (registrar, guidance counselor ...) are not affected.
/// </summary>
public static class StaffPages
{
    public const string General = "Every account";
    public const string Analytics = "Analytics";
    public const string RegistrarGroup = "Registrar";
    public const string GuidanceGroup = "Guidance counselor";
    public const string CoordinatorGroup = "Academic coordinator";
    public const string ProviderGroup = "Academic provider";

    public static readonly IReadOnlyList<StaffPage> All =
    [
        new("calendar", "Calendar", "/calendar", "calendar-days", "Month and week, with the announcements and the holidays", General),
        new("users", "Users", "/users", "users-round", "The students, the teachers and the staff of the school", General),
        new("groups", "Groups", "/groups", "users", "The groups of the person", General),
        new("analytics-school", "Analytics · the school", "/analytics", "chart-column", "How the students do, by grade level and strand", Analytics),
        new("analytics-guidance", "Analytics · guidance", "/analytics", "heart-handshake", "Attendance and the students with a concern", Analytics),
        new("analytics-library", "Analytics · course library", "/provider/analytics", "folder-open", "What the teachers and students opened of the library", Analytics),
        new("analytics-evaluation", "Analytics · teacher evaluation", "/coordinator/analytics", "clipboard-check", "The ratings of the teachers", Analytics),

        // Round 41: the pages of the other staff roles can be ticked too (not the pages of the administrator).
        new("registrar-dashboard", "Registrar dashboard", "/registrar", "layout-dashboard", "The numbers of the enrollments", RegistrarGroup, AppRoles.Registrar, Exact: true),
        new("registrar-enrollment", "Enrollment", "/registrar/enrollments", "clipboard-list", "Enroll a student, one by one or from Excel", RegistrarGroup, AppRoles.Registrar),
        new("registrar-accounts", "Student's Accounts", "/registrar/student-accounts", "graduation-cap", "The enrolled students, their grades and their accounts", RegistrarGroup, AppRoles.Registrar),
        new("guidance-dashboard", "Guidance dashboard", "/guidance", "layout-dashboard", "The students with a concern", GuidanceGroup, AppRoles.GuidanceCounselor, Exact: true),
        new("guidance-referrals", "Referrals", "/guidance/referrals", "heart-handshake", "Write a concern, the notes and the schedule of a student", GuidanceGroup, AppRoles.GuidanceCounselor),
        new("coordinator-dashboard", "Coordinator dashboard", "/coordinator", "layout-dashboard", "The school now", CoordinatorGroup, AppRoles.AcademicCoordinator, Exact: true),
        new("coordinator-setup", "Academic setup", "/coordinator/academic-setup", "settings-2", "School years, tracks, strands, courses and class sections", CoordinatorGroup, AppRoles.AcademicCoordinator),
        new("coordinator-sections", "Class sections", "/coordinator/sections", "layout-grid", "The sections, their advisers and their students", CoordinatorGroup, AppRoles.AcademicCoordinator),
        new("coordinator-evaluations", "Teacher evaluation", "/coordinator/evaluations", "clipboard-check", "The evaluations the students answer about their teachers", CoordinatorGroup, AppRoles.AcademicCoordinator),
        new("coordinator-announcements", "Announcements", "/coordinator/announcements", "megaphone", "The notices of the school and the holidays", CoordinatorGroup, AppRoles.AcademicCoordinator),
        new("provider-dashboard", "Provider dashboard", "/provider", "layout-dashboard", "What the teachers did with the library", ProviderGroup, AppRoles.AcademicProvider, Exact: true),
        new("provider-library", "Course library", "/provider/library", "folder-open", "The handouts and the summative exams of the courses", ProviderGroup, AppRoles.AcademicProvider),
        new("provider-tracking", "Tracking", "/provider/tracking", "list-checks", "Which teachers got and used what was sent", ProviderGroup, AppRoles.AcademicProvider),
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
        return StaffPages.All.Any(p => pages.Contains(p.Key) && (p.Exact ? clean == p.Path : Under(clean, p.Path)));
    }
}

/// <summary>
/// The work of the other staff roles for the account of a role the administrator made up (round 41). A person with a page of an area (registrar, guidance counselor, academic coordinator,
/// academic provider) gets the role of the area besides the base role at sign-in (so the page opens) and the services accept them as a person of the area: <see cref="InArea"/> is
/// the check that reads the database at the moment of saving.
/// </summary>
public static class StaffAreas
{
    private static string Prefix(string area) => area switch
    {
        AppRoles.Registrar => ",registrar-",
        AppRoles.GuidanceCounselor => ",guidance-",
        AppRoles.AcademicCoordinator => ",coordinator-",
        AppRoles.AcademicProvider => ",provider-",
        _ => ",none-",
    };

    /// <summary>The areas the ticked pages belong to ("Registrar", "AcademicCoordinator" ...), once each, in the order of the catalog.</summary>
    public static IReadOnlyList<string> Of(string? savedPages)
    {
        var keys = StaffPages.Parse(savedPages);
        return [.. StaffPages.All.Where(p => p.Area is not null && keys.Contains(p.Key)).Select(p => p.Area!).Distinct()];
    }

    /// <summary>The accounts of the role itself and the accounts of a made-up role that has at least one page of the area.</summary>
    public static IQueryable<User> InArea(this IQueryable<User> users, string area)
    {
        var roleId = AppRoles.Ids[area];
        var prefix = Prefix(area);
        return users.Where(u => u.RoleId == roleId || (u.CustomRole != null && ("," + u.CustomRole.Pages).Contains(prefix)));
    }
}
