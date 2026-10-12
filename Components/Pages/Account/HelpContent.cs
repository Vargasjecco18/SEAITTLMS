using SEAITT_LMS.Data.Entities;
using SEAITT_LMS.Services;

namespace SEAITT_LMS.Components.Pages.Account;

/// <summary>One question of the Help page with its answer as short steps (or one paragraph).</summary>
public sealed record HelpTopic(string Question, string[] Steps, string? Link = null, string? LinkText = null)
{
    public string Text => Question + " " + string.Join(' ', Steps);
}

/// <summary>A group of questions under one title.</summary>
public sealed record HelpSection(string Title, string Icon, IReadOnlyList<HelpTopic> Topics);

/// <summary>
/// What the Help page says. The first section is for everybody; the others are for the role of the person. When a page of the system
/// changes, change what is written here about it.
/// </summary>
public static class HelpContent
{
    public static IReadOnlyList<HelpSection> For(string role, SystemSettingsValues settings)
    {
        var sections = new List<HelpSection> { Everybody(settings) };
        sections.AddRange(role switch
        {
            AppRoles.Student => [Student()],
            AppRoles.Teacher => [Teacher()],
            AppRoles.Admin => [Admin()],
            AppRoles.Registrar => [Registrar()],
            AppRoles.AcademicCoordinator => [Coordinator()],
            AppRoles.AcademicProvider => [Provider()],
            AppRoles.GuidanceCounselor => [Guidance()],
            _ => Array.Empty<HelpSection>(),
        });

        return sections;
    }

    public static string GuideTitle(string role) => role switch
    {
        AppRoles.Student => "For students",
        AppRoles.Teacher => "For teachers",
        AppRoles.Admin => "For the administrator",
        AppRoles.Registrar => "For the registrar",
        AppRoles.AcademicCoordinator => "For the academic coordinator",
        AppRoles.AcademicProvider => "For the academic provider",
        AppRoles.GuidanceCounselor => "For the guidance counselor",
        _ => "Your pages",
    };

    private static HelpSection Everybody(SystemSettingsValues settings) => new("Getting started", "circle-help",
    [
        new("How do I sign in and sign out?",
        [
            "Open the address of the system and type your email and your password.",
            "Tick “Keep me signed in on this device” only on your own computer or phone.",
            "To sign out, press the arrow button at the bottom of the menu, next to your name.",
            $"After {settings.MaxFailedAttempts} wrong passwords in a row the account is locked for {settings.LockoutMinutes} minutes. Wait, or ask the administrator.",
        ]),
        new("I forgot my password.",
        [
            "Ask the administrator of the school. An administrator can set a new password for you (Users Management, Edit).",
            "Then sign in with the new password.",
        ]),
        new("How do I find a course, a person or a page?",
        [
            "Press the search box in the middle of the bar at the top and type a few letters.",
            "The results are courses, people and pages. Press one to open it.",
        ]),
        new("What is the menu on the left?",
        [
            "It lists the pages for your role. Rest the pointer on a page and a small box opens beside it showing what is inside, with links to its parts (for example, Courses shows Enrolled and Completed).",
            "On a phone, the menu button at the top left opens it.",
        ]),
        new("How do I change the look, or my picture?",
        [
            "Press the gear at the top right (Settings).",
            "Under Profile picture, press “Upload a picture” and choose a photo. It is made small for you. “Remove” brings back your initials.",
            "Under Appearance choose Light, Dark or Auto (Auto follows your computer). You can also switch the animations off.",
        ]),
        new("How do messages work?",
        [
            "Press the speech bubble at the top. The list shows your conversations; the search box finds a person you may write to.",
            MessagesRule(settings),
            "A number on the bubble tells how many messages you have not read.",
        ]),
        new("What do the notifications say?",
        [
            "The bell at the top lists what happened for you: a new module, a grade, a deadline coming, a friend request, a file someone shared.",
            "Press one to go to the page it is about. “Mark all as read” clears the number.",
        ]),
    ]);

    private static string MessagesRule(SystemSettingsValues settings) => settings.MessagesHaveClosingHours
        ? $"Messages can't be sent from {MessagingHours.HourText(settings.MessagesCloseHour)} to {MessagingHours.HourText(settings.MessagesOpenHour)} (Philippine time). You can still read them."
        : "You can write at any hour.";

