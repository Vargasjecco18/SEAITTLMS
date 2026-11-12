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
            "When you close the browser you are signed out. Sign in again the next time. On a shared computer, press the arrow button to sign out before you leave.",
            "To sign out, press the arrow button at the bottom of the menu, next to your name.",
            "Being away from the keyboard does not sign you out: with the page open you stay signed in until you press the arrow button, or close the browser.",
            $"After {settings.MaxFailedAttempts} wrong passwords in a row the account is locked for {settings.LockoutMinutes} minutes. Wait, or ask the administrator.",
            "An account can be signed in in one place at a time. If someone is signed in with your account, a notice at the top of the login page says so: sign out there first, or wait a few minutes after that page was closed.",
        ]),
        new("I forgot my password.",
        [
            "Ask the administrator of the school. An administrator can set a new password for you (Users Management, Edit).",
            "Then sign in with the new password.",
        ]),
        new("What is the menu on the left?",
        [
            "It lists the pages for your role. Rest the pointer on a page and a small box opens beside it showing what is inside, with links to its parts (for example, Courses shows your courses, and a click opens one).",
            "On a phone, the menu button at the top left opens it. On some pages (the academic setup, the enrollment, the course library) the menu folds to its icons by itself so the page has more room: the button at the top of the menu, “Hide Nav bar”, shows the names again.",
        ]),
        new("Where is the calendar?",
        [
            "Open Calendar in the menu (every role has it). It shows the days of the month or the week with what is due, the announcements from their start to their end, your own events and the holidays of the Philippines (a rose outline). The staff (the registrar, the guidance counselor and the academic provider) see announcements and holidays at the right of the calendar; the work of a course (assignments, quizzes and so on) is for the students and the teachers.",
            "Double click a day to add an event on it. In the week view press the day to see everything on it. A teacher also has the button “Announcement to my class”. The academic coordinator writes announcements and manages the holidays in Announcements, so the coordinator has no Calendar.",
        ], "calendar", "Open Calendar"),
        new("How do I change the look, or my picture?",
        [
            "Press the gear at the top right (Settings).",
            "Under Profile picture, press “Upload a picture” and choose a photo. It is made small for you. “Remove” brings back your initials.",
            "Under Appearance choose Light, Dark, Midnight, Ocean, Forest, Sunset, Paper, Auto (Auto follows your computer) or Customize: Customize shows four color boxes (the page, the cards, the text and the accent) and works out the other colors from them; Reset the colors brings the first ones back. You can also switch the animations off.",
        ]),
        new("What does the Save button of Settings keep?",
        [
            "The picture, and the look of the pages (Light, Dark, Auto or one of the five colors, the animations and the sound switch), are kept as soon as you choose them.",
            "The notification sound and the password are kept with the Save button at the bottom of Settings. Choose a sound (it plays so you can hear it), or fill in the password boxes, then press Save. A new password signs you out, and you log in again with it.",
        ]),
        new("How do messages work?",
        [
            "Press the speech bubble at the top. The list shows your conversations; type a name in the box at the top of the list to find a person you may write to.",
            "You can write to a person at any hour. Press the paper clip to send a file with the message.",
            "A number on the bubble tells how many messages you have not read.",
        ]),
        new("What do the notifications say?",
        [
            "The bell at the top lists what happened for you: a new handout, a grade, a deadline coming, a friend request, a file someone shared. A new one also plays a short sound. In Settings (the gear at the top) you can choose the sound, add your own sound file, or switch it off. The browser lets a page make sound only after you clicked on it once, so click anywhere on the page if you hear nothing.",
            "Press one to go to the page it is about. “Mark all as read” clears the number.",
        ]),
    ]);

    private static HelpSection Student() => new("For students", "graduation-cap",
    [
        new("Where are my courses, and how far along am I?",
        [
            "Open Courses in the menu. Enrolled shows this term; Completed shows the courses of terms that are over.",
            "Each course has a progress bar. It counts the handouts you opened, out of the handouts your teacher posted.",
            "Switch between cards and a list with the buttons at the top right of the page. The system remembers the one you chose.",
        ], "student/subjects", "Open Courses"),
        new("How do I open a handout and do the assignments?",
        [
            "Open a course, then a handout. The syllabus of the course is the first thing in the list.",
            "Under a handout you find its assignments, quizzes and task performances. Open one, read the instructions and send your answer before the due date.",
            "A handout appears when your teacher posts it. The ones you have not opened yet show on your dashboard.",
            "When you open a course from Courses there is the Course Menu at the left: Courses (your courses, to go from one to another), Handouts, Activities (Seatwork, Assignments, Quizzes and Task performances, and Exams when your teacher gave one), then Classmates, Teachers (with a Message button to write to the teacher) and Grades. It stays in view while you scroll, and the button at its top hides it or shows it again.",
            "The “Progress” at the top of a handout is the same number as the bar of its row in the course: opening the handout is 50 and the slides you saw are the other 50 (a handout with no slides is 100 as soon as you open it). On a phone the slides open with the Slides button, in the whole screen. In the course, the Slides and Handout buttons are at the right of each row.",
            "Each row of the course (the syllabus and the handouts) has its progress bar, at the left of what the handout has (its activities). A handout with slides is worth 50 for opening the handout and 50 for the slides you saw (the Slides button opens them first, one slide at a time, from left to right: the arrows, the keys of the keyboard or the dots). A handout with no slides is 100 as soon as you open it. The Slides and the Handout buttons are in the middle of the row. At the right of the lesson are the activities of the handout, each one a link with how you did; the button above them hides them. A reference link of a handout is at the left of “Open in a new tab”.",
            "On an activity the Course Menu is at the left too, then the instructions, and your answer with its Details and the Feedback of your teacher under it.",
            "When you read a lesson, or read it again, the main menu folds to its icons to give the lesson more room. The button at the top of the icons shows the names again.",
        ]),
        new("What if I hand in after the due date?",
        [
            "You can still hand in an assignment, but it is marked late and your teacher decides if it is accepted. You are told. Work that is not accepted is not graded and can't be handed in again.",
        ]),
        new("How does a quiz work?",
        [
            "Read the time limit and the rules before you start. The timer keeps running even if you close the page.",
            "Some quizzes watch if you leave the page, copy, paste, print or take a screenshot. Each one is a warning, and too many warnings end the quiz and send what you have. Your name is shown faintly over the quiz.",
            "A quiz can have several kinds of question: choose one answer, tick several, true or false, type a short answer, type the word of an identification, fill in the blanks of a sentence, match the items of Column A with Column B, or write an essay. Some questions have a picture. An essay is scored by your teacher, so the quiz waits for the score after you hand it in.",
            "When you send it, the result is shown if your teacher allowed it.",
        ]),
        new("Where are my grades?",
        [
            "Open Grades. Every course is a card with your progress at the top (the bar has the passing mark drawn on it) and, under it, the gradebook of the course with your own row: a column for each activity (A assignment, Q quiz, TP task performance, S seatwork, SE summative exam), your Average and the Final grade of your teacher. It is only to read.",
            "Open Grades in the menu: your grade in every course and how it is going. A course below 70 is marked.",
            "The dashboard shows your recent grades and your average.",
            "If your teacher hides the grades of a course, the card says “Hidden by your teacher” and shows no scores until your teacher shows them again.",
        ], "student/grades", "Open Grades"),
        new("Where are the details and the feedback of an activity?",
        [
            "Open an assignment, a quiz or a task. In the header, before “Back to the handout”, there are two buttons: Details (the status, the due date, the highest score and your score) and Feedback (what your teacher wrote about your work; a red dot means there is feedback). Each one opens a small box right under its button.",
        ]),
        new("How do I see my deadlines?",
        [
            "Open Calendar. Switch between month and week. A dot on a day is something due.",
            "Press an assignment to go to it. The colors tell the kinds apart: the summative exams are teal, an announcement is gray (it is on every day from its Start to its End), a holiday of the Philippines has a rose outline and your own events are violet.",
            "Double click a day to add an event on it.",
        ], "calendar", "Open Calendar"),
        new("How do I evaluate a teacher?",
        [
            "When the academic coordinator sends a questionnaire, a card appears on your dashboard and you get a notification.",
            "Answer one questionnaire for each of your teachers before the due date. Your answers are anonymous: the teacher and the coordinator see the ratings, not who gave them.",
        ], "student/evaluations", "Open the evaluations"),
        new("How do I add friends and see my classmates?",
        [
            "Open Users. The tabs are Friends, Classmates, Students, Teachers and Staff / Non-teaching. Students and Teachers are tables with the name, the grade level, the strand, the section, a Message button and the friend status; Staff / Non-teaching has the name, the role, and at the right the Message button and the friend status. The Message button is there for everybody, friends or not.",
            "Press “Add friend” beside a person (a student, a teacher or a member of the staff). They get a request and must accept. In Friends you can also find any student or teacher by name.",
            "Friends can write to each other, put each other in groups and share files.",
        ], "users", "Open Users"),
        new("How do groups work?",
        [
            "Open Groups. Some groups are made for you by the system: Grade 11 students (or your grade level), the students of your course (strand) and All students. You are in them automatically and cannot leave them.",
            "Press “Add a group” to make your own: give it a name and tick the people to add (classmates, friends, teachers).",
            "Everybody in a group can write on its wall, reply, and see its members and files. Only the person who made a group can add or remove people, change it or delete it. Anybody can leave a group they were added to.",
        ], "groups", "Open Groups"),
        new("What is the locker?",
        [
            "Open Resources. The Locker is your own storage for files: notes, projects, reviewers. Only you see them.",
            "Press the Share button of a file to let some people, or a group, open it. “Uploaded files” lists the files you sent to other people (who got each one, and when). “Received files” lists what others shared with you; press the folder button to keep a copy in your locker.",
            "There is a limit for the size of a file and of your locker (the bar shows how much you used).",
        ], "resources", "Open Resources"),
    ]);

    private static HelpSection Teacher() => new("For teachers", "presentation",
    [
        new("Where are my courses?",
        [
            "Open Courses. Each course you teach is a card (or a row, in the list view) with its section. Press Open to see its handouts and assignments.",
            "The administrator gives you your courses (Users Management). If one is missing, ask them.",
        ], "teacher/subjects", "Open Courses"),
        new("How do I make a handout and post it to my students?",
        [
            "Open a course and press “Add handout”. Write the lesson and add a link to a file or a video if you have one. You can also attach slides (a PowerPoint file or a PDF) apart from the other files. Many handouts at once: press “Add many handouts in a sheet” in the same box: a row for each handout with its Title, its Slides (optional), its Link and its Files. There is no Excel file to download.",
            "Press Open on a handout, or a PDF of it, to read it inside the system, like the students do: the course menu is at the left and Edit handout at the right.",
            "A handout is a draft until you post it. In its row of the course, press the date (it says Hidden from students, Posted Oct 6, 2026 or Scheduled for Nov 10, 2026, 8:00 AM): a small box has the time it appears. Choose a time and press Save time (a later time schedules it, an earlier one posts it from that date), or press Post now. The Hide from students button of the row makes it a draft again. Students see a handout only after it is posted. The bar of each row is how far the class is: 50 for the slides and 50 for the handout (100 for the handout when it has no slides), the average of the class.",
            "Press “Add activities” at the top right of the course. It offers “Add activities” and “Add quiz”. “Add activities” opens a sheet inside the system: a row for each activity, with its Handout and its Type (lists to choose from), the Title, the Instructions, the Points, the Due date and the Files; fill the rows you want and press Save (the sheet fits the box: no scroll bars). “Add quiz” opens a sheet of questions: choose the Handout of the quiz and write its Title, then write a row for each question (its type and its right answer are chosen from lists) and press Save: the quiz is made with its questions. Set its due date, its time limit and the anti-cheat rules afterwards with Edit on the quiz. To add one at a time, open Activities in the Course Menu, then a kind (for example Quizzes) and press its Add button.",
        ]),
        new("What is the menu inside a course?",
        [
            "When you open a course from Courses there is the Course Menu at the left: Courses (your courses, to go from one to another), Handouts, Activities (Seatwork, Assignments, Quizzes and Task performances, each one listing that kind of work for the whole course), then Students (each with a Message button), Gradebook and Attendance. The first page of the course lists the syllabus and the handouts: each row shows how far the class has read the slides (the average of the class), with the Slides and the Handout buttons.",
            "The menu stays in view while you scroll. The button at its top hides it, to give the page more room, and shows it again; the system remembers your choice.",
            "Only Courses and Handouts have a “See all” line; every kind of activity ends with an “Add seatwork” (or “Add assignment” ...) line, also when it already has activities. Inside an activity the Course Menu is at the left of the page, and a double click on the title of the activity shows what you gave (the instructions and the files). There are no exams in your menu: the summative exams come from the academic provider.",
        ]),
        new("Can I add many activities at once?",
        [
            "Yes. Press “Add activities” (or “Many in a sheet” in an Add menu): a sheet opens in the middle of the screen, with the step by step at the right. Each row is one seatwork, assignment or task performance (a quiz is added with “Add quiz”). Choose its handout and its type from the lists, type the title, the instructions and the points, choose the due date and add the files if there are any.",
            "To add more rows choose the type (Seatwork, Assignment or Task performance) and the number at the bottom, then press Add (“Add [type] [number] rows”), like the questions of a summative exam. The Ready column says what is missing in a row. A row with nothing in it is ignored, and a row with a problem is left out. Press Save: every row becomes an activity of its handout.",
            "The questions of a quiz are written in a sheet like the one of a summative exam, in a wide box with tall rows: “Add quiz” opens it for a new quiz, and “Questions” on a quiz that is there opens it again. Write a row for each question (its type and its right answer are chosen from lists), and use “Add [type] [number] rows” to add many rows of one type. There is no Excel for the questions any more.",
        ]),
        new("A student handed in after the due date. What do I do?",
        [
            "Open the assignment. A late row says “Late · accept?”. Press Accept to give it a score, or Decline. The student is told. Late work can be graded only after you accepted it.",
        ]),
        new("What are the drafts that came from the academic provider?",
        [
            "The academic provider (of the central team) can send you the syllabus and handouts of a course, with their assignments and quizzes. They arrive in your course hidden from your students: they see them only after you post them.",
            "The provider sees whether you opened what was sent and whether you posted it. Opening a draft to read it or to edit it is enough to count as opened.",
            "They are yours to change: edit the text, move the due dates, then post them when you are ready. The syllabus is always first.",
        ]),
        new("How do I make a quiz?",
        [
            "Press “Add activities”, then “Add quiz”: choose the handout, write the title and the questions. Open a quiz that is there and press Questions to change them. Add questions of eight kinds (multiple choice, true or false, short answer, identification, fill in the blank, matching type, multiple select and essay) with their points: double click a question (or press the box button at the end of its row) to open it in a box where you can add a picture to the question and to each choice, write more choices, the blanks, and the two columns of a matching type. An essay is scored by you: the quiz waits for your score. A quiz that has a file has no questions: the students answer with a file and you give the score.",
            "In the quiz settings choose the time limit, whether the questions are shuffled, and the anti-cheat rules (how many times a student may leave the page, copy, print or take a screenshot). A photo taken with another device can't be detected, but the student's name is shown faintly over the quiz.",
        ]),
        new("How do I check and grade the assignments of my students?",
        [
            "Open the assignment, quiz or task performance to see who handed in. In the Answers column one button (“View answer”, or “View answer and 2 files”) opens the work of the student; pressing the row does the same. A box opens with the students who handed in at the left, the answer in the middle, with a preview of the files, and the score and the feedback at the right.",
            "A seatwork, an assignment or a task performance that was handed in after the due date shows a warning with how late it was (“Late by 2 days”), and the course page shows “n late” on that activity. Accept the late work to grade it, or decline it.",
            "Quizzes are graded by the system; a short answer is right when it matches one of the answers you accepted.",
            "“Activities to check” and “Unchecked activities (including quiz)” on your dashboard list what waits for you.",
        ]),
        new("How do I take attendance?",
        [
            "Open Attendance, choose the course and the day, and mark each student Present, Late or Absent. Press Save. Classes are held from Monday to Friday: Saturday and Sunday can't be marked (if you pick one, the Friday before is shown).",
            "Press “View attendance” to see each student's attendance. Students who miss a lot are sent to the guidance counselor by the system.",
        ], "teacher/attendance", "Open Attendance"),
        new("Where is the gradebook?",
        [
            "Open Gradebook and choose the course in the list at the top left of the card. Every student has a row, every assignment, quiz and task a column, and the average at the end. Scores are saved by themselves when you leave a box. The last column, Final grade, starts with the average of the student as soon as there is one; change it if you like, then press “Send final grades” to give it to the students. At the top right: “Add Others” adds a column for extra scores (a recitation, an exam or others; choose the handout), “Hide from students” (it becomes “Show to students”) hides the grades of the course from the students, “Export to Excel” makes one file with all your courses, a sheet for each, that shows how the grades are worked out, and “Send final grades” sends the last column to the students. The two arrows under the name of a column move it to the left or to the right (the file follows the same order).",
        ], "teacher/gradebook", "Open Gradebook"),
        new("How do I give a due date with the calendar?",
        [
            "Open Calendar. Assignments without a due date are listed at the right: drag one onto a day, or press “Set date”.",
            "Press an assignment on the calendar to see who has handed it in. Double click a day to add an event on it.",
        ], "calendar", "Open Calendar"),
        new("How do I find my students, and make friends?",
        [
            "Open Users. Students are the students of your courses (with the course and section they share); Teachers are all the other teachers of the school; Staff / Non-teaching are the registrar, the guidance counselor, the academic coordinator and the academic provider; Friends are the people who accepted your request.",
            "Green dots show who is online. The dashboard also lists your students who are online.",
            "Open Groups for the groups made for you: the teachers of each grade level you teach in, All teachers and Teachers and staff.",
        ], "users", "Open Users"),
        new("How do I write to my class?",
        [
            "Open Calendar and press “Announcement to my class”, or double click a day and choose Announcement. Choose the class (one of your courses), what it is about (an exam or other), and write it. Only the students of that class see it, and they are told with a notification.",
            "“From the school” lists the notices of the academic coordinator for the teachers: exams, holidays, the semester break and teachers' meetings.",
        ], "calendar", "Open Calendar"),
        new("How do I share a file with my students?",
        [
            "Open Resources and add the file to your locker (Add files).",
            "Press Share, and under “My courses” tick the course. Its students see the file under Received files and are notified. The files you shared are listed under Uploaded files. You can also share with people and groups.",
        ], "resources", "Open Resources"),
    ]);

    private static HelpSection Admin() => new("For the administrator", "shield-check",
    [
        new("How do I add, change or switch off an account?",
        [
            "Open Users Management and choose the tab: Students, Teachers or Staff. The sidebar folds to give the table room. A green dot on a person's picture means the person is online now. The status of an account is Activated, Deactivated or Archived (a student who dropped out or graduated).",
            "Press the Add button (Add student, Add teacher or Add staff), or Edit on a row. In Edit you can change the name and email, set a new password, and activate or deactivate the account. “Sign-in details” on a row shows the email and, while the person has not changed it, the first password.",
            "In the names only letters can be typed, and when you press Tab the first letter of each word becomes a capital. The suffix is a list (Jr., Sr., I, II, III, IV, V and Others, where you type another one). The email and the password are made by the system and shown before you press Add. The employee number of a teacher starts with T- by itself; the one of a member of the staff starts with the first letter of the role.",
            "A teacher who works in more than one section has a “sections” button: press it to see them all. A deactivated account can't sign in.",
        ], "admin/users", "Open Users Management"),
        new("How do I add a staff member, or make a new staff role like Librarian?",
        [
            "Open Users Management, the Staff tab, and press Add staff. In “Name of the new role” type the role (the usual ones are in the list: Registrar, Guidance counselor, Academic coordinator, Academic provider, and the roles you made before).",
            "A name that is new makes a new role: the pages the people of the role can open appear as checkboxes, one by one, in groups: the pages every account has (Calendar, Users, Groups), the different analytics (school, guidance, library, evaluation) and the pages of the registrar (Enrollment, Student's Accounts), the guidance counselor (Referrals), the academic coordinator (Academic setup, Class sections, Teacher evaluation, Announcements) and the academic provider (Course library, Tracking), each with its dashboard. The pages of the administrator can't be ticked. Tick the ones the role needs. The people of the role get their own dashboard with those pages and nothing else, and what they do in a ticked page is saved as the registrar, the guidance counselor and so on would.",
            "Many people can have a role you made: choose it in the list the next time.",
            "Only the shared pages can be ticked (the pages of the registrar, the coordinator and the others work with their own data, so they stay with the real roles).",
        ], "admin/users/staff", "Open the staff list"),
        new("A teacher needs courses.",
        [
            "Open Users Management, Teachers, and press Courses on the teacher's row. Choose the section and the courses.",
        ]),
        new("Where are the enrolled and the not enrolled students?",
        [
            "On the Dashboard, the Accounts card has Teachers, Students, Enrolled and Not enrolled. Press one to open its list.",
        ], "admin", "Open the Dashboard"),
        new("How do I see who uses the LMS?",
        [
            "Open Analytics. The green, yellow and red cards count the students: Active (they visit the LMS, view handouts and hand in activities often: a score of 70 or more in the last 14 days), Inactive (less than that) and Never logged in (red). The pie shows them. “Performance by courses” has a Strand list: choose the strand and its courses show with their students, their average grade and how many are failing; press a course to see its students (grade, status and attendance) in a box. The Excel file has a sheet “LMS activity” with every student.",
        ], "analytics", "Open Analytics"),
        new("How do I archive students who graduate, and see their grades?",
        [
            "Open Archive Accounts. Grade 12 students appear under Graduating students. Tick them, write a note (for example Class of 2027) and press Archive.",
            "Their sign-in is switched off. On the Archived tab, “Grades” shows every grade of the student from the first term (where they started, a shift to another strand, and graduation), “Export” saves them in Excel, and “Export all grades” saves everybody in one table.",
            "An archived account is deleted by itself 30 days after it was archived: the column “Deleted in” counts the days. Export the grades before that. “Restore” takes a student out of the archive.",
        ], "admin/archive", "Open Archive Accounts"),
        new("How do I read the security log?",
        [
            "Open Security Logs. The tabs filter the lines: Last login (the day each person last logged in), Failed, Accounts and System (the lines of the system: backups and settings, with no “Who” column). You can search by email or address and choose dates.",
            "Download CSV saves the lines you see as a file for a spreadsheet. Old lines are cleared by the system (see System Settings) or by you.",
        ], "admin/security-logs", "Open Security Logs"),
        new("How do I back up and restore the data?",
        [
            "Open Database Backup and press “Back up now”. The file is saved in the Backups folder and can be downloaded. Keep a copy somewhere safe, outside this computer.",
            "To go back to a backup, press Restore beside it and type RESTORE. A backup of what is there now is made first, so it can be undone.",
            "A downloaded backup is a plain SQL file that MySQL can import: in MySQL Workbench open it (File > Open SQL Script), double-click the schema so it is bold and press the lightning button, or in a terminal: mysql -u root -p seaittlms < backup.sql. It replaces the tables that have the same names.",
            "To use a backup file from another computer, upload it, then restore it from the list. System Settings can make a backup by itself every day.",
        ], "admin/backups", "Open Database Backup"),
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
            "Open Enrollment. Type the 12-digit LRN (digits only, like the phone number) and the names (letters only). If the student has an account it is found and filled in; otherwise their account is made, with a sign-in you can copy and give to them.",
            "Many students at once: press “Import from Excel”, download the template, fill one row for each student (LRN, names, grade level, strand, section, courses) and upload it. A row that is wrong is left out and the file with the result tells why.",
            "Choose the grade level, the track and strand and the section (if the section is not in the list, choose “+ Add a section…” and type its name). The courses of the strand show under it. The sign-in details are under the student card, at the left, and stay where they are when the courses appear. Press “Print Enrollment” to print the form.",
            "If something is missing when you press “Enroll student”, each box with a problem turns red with a message under it, a red alert at the top lists them, and the cursor goes to the first one. Fix the box and its message goes away.",
        ], "registrar/enrollments", "Open Enrollment"),
        new("Where do I see the enrolled students?",
        [
            "Open Student's Accounts (the sidebar folds). Filter by term, strand or next term, or search by name or LRN. Each row has Account (the email and, while the student has not changed it, the first password), Edit (the enrollment) and Drop out. The grade is a button, “Overall grade”: it opens every course of the student with its final grade, term by term (the earlier terms stay there). Each student has one line only, even after being enrolled again. Students who graduated or were archived are not in this list or in the numbers of the registrar.",
            "The last column is about the next term: “Enrolled” with the term (and Unenroll) when the student is enrolled for it, otherwise one button, “Re-enroll for 2nd Term” (or 1st, or 3rd Term). The button is off until the current term has ended; when it works, it opens a box with the grade level, the track and the strand filled in, and you choose the section.",
            "“View Enrolled Student Today” (top right) lists the students enrolled today, the newest first, with their LRN, grade level, strand, section and term.",
        ], "registrar/student-accounts", "Open Student's Accounts"),
        new("The section I need is not in the list.",
        [
            "Sections A, B and C exist for each strand, for Grade 11 and Grade 12. If one is missing, open the Section list in Enrollment and choose “+ Add a section…”: type its name and it is added.",
        ]),
        new("How do I find people and make a group?",
        [
            "Open Users: Students and Teachers are everybody of the school (a teacher also has Handled students, the students of their own courses; a student also has Course teachers, the teachers of their courses), Message and Friend status are at the right of each row, and Staff / Non-teaching are the registrar, the guidance counselor, the academic coordinator and the academic provider. A green dot means the person is online now. The button with the speech bubble writes to the person (it is only there when your role may write to them).",
            "Open Groups and press Add a group: give it a name and choose the people to add. Everybody in the group can write in it. Two groups are made for you: All staff (the registrar, guidance counselor, academic coordinator and academic provider) and Teachers and staff.",
        ], "users", "Open Users"),
    ]);

    private static HelpSection Coordinator() => new("For the academic coordinator", "clipboard-list",
    [
        new("How do I set up the school years, terms, tracks, strands and courses?",
        [
            "Open Academic setup. The parts are the tabs at the left: School years, Tracks, strands and courses and Class sections; a tab opens a dropdown with what is inside (the school years, the tracks and strands, the grade levels) to go straight to one. The menu of the school folds by itself on this page, and the button above the tabs (Hide tabs) hides only the tabs. The search box at the top searches the tab that is open.",
            "School years keep themselves: the school year that is running and the next two are always there, each with three terms. When a school year is over it goes away by itself (its students, grades and attendance stay). Every box is ready to type in: change a name or a day and leave the box, and it is saved. There is no Edit button. The bin of a term deletes it, after asking (not a term that has students enrolled); a school year with fewer than three terms has an “Add a term” button.",
            "Tracks, strands and courses are three cards side by side, each with its columns (tracks: Code, Name, Strands, Offered, Delete; strands: Code, Name, Courses, Offered, Delete): press a track to see its strands, press a strand to see its courses. Change a name, the code or the unit in place, or use the row at the bottom of a card to add one. A course has the columns Code, Course, Unit, Offered and Delete: uncheck Offered and the registrar can't enroll students in it; Delete takes it out of the strand, after asking. Each card has its own search box. The button above the tabs hides them, so the cards have more room.",
            "To fill many rows at once press the Excel button at the top and choose “Template and import (Excel)”: the workbook has a sheet for each step. “Export to Excel” gives the whole setup in the same workbook: change it and import it again. A row that already exists is changed, nothing is deleted.",
        ], "coordinator/academic-setup", "Open Academic setup"),
        new("Where are the class sections?",
        [
            "Class sections is a tab of the Academic setup. The tab opens a list of the levels: Grade level (11 and 12), inside it the Strands and inside each strand the Sections A, B and C; press a grade level to see all its strands and sections. In the page there is a tab for each class too, and inside each strand the sections are cards. Press a card, or a letter A, B or C under a strand in the tab, to choose its adviser (only a teacher of that strand) and to see the students in it.",
            "A card that says “Add section” makes the section that is missing (A, B and C). The bin on a card deletes the section, after asking.",
        ], "coordinator/academic-setup/sections", "Open Class sections"),
                new("Who sends the syllabus, handouts and assignments to the teachers?",
        [
            "The academic provider of the central team. They prepare the course library and send it to the teachers; you do not send course materials.",
            "If a course has no syllabus or a teacher did not get the handouts, write to the academic provider (Messages).",
        ]),
        new("How do I evaluate the teachers?",
        [
            "Open Teacher evaluation and make a questionnaire (you can start from the default questions). Add a question by choosing how the students answer: a 5-point Likert scale, a 5-point performance scale, a frequency scale, a binary (Yes/No) scale, a ranking, select-all-that-apply, or a comment. Choose who it is about and when it is due, then send it.",
            "Students answer once for each of their teachers; answers are anonymous. In an evaluation the Questions are first and the Details after them; “Answers by course” shows how many answered and who has not. “Message” writes to the students who have not answered in one course, and “Message all not answered” (at the right of the header of the card) writes once to every student who has not answered in any course.",
            "Close it early, or let it close on its due date.",
            "New evaluation opens a sheet like the one of a quiz: write the title and the instructions, then a row for each question. Choose its type from the list (5-Point Likert Scale, 5-Point Performance Scale, Frequency Scale, Binary Scale, Ranking Scale, Select-All-That-Apply or Comment), write its area and, for a ranking or a select-all, the options.",
        ], "coordinator/evaluations", "Open Teacher evaluation"),
        new("Where are the results of the evaluation?",
        [
            "Open Analytics. Choose the questionnaire to see the rating of each teacher, the ratings by area, the Yes/No, ranking and select-all results, and the comments. “Export to Excel” puts everything of the page in one workbook, one sheet for each part, with the result of every teacher.",
        ], "coordinator/analytics", "Open Analytics"),
        new("How do I write an announcement?",
        [
            "Open Announcements and press “New announcement”. Choose what it is about (an exam, a holiday, the semester break, a teachers' meeting or other), write the title and the text, choose who reads it (everybody: the staff, the teachers and the students; the students; the teachers; the staff; or the staff and the teachers; a teachers' meeting is for the teachers) and, if you want, its Start and its End (the days it is about). It is shown until its End, and the people it is for see every day from Start to End in their calendar.",
            "To write many announcements at once press “Announcements sheet”: each row has a Title, a Message, what it is About, who it is For, and a Start and an End day. The Ready column tells you what is missing; press Post and every ready row is posted. “Export to Excel” saves the announcements you have as a file.",
            "The Philippine holidays are on the calendar of the announcements. Press one to see it and to delete it: it is deleted from the calendars of all the years (under “Deleted holidays” you can bring it back).",
        ], "coordinator/announcements", "Open Announcements"),
        new("How do I find people and make a group?",
        [
            "Open Users: Students and Teachers are everybody of the school, and Staff / Non-teaching are the registrar, the guidance counselor, the academic coordinator and the academic provider. A green dot means the person is online now. The button with the speech bubble writes to the person (it is only there when your role may write to them).",
            "Open Groups and press Add a group: give it a name and choose the people to add. Everybody in the group can write in it. Two groups are made for you: All staff (the registrar, guidance counselor, academic coordinator and academic provider) and Teachers and staff.",
        ], "users", "Open Users"),
    ]);

    private static HelpSection Provider() => new("For the academic provider", "send",
    [
        new("How do I send a syllabus and handouts to the teachers?",
        [
            "Open Course library: the courses of the current term are cards, like the Courses of the teachers and the students. Use the search box and the lists Track, Grade level, Strand and Course to find one, then press Open (a course opens with a Course Menu at the left, where the Courses dropdown goes from track to strand to course without going back; the previous terms are hidden, because the library works on the current term). Add the syllabus first, then the handouts (“Add many handouts in a sheet” in the same box opens a sheet inside the system, in the middle of the screen: a row for each handout with its title, its slides (optional), its link (optional) and its files (needed), and the step by step at the right; no Excel is needed. The teachers and the students get a Slides button when a handout has slides). Press a syllabus or a handout to open it and see its text and files.",
            "You send the syllabus and the handouts to the teachers (they arrive hidden from the students; the teacher posts them) and you post the summative exams straight to the students. The teacher makes the seatwork, assignments, quizzes and task performances in their own copy of the course.",
            "You can attach files (a PDF, a picture, a text file ...) to the syllabus, a handout and a summative exam. There is no limit in MB for them. The path at the top (Library › Academic › the strand › the term) takes you back one step at a time.",
            "Press “Send to teachers”, choose what to send (the syllabus is the first thing on the list) and which teachers get it.",
            "Each teacher gets their own copy as a draft, with the files: they edit the text and the dates and post it to their students when they are ready. A PDF or a picture opens in a box in the middle of the screen, where it can be downloaded.",
        ], "provider/library", "Open Course library"),
        new("How do I make a summative exam?",
        [
            "Open Course library, choose a course and press “Add summative exam” (or the same button in the card Summative exams).",
            "Write the title and the instructions, then press “Add questions” (it is under the instructions, and the highest score is under the questions): a sheet opens where each row is a question, with its type and its right answer chosen from lists; “Add [type] [number] rows” adds many rows of one type. The settings and when it opens and closes are not asked here: you choose them when you post it. The academic coordinator gets a notification and an announcement.",
            "In a course the buttons are in groups: Add handout and Send to teachers | Add summative exam. The library also has a list view (a table with a line for each course). To make many exams at once press “Excel” in the card: download the template, fill it in and import it, like the handouts. “Export” gives the exams of the course in the same file. Press a syllabus or a handout of the course to open it in one box: the other handouts are at the left, the preview of the files in the center and the upload form at the right (“Upload” saves the files and the center shows them).",
            "Press “Post summative exam to students” at the right of the lists of the library to post the exam of every course in the list (choose a course in All courses to post only that one), (there is no Post to students inside a course). Choose the Summative Exam Settings (time limit, anti-cheating watch, order, result) and when it opens and closes: the exam goes straight to the students and the system gives the scores. A course with no summative exam stops it: the page goes to that course and tells you. The teacher sees the scores in the gradebook.",
        ], "provider/library", "Open Course library"),
        new("Who has not posted what I sent?",
        [
            "Open Tracking. It has three parts: Syllabus, Handouts and Summative exams. In the first two the buttons are Not posted, Scheduled and Not opened. “Not posted” shows the teachers who received a syllabus or a handout and have not posted it to their students yet; “Scheduled” shows what a teacher has scheduled for later, with the time it will appear; you can write to one teacher (or all of them) when it is scheduled too far away.",
            "Each line has the syllabus or handout, the teacher and the section. The search box and the grade and course boxes narrow the list. A handout that is scheduled to post later is not late: it is on “Scheduled” and is posted when its time comes.",
        ], "provider/tracking", "Open Tracking"),
        new("Who has not opened it?",
        [
            "Choose “Not opened” in Syllabus or Handouts. A student is on this list only after the teacher posted it: until then the teacher is the one on “Not posted”. Once the student opens it, the student leaves the list.",
            "In Summative exams the buttons are Not opened, Not handed in and Handed in. They list the students of the exams that are posted: an exam that is not posted yet has no students here. “Handed in” shows the scores.",
        ]),
        new("Can I remind the people on the list?",
        [
            "Yes. The button “Message” at the top right of the list tells everybody who is on it, and says how many people that is (in “Scheduled” and “Not posted” you can choose one teacher). The words are filled in for you and you can change them. The button is off when the list is empty.",
        ]),
        new("What do the analytics show?",
        [
            "Open Analytics to see how fast the teachers open and post, how many students open, week by week, the teachers who have not opened what you sent, what the students open least, and the numbers by section and by course. “Export to Excel” puts everything of the page in one workbook: the numbers of the page, every teacher, every student and the result of each student in each course.",
        ], "provider/analytics", "Open Analytics"),
        new("Why does the library say “Something went wrong” when I choose All terms?",
        [
            "It does not any more: the Course library lists every course of every term when you choose All terms in the Term list (the page used to stop when it had more than a hundred buttons).",
        ]),
        new("How do I find people and make a group?",
        [
            "Open Users: Students and Teachers are everybody of the school, and Staff / Non-teaching are the registrar, the guidance counselor, the academic coordinator and the academic provider. A green dot means the person is online now. The button with the speech bubble writes to the person (it is only there when your role may write to them).",
            "Open Groups and press Add a group: give it a name and choose the people to add. Everybody in the group can write in it. Two groups are made for you: All staff (the registrar, guidance counselor, academic coordinator and academic provider) and Teachers and staff.",
        ], "users", "Open Users"),
    ]);

    private static HelpSection Guidance() => new("For the guidance counselor", "heart-handshake",
    [
        new("What do I look after?",
        [
            "You look after two things only: attendance and violations. Failing grades and students at risk in a course are for the teacher.",
            "Open Referrals. Every student of the school is in the list, one line each, so you can write a concern for anybody. Each line has the student, the section, the Concern, what happened (with View attendance for an attendance concern), the Notes and a Solved button. Use the search box and the choices Show and Concern to find students.",
        ], "guidance/referrals", "Open Referrals"),
        new("Where do the attendance concerns come from?",
        [
            "The system finds them by itself: a student whose attendance is under 80% over at least 5 marked days. You get a notification, and the student shows with Attendance in the Concern box.",
            "To see only the students who have a concern choose “Only students with a concern” in Show, or use “With concerns” or “Attendance” under Referrals in the menu.",
        ], "guidance/referrals?show=concerns", "Open the students with a concern"),
        new("How do I write a violation or follow up a concern?",
        [
            "On the row of the student choose the Concern (Attendance or Violation) and then choose what happened from the list: the concern is recorded and the Concern list is locked (press Solved to change it). If it is not in the list choose “Others”: a box opens in the middle of the screen to type what happened. The name of the student opens a box with the earlier concerns and a place for more words only you can read.",
            "Press the Notes button of the row (it says Noted when there are notes and None when there are not) to choose the help you gave (“Others” opens a box to type it) and write your notes. Saving them tells the student (no message pops up at the top for you: the row just changes). When the case is over press Solved. For an attendance concern press “View attendance” to see each course in a box in the middle of the screen.",
        ]),
        new("What does the student answer, and how do I set a schedule?",
        [
            "The Status column (at the right of Notes) says “Not seen yet” until the student presses OK on your message, then “Seen”. If the student closes the box with the X (or Escape) without answering, it says “Ignored”. When you save notes again the student gets the message again, with the buttons Going and Not going, and the status starts over.",
            "The Schedule column (at the right of Status) shows the answer of the student, “Going” or “Not going”. When the student is going, press “Set schedule”, choose the day and the time and save: the student is told and the day goes to the calendar of both of you (Calendar, as Counseling). Press the day again to move it (the student is told that it has been moved) or to take it away.",
        ]),
        new("Who goes to the referrals by themselves?",
        [
            "A student who is absent three times in a course, and a student whose attendance is under 80% over at least five marked days. You get a notification.",
            "In Analytics the card “Absent by day” lists each day (the last 30 days) with the students who were marked absent and the courses (weekdays only: classes are Monday to Friday).",
        ]),
        new("How do I find people and make a group?",
        [
            "Open Users: Students and Teachers are everybody of the school, and Staff / Non-teaching are the registrar, the guidance counselor, the academic coordinator and the academic provider. A green dot means the person is online now. The button with the speech bubble writes to the person (it is only there when your role may write to them).",
            "Open Groups and press Add a group: give it a name and choose the people to add. Everybody in the group can write in it. Two groups are made for you: All staff (the registrar, guidance counselor, academic coordinator and academic provider) and Teachers and staff.",
        ], "users", "Open Users"),
    ]);
}
