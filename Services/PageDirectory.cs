using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>One page of the system as the search shows it.</summary>
public sealed record PageEntry(string Title, string Description, string Href, string Icon);

/// <summary>
/// The pages each role can open, for the search box. (The menu on the left, Components/Layout/NavMenu.razor, lists the
/// same pages: when a page is added, add it in both places.)
/// </summary>
public static class PageDirectory
{
    private static readonly PageEntry MyAccount = new("My account", "Your details and sign-in name", "account", "id-card");

    public static IReadOnlyList<PageEntry> For(string role) => role switch
    {
        AppRoles.Admin =>
        [
            new("Dashboard", "Accounts and the school setup at a glance", "admin", "layout-dashboard"),
            new("Users Management", "Students, teachers, registrars, guidance and administrators", "admin/users", "users"),
            MyAccount,
        ],
        AppRoles.Registrar =>
        [
            new("Dashboard", "Enrollment at a glance", "registrar", "layout-dashboard"),
            new("Enrollments", "Enroll a student", "registrar/enrollments", "clipboard-list"),
            new("Student's Accounts", "The enrolled students", "registrar/student-accounts", "graduation-cap"),
            MyAccount,
        ],
        AppRoles.Teacher =>
        [
            new("Dashboard", "Work to check, deadlines and drafts", "teacher", "layout-dashboard"),
            new("Subjects", "The subjects you teach, with their modules and work", "teacher/subjects", "book-open"),
            new("Calendar", "Deadlines, postings and your to-do list, by month or week", "calendar", "calendar-days"),
            MyAccount,
        ],
        AppRoles.Student =>
        [
            new("Dashboard", "Assignments, due dates, new modules and achievements", "student", "layout-dashboard"),
            new("Subjects", "Your subjects, modules, activities and quizzes", "student/subjects", "book-open"),
            new("Grades and progress", "Your grade in every subject and how it is going", "student/grades", "trending-up"),
            new("Calendar", "Deadlines and your to-do list, by month or week", "calendar", "calendar-days"),
            MyAccount,
        ],
        _ => [MyAccount],
    };
}