    private static HelpSection Student() => new("For students", "graduation-cap",
    [
        new("Where are my courses, and how far along am I?",
        [
            "Open Courses in the menu. Enrolled shows this term; Completed shows the courses of terms that are over.",
            "Each course has a progress bar. It counts the modules you opened and the activities and quizzes you finished, out of everything your teacher posted.",
            "Switch between cards and a list with the buttons at the top right of the page.",
        ], "student/subjects", "Open Courses"),
        new("How do I open a module and do the activities?",
        [
            "Open a course, then a module. The syllabus of the course is the first thing in the list.",
            "Under a module you find its activities, quizzes and task performances. Open one, read the instructions and send your answer before the due date.",
            "A module appears when your teacher posts it. The ones you have not opened yet show on your dashboard.",
        ]),
        new("How does a quiz work?",
        [
            "Read the time limit and the rules before you start. The timer keeps running even if you close the page.",
            "Some quizzes watch if you leave the page. Leaving it too many times ends the quiz and sends what you have.",
            "When you send it, the result is shown if your teacher allowed it.",
        ]),
        new("Where are my grades?",
        [
            "Open Grades in the menu: your grade in every course and how it is going. A course below 70 is marked.",
            "The dashboard shows your recent grades and your average.",
        ], "student/grades", "Open Grades"),
        new("How do I see my deadlines?",
        [
            "Open Calendar. Switch between month and week. A dot on a day is something due.",
            "Press an activity to go to it.",
        ], "calendar", "Open Calendar"),
        new("How do I evaluate a teacher?",
        [
            "When the academic coordinator sends a questionnaire, a card appears on your dashboard and you get a notification.",
            "Answer one questionnaire for each of your teachers before the due date. Your answers are anonymous: the teacher and the coordinator see the ratings, not who gave them.",
        ], "student/evaluations", "Open the evaluations"),
        new("How do I add friends and see my classmates?",
        [
            "Open Users. Classmates are the students of your section; Teachers are the teachers of your courses.",
            "Press “Add friend” beside a person. They get a request and must accept. In Friends you can also find any student or teacher by name.",
            "Friends can write to each other, put each other in groups and share files.",
        ], "users", "Open Users"),
        new("How do groups work?",
        [
            "Open Groups and press “New group”. Give it a name and tick the people to add (classmates, friends, teachers).",
            "Everybody in the group can write on its wall, reply, and see its members and files. Only the person who made it can add or remove people, change it or delete it.",
            "Anybody can leave a group.",
        ], "groups", "Open Groups"),
        new("What is the locker?",
        [
            "Open Resources. The Locker is your own storage for files: notes, projects, reviewers. Only you see them.",
            "Press the Share button of a file to let some people, or a group, open it. “Uploaded files” lists what others shared with you; press the folder button to keep a copy in your locker.",
            "There is a limit for the size of a file and of your locker (the bar shows how much you used).",
        ], "resources", "Open Resources"),
    ]);

    private static HelpSection Teacher() => new("For teachers", "presentation",
    [
        new("Where are my courses?",
        [
            "Open Courses. Each course you teach is a card (or a row, in the list view) with its section. Press Open to see its modules and work.",
            "The administrator gives you your courses (Users Management). If one is missing, ask them.",
        ], "teacher/subjects", "Open Courses"),
        new("How do I make a module and post it to my students?",
        [
            "Open a course and press “Add module”. Write the lesson and add a link to a file or a video if you have one.",
            "A module is a draft until you post it. Choose “post now” or a date and time to post it later. Students see it only after it is posted.",
            "Under a module add activities, quizzes and task performances, each with its points and due date.",
        ]),
        new("What are the drafts that came from the academic provider?",
        [
            "The academic provider (of the central team) can send you the syllabus and modules of a course, with their activities and quizzes. They arrive in your course as drafts with a badge “From the academic provider”.",
            "The provider sees whether you opened what was sent and whether you posted it. Opening a draft to read it or to edit it is enough to count as opened.",
            "They are yours to change: edit the text, move the due dates, then post them when you are ready. The syllabus is always first.",
        ]),
        new("How do I make a quiz?",
        [
            "Add a quiz to a module, then press Questions. Add multiple choice, true or false and short answer questions with their points.",
            "In the quiz settings choose the time limit, whether the questions are shuffled, and the anti-cheat rules (how many times a student may leave the page).",
        ]),
        new("How do I check and grade the work of my students?",
        [
            "Open the activity, quiz or task performance to see who handed in. Press Grade (or Give score) on a student's row, read the answer and save the score with a comment.",
            "Quizzes are graded by the system; a short answer is right when it matches one of the answers you accepted.",
            "“Work to check” on your dashboard lists what waits for you.",
        ]),
        new("How do I take attendance?",
        [
            "Open Attendance, choose the course and the day, and mark each student Present, Late or Absent. Press Save.",
            "The summary under the list shows each student's attendance. Students who miss a lot are sent to the guidance counselor by the system.",
        ], "teacher/attendance", "Open Attendance"),
        new("Where is the gradebook?",
        [
            "Open Gradebook and choose a course. Every student has a row, every activity, quiz and task a column, and the average at the end. Press “Edit grades” on a row to change the scores of that student.",
        ], "teacher/gradebook", "Open Gradebook"),
        new("How do I give a due date with the calendar?",
        [
            "Open Calendar. Activities without a due date are listed at the right: drag one onto a day, or press “Set date”.",
            "Press an activity on the calendar to see who has handed it in.",
        ], "calendar", "Open Calendar"),
        new("How do I find my students, and make friends?",
        [
            "Open Users. Students are the students of your courses (with the course and section they share); Teachers are your colleagues; Friends are the people who accepted your request.",
            "Green dots show who is online. The dashboard also lists your students who are online.",
        ], "users", "Open Users"),
        new("How do I share a file with my students?",
        [
            "Open Resources and add the file to your locker (Add files).",
            "Press Share, and under “My courses” tick the course. Its students see the file under Uploaded files and are notified. You can also share with people and groups.",
        ], "resources", "Open Resources"),
    ]);

