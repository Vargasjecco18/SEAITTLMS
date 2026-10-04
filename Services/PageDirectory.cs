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
            new("Archive", "Archive the students who graduate, restore a student", "admin/archive", "archive"),
            new("Analytics", "How students are doing across the school", "analytics", "chart-column"),
            MyAccount,
        ],
        AppRoles.Registrar =>
        [
            new("Dashboard", "Enrollment at a glance", "registrar", "layout-dashboard"),
            new("Enrollments", "Enroll a student", "registrar/enrollments", "clipboard-list"),
            new("Student's Accounts", "The enrolled students", "registrar/student-accounts", "graduation-cap"),
            new("Withdrawals", "Answer withdrawal requests, withdraw a student", "registrar/withdrawals", "user-x"),
            MyAccount,
        ],
        AppRoles.Teacher =>
        [
            new("Dashboard", "Work to check, deadlines and drafts", "teacher", "layout-dashboard"),
            new("Subjects", "The subjects you teach, with their modules and work", "teacher/subjects", "book-open"),
            new("Attendance", "Mark who came to class and see the attendance summary", "teacher/attendance", "calendar-check"),
            new("Gradebook", "The scores of every student in a subject, and their average", "teacher/gradebook", "clipboard-list"),
            new("Analytics", "How the students of your subjects are doing", "analytics", "chart-column"),
            new("Calendar", "Deadlines, postings and your to-do list, by month or week", "calendar", "calendar-days"),
            MyAccount,
        ],
        AppRoles.Student =>
        [
            new("Dashboard", "Assignments, due dates, new modules and achievements", "student", "layout-dashboard"),
            new("Subjects", "Your subjects, modules, activities and quizzes", "student/subjects", "book-open"),
            new("Grades and progress", "Your grade in every subject and how it is going", "student/grades", "trending-up"),
            new("Withdrawal", "Ask the registrar to withdraw you from school", "student/withdrawal", "user-x"),
            new("Calendar", "Deadlines and your to-do list, by month or week", "calendar", "calendar-days"),
            MyAccount,
        ],
        AppRoles.GuidanceCounselor =>
        [
            new("Dashboard", "Referrals and students who need help", "guidance", "layout-dashboard"),
            new("Referrals", "Students the system sent to you", "guidance/referrals", "flag"),
            new("Students at risk", "Failing (below 70), at risk and not attending class", "guidance/students", "triangle-alert"),
            new("Analytics", "How students are doing across the school", "analytics", "chart-column"),
            MyAccount,
        ],
        _ => [MyAccount],
    };
}
