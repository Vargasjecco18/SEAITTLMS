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
    public static IReadOnlyList<PageEntry> For(string role) => role switch
    {
        AppRoles.Admin =>
        [
            new("Dashboard", "Accounts and the school setup at a glance", "admin", "layout-dashboard"),
            new("Users Management", "Students, teachers, registrars, guidance and administrators", "admin/users", "users"),
            new("School setup", "School years, terms, tracks, strands, courses and sections", "admin/school-setup", "school"),
            new("Announcements", "Write notices for students, teachers or everybody", "announcements", "megaphone"),
            new("Archive Accounts", "Archive the students who graduate, restore a student", "admin/archive", "archive"),
            new("Analytics", "How students are doing across the school", "analytics", "chart-column"),
        ],
        AppRoles.Registrar =>
        [
            new("Dashboard", "Enrollment at a glance", "registrar", "layout-dashboard"),
            new("Enrollments", "Enroll a student", "registrar/enrollments", "clipboard-list"),
            new("Student's Accounts", "The enrolled students, and who is enrolled for the next term", "registrar/student-accounts", "graduation-cap"),
        ],
        AppRoles.Teacher =>
        [
            new("Dashboard", "Work to check and drafts", "teacher", "layout-dashboard"),
            new("Courses", "The courses you teach, with their modules and work", "teacher/subjects", "book-open"),
            new("Attendance", "Mark who came to class and see the attendance summary", "teacher/attendance", "calendar-check"),
            new("Calendar", "Deadlines, postings and your to-do list, by month or week", "calendar", "calendar-days"),
            new("Gradebook", "The scores of every student in a course, and their average", "teacher/gradebook", "clipboard-list"),
            new("Analytics", "How the students of your courses are doing", "analytics", "chart-column"),
        ],
        AppRoles.Student =>
        [
            new("Dashboard", "Assignments, due dates, new modules and achievements", "student", "layout-dashboard"),
            new("Courses", "Your courses, modules, activities and quizzes", "student/subjects", "book-open"),
            new("Grades and progress", "Your grade in every course and how it is going", "student/grades", "trending-up"),
            new("Calendar", "Deadlines and your to-do list, by month or week", "calendar", "calendar-days"),
        ],
        AppRoles.GuidanceCounselor =>
        [
            new("Dashboard", "Referrals and students who need help", "guidance", "layout-dashboard"),
            new("Referrals", "Students the system sent to you", "guidance/referrals", "flag"),
            new("Students at risk", "Failing (below 70), at risk and not attending class", "guidance/students", "triangle-alert"),
            new("Analytics", "How students are doing across the school", "analytics", "chart-column"),
        ],
        _ => [],
    };
}