    private static HelpSection Admin() => new("For the administrator", "shield-check",
    [
        new("How do I add, change or switch off an account?",
        [
            "Open Users Management and choose the tab of the role (Students, Teachers, Registrars, Guidance, Coordinators, Providers, Administrators).",
            "Press the Add button, or Edit on a row. In Edit you can change the name and email, set a new password, and switch the account on or off.",
            "Students are added by the registrar when enrolling them. A switched-off account can't sign in.",
            "The school has one administrator, one registrar and one guidance counselor; it can have several academic coordinators and academic providers.",
        ], "admin/users", "Open Users Management"),
        new("A teacher needs courses.",
        [
            "Open Users Management, Teachers, and press Courses on the teacher's row. Choose the section and the courses.",
        ]),
        new("Where are the enrolled and the not enrolled students?",
        [
            "On the Dashboard, the Accounts card has Teachers, Students, Enrolled and Not enrolled. Press one to open its list.",
        ], "admin", "Open the Dashboard"),
        new("How do I archive students who graduate?",
        [
            "Open Archive Accounts. Grade 12 students appear under Graduating students. Tick them, write a note (for example Class of 2027) and press Archive.",
            "Their records stay and their sign-in is switched off. You can restore a student from the Archived tab.",
        ], "admin/archive", "Open Archive Accounts"),
        new("How do I read the security log?",
        [
            "Open Security Logs. The tabs filter the lines: Sign-ins, Failed, Accounts and System. You can search by email or address and choose dates.",
            "Download CSV saves the lines you see as a file for a spreadsheet. Old lines are cleared by the system (see System Settings) or by you.",
        ], "admin/security-logs", "Open Security Logs"),
        new("How do I back up and restore the data?",
        [
            "Open Database Backup & Restore and press “Back up now”. The file is saved in the Backups folder and can be downloaded. Keep a copy somewhere safe, outside this computer.",
            "To go back to a backup, press Restore beside it and type RESTORE. A backup of what is there now is made first, so it can be undone.",
            "To use a backup file from another computer, upload it, then restore it from the list.",
            "System Settings can make a backup by itself every day.",
        ], "admin/backups", "Open Backup & Restore"),
        new("How do I close the system for maintenance?",
        [
            "Open System Settings, Maintenance, tick “Close the system for maintenance” and write the message people will see. Save.",
            "Only administrators can sign in while it is on. Untick it and save to open the system again.",
        ], "admin/settings", "Open System Settings"),
    ]);

    private static HelpSection Registrar() => new("For the registrar", "clipboard-check",
    [
        new("How do I enroll a student?",
        [
            "Open Enrollments. Type the 12-digit LRN. If the student has an account it is found and filled in; otherwise their account is made, with a sign-in you can copy and give to them.",
            "Choose the term, the grade level, the track and strand, the schedule and the section (A to E), then the courses. Press Enroll.",
        ], "registrar/enrollments", "Open Enrollments"),
        new("Where do I see the enrolled students?",
        [
            "Open Student's Accounts. Filter by term, strand, schedule or search by name or LRN. Press a student's courses to see them.",
            "You can also see who is enrolled for the next term, and unenroll a student.",
        ], "registrar/student-accounts", "Open Student's Accounts"),
        new("The section I need is not in the list.",
        [
            "Sections A to E exist for each strand and schedule. If one is missing, ask the academic coordinator to make it in Academic setup.",
        ]),
    ]);

