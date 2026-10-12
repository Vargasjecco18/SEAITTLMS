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
    private static readonly PageEntry Help = new("Help", "How to use the system, for your role", "help", "circle-help");

    /// <summary>The pages of a role, and the Help page that every role has.</summary>
    public static IReadOnlyList<PageEntry> For(string role) => Pages(role) is { Count: > 0 } pages ? [.. pages, Help] : [];

    private static IReadOnlyList<PageEntry> Pages(string role) => role switch
    {
        AppRoles.Admin =>
        [
            new("Dashboard", "Accounts and enrollment at a glance", "admin", "layout-dashboard"),
            new("Users Management", "Students, teachers, registrars, guidance, academic coordinators and administrators", "admin/users", "users"),
            new("Enrolled", "The students who are enrolled", "admin/enrollment/enrolled", "user-check"),
            new("Not enrolled", "The students who are not enrolled yet", "admin/enrollment/not-enrolled", "user-x"),
            new("Archive Accounts", "Archive the students who graduate, restore a student", "admin/archive", "archive"),
            new("Analytics", "How students are doing across the school", "analytics", "chart-column"),
            new("Security Logs", "Sign-ins, failed sign-ins and changes to accounts and the system", "admin/security-logs", "scroll-text"),
            new("Database Backup & Restore", "Save a copy of all the data, and put it back", "admin/backups", "database-backup"),
            new("System Settings", "Maintenance mode, backups and the log", "admin/settings", "settings-2"),
        ],
        AppRoles.AcademicCoordinator =>
        [
            new("Dashboard", "Evaluations, announcements and the school setup at a glance", "coordinator", "layout-dashboard"),
            new("Academic setup", "School years, terms, tracks, strands, courses and sections", "coordinator/academic-setup", "school"),
            new("Teacher evaluation", "Questionnaires for the students about their teachers, with their responses", "coordinator/evaluations", "clipboard-check"),
            new("Announcements", "Exams, holidays, the semester break, teachers' meetings and other notices", "coordinator/announcements", "megaphone"),
            new("Analytics", "How the teachers were evaluated", "coordinator/analytics", "chart-column"),
        ],
        AppRoles.AcademicProvider =>
        [
            new("Dashboard", "What you sent to the teachers, and who opened it", "provider", "layout-dashboard"),
            new("Course library", "Syllabus, modules, activities, quizzes and task performances to send to the teachers", "provider/library", "folder-open"),
            new("Tracking", "Every module you sent, the teachers who got it and the students who see it, and whether they opened it", "provider/tracking", "eye"),
            new("Analytics", "How far what you send gets: teachers, students, courses", "provider/analytics", "chart-column"),
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
            new("Calendar", "Deadlines and postings, by month or week", "calendar", "calendar-days"),
            new("Announcements", "Write to the students of your classes, and read the notices of the school", "teacher/announcements", "megaphone"),
            new("Gradebook", "The scores of every student in a course, and their average", "teacher/gradebook", "clipboard-list"),
            new("Analytics", "How the students of your courses are doing", "analytics", "chart-column"),
            new("Users", "Your friends, your students and all the teachers", "users", "users-round"),
            new("Groups", "Study groups and teams you are in", "groups", "users"),
            new("Resources", "Your locker and the files shared with you", "resources", "folder-open"),
        ],
        AppRoles.Student =>
        [
            new("Dashboard", "Assignments, due dates, new modules and achievements", "student", "layout-dashboard"),
            new("Courses", "Your courses, modules, activities and quizzes", "student/subjects", "book-open"),
            new("Calendar", "Deadlines, by month or week", "calendar", "calendar-days"),
            new("Grades and progress", "Your grade in every course and how it is going", "student/grades", "trending-up"),
            new("Users", "Your classmates, all the students, your friends and your teachers", "users", "users-round"),
            new("Groups", "Study groups and teams you are in", "groups", "users"),
            new("Resources", "Your locker and the files shared with you", "resources", "folder-open"),
        ],
        AppRoles.GuidanceCounselor =>
        [
            new("Dashboard", "Referrals and students who need help", "guidance", "layout-dashboard"),
            new("Referrals", "Students the system sent to you: failing, at risk and not attending, with grade level and course", "guidance/referrals", "flag"),
            new("Analytics", "How students are doing across the school", "analytics", "chart-column"),
        ],
        _ => [],
    };
}