    private static HelpSection Coordinator() => new("For the academic coordinator", "clipboard-list",
    [
        new("How do I set up the school year, terms and sections?",
        [
            "Open Academic setup and use the tabs: School years, Terms, Tracks, Strands, Courses and Sections.",
            "The current term is the one students are enrolled in. Make the term first, then the sections.",
        ], "coordinator/academic-setup", "Open Academic setup"),
        new("Who sends the syllabus, modules and activities to the teachers?",
        [
            "The academic provider of the central team. They prepare the course library and send it to the teachers; you do not send course materials.",
            "If a course has no syllabus or a teacher did not get the modules, write to the academic provider (Messages).",
        ]),
        new("How do I evaluate the teachers?",
        [
            "Open Teacher evaluation and make a questionnaire (you can start from the default questions). Choose who it is about and the due date, then send it.",
            "Students answer once for each of their teachers; answers are anonymous. The page shows how many answered and who has not.",
            "Close it early, or let it close on its due date.",
        ], "coordinator/evaluations", "Open Teacher evaluation"),
        new("Where are the results of the evaluation?",
        [
            "Open Analytics. Choose the questionnaire to see the rating of each teacher, the ratings by area, and the comments.",
        ], "coordinator/analytics", "Open Analytics"),
        new("How do I write an announcement?",
        [
            "Open Announcements, write the title and the text, choose who reads it (everybody, the students, the teachers) and, if you want, the last day it is shown.",
        ], "coordinator/announcements", "Open Announcements"),
    ]);

    private static HelpSection Provider() => new("For the academic provider", "send",
    [
        new("How do I send a syllabus, modules and activities to the teachers?",
        [
            "Open Course library and choose a course. Add the syllabus first, then the modules with their activities, quizzes and task performances.",
            "Press “Send to teachers”, choose what to send (the syllabus is the first thing on the list) and which teachers get it.",
            "Each teacher gets their own copy as a draft: they edit the text and the due dates and post it to their students when they are ready.",
        ], "provider/library", "Open Course library"),
        new("How do I know if the teachers opened what I sent?",
        [
            "Open Tracking. Each line is one thing sent to one teacher's course. The state tells where it is: Not opened, Opened (a draft the teacher has seen), Scheduled or Posted.",
            "A teacher counts as having opened it when they read or edit the module, open one of its activities, or post it. Filter by state to see what is waiting.",
        ], "provider/tracking", "Open Tracking"),
        new("How do I know if the students opened it?",
        [
            "Once a teacher posts a module, the Tracking page shows how many students opened it and its activities. Open a line to see each student, with the ones who have not opened it first.",
            "The syllabus counts like a module. An activity, quiz or task performance counts when the student opens its page.",
        ]),
        new("Where do I see one teacher or one student?",
        [
            "Open Users and choose Teachers or Students. A teacher's line shows what they received, opened and posted, and how long they took. A student's line shows how much of what was posted to them they opened.",
            "Press a name to see every thing sent, one by one.",
        ], "provider/users", "Open Users"),
        new("What do the analytics show?",
        [
            "Open Analytics to see how fast the teachers open and post, how many students open, and the numbers by course, by kind (syllabus, module, activity, quiz, task performance), by section and week by week.",
        ], "provider/analytics", "Open Analytics"),
    ]);

    private static HelpSection Guidance() => new("For the guidance counselor", "heart-handshake",
    [
        new("Where do the referrals come from?",
        [
            "The system refers a student to you by itself when they are failing (below 70), close to failing or missing work, or missing class.",
            "Open Referrals to see them. Filter by status: Open, In progress or Resolved.",
        ], "guidance/referrals", "Open Referrals"),
        new("How do I follow up a referral?",
        [
            "Press “Work on it” on a referral. Choose the status, the intervention and write your notes (only you can read them). When it is resolved, say whether the student Improved or had No change.",
        ]),
        new("How do I see all the students at risk?",
        [
            "Open Students at risk and use the tabs: Failing, At risk and Low attendance.",
        ], "guidance/students", "Open Students at risk"),
    ]);
}
