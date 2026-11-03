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
            "On a phone, the menu button at the top left opens it.",
        ]),
        new("How do I change the look, or my picture?",
        [
            "Press the gear at the top right (Settings).",
            "Under Profile picture, press “Upload a picture” and choose a photo. It is made small for you. “Remove” brings back your initials.",
            "Under Appearance choose Light, Dark or Auto (Auto follows your computer). You can also switch the animations off.",
        ]),
        new("What does the Save button of Settings keep?",
        [
            "The picture, and the look of the pages (Light, Dark or Auto, the animations and the sound switch), are kept as soon as you choose them.",
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
            "The bell at the top lists what happened for you: a new module, a grade, a deadline coming, a friend request, a file someone shared. A new one also plays a short sound. In Settings (the gear at the top) you can choose the sound, add your own sound file, or switch it off. The browser lets a page make sound only after you clicked on it once, so click anywhere on the page if you hear nothing.",
            "Press one to go to the page it is about. “Mark all as read” clears the number.",
        ]),
    ]);

    private static HelpSection Student() => new("For students", "graduation-cap",
    [
        new("Where are my courses, and how far along am I?",
        [
            "Open Courses in the menu. Enrolled shows this term; Completed shows the courses of terms that are over.",
            "Each course has a progress bar. It counts the modules you opened, out of the modules your teacher posted.",
            "Switch between cards and a list with the buttons at the top right of the page. The system remembers the one you chose.",
        ], "student/subjects", "Open Courses"),
        new("How do I open a module and do the assignments?",
        [
            "Open a course, then a module. The syllabus of the course is the first thing in the list.",
            "Under a module you find its assignments, quizzes and task performances. Open one, read the instructions and send your answer before the due date.",
            "A module appears when your teacher posts it. The ones you have not opened yet show on your dashboard.",
            "Inside a course there is a second menu at the left: Modules, Assignments, Quizzes and Task performances (and Exams when your teacher gave one), then Grades and Teachers (the teacher of the course, with a Message button to write to them). It stays in view while you scroll, and the button at its top hides it or shows it again.",
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
            "Press an assignment to go to it.",
        ], "calendar", "Open Calendar"),
        new("How do I evaluate a teacher?",
        [
            "When the academic coordinator sends a questionnaire, a card appears on your dashboard and you get a notification.",
            "Answer one questionnaire for each of your teachers before the due date. Your answers are anonymous: the teacher and the coordinator see the ratings, not who gave them.",
        ], "student/evaluations", "Open the evaluations"),
        new("How do I add friends and see my classmates?",
        [
            "Open Users. Classmates are the students of your section; Students are all the students of the school; Teachers are the teachers of your courses; Staff / Non-teaching are the registrar, the guidance counselor, the academic coordinator and the academic provider.",
            "Press “Add friend” beside a person. They get a request and must accept. In Friends you can also find any student or teacher by name.",
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
            "Open Courses. Each course you teach is a card (or a row, in the list view) with its section. Press Open to see its modules and assignments.",
            "The administrator gives you your courses (Users Management). If one is missing, ask them.",
        ], "teacher/subjects", "Open Courses"),
        new("How do I make a module and post it to my students?",
        [
            "Open a course and press “Add module”. Write the lesson and add a link to a file or a video if you have one. You can also attach slides (a PowerPoint file or a PDF) apart from the other files. Many modules at once: press “Template and import (Excel)” in the same box.",
            "Press Open on a module, or a PDF of it, to read it inside the system, like the students do: the course menu is at the left and Edit module at the right.",
            "A module is a draft until you post it. Choose “post now” or a date and time to post it later. Students see it only after it is posted.",
            "Press “Add activities” at the top right of the course. It opens the Excel template and the import: one workbook lists every module with a row for Seatwork, Assignment, Quiz and Task performance. Fill the rows you want, save it and import it. To add one at a time, open a list in the course menu (for example “See all quizzes”) and press its Add button.",
        ]),
        new("What is the menu inside a course?",
        [
            "Inside a course there is a second menu at the left: Modules, Assignments, Quizzes and Task performances (and Exams, once you added one). Each one lists that kind of work for the whole course. Below them are Attendance, Gradebook and Students: the students of the course, each with a Message button to write to them.",
            "The menu stays in view while you scroll. The button at its top hides it, to give the page more room, and shows it again; the system remembers your choice.",
        ]),
        new("Can I fill many assignments or questions from Excel?",
        [
            "Yes. Press “Add activities”: the template has the sheet Activities (module by module: Seatwork, Assignment, Quiz, Task performance) and the sheet Quiz Questions. Fill it, save it as .xlsx, choose it in the box, choose the files it names and press Import.",
            "Every template has a sheet “How to fill”: for each column it says what to write and what to use in Excel. The cells that take a fixed answer (a quiz, a type, Yes or No) have a drop-down list, and a tip shows when you select a cell. Type a day like 2026-11-19.",
            "In the questions of a quiz, use Template, Export the questions and Import from Excel. The imported questions appear in the list; press Save questions to keep them.",
        ]),
        new("A student handed in after the due date. What do I do?",
        [
            "Open the assignment. A late row says “Late · accept?”. Press Accept to give it a score, or Decline. The student is told. Late work can be graded only after you accepted it.",
        ]),
        new("What are the drafts that came from the academic provider?",
        [
            "The academic provider (of the central team) can send you the syllabus and modules of a course, with their assignments and quizzes. They arrive in your course as drafts with a badge “From the academic provider”.",
            "The provider sees whether you opened what was sent and whether you posted it. Opening a draft to read it or to edit it is enough to count as opened.",
            "They are yours to change: edit the text, move the due dates, then post them when you are ready. The syllabus is always first.",
        ]),
        new("How do I make a quiz?",
        [
            "Add a quiz to a module, then press Questions. Add multiple choice, true or false and short answer questions with their points. A quiz that has a file has no questions: the students answer with a file and you give the score.",
            "In the quiz settings choose the time limit, whether the questions are shuffled, and the anti-cheat rules (how many times a student may leave the page, copy, print or take a screenshot). A photo taken with another device can't be detected, but the student's name is shown faintly over the quiz.",
        ]),
        new("How do I check and grade the assignments of my students?",
        [
            "Open the assignment, quiz or task performance to see who handed in. The Files column shows the files the student sent: press one to read it. Press Grade (or Give score) on a student's row, read the answer and save the score with a comment.",
            "Quizzes are graded by the system; a short answer is right when it matches one of the answers you accepted.",
            "“Assignments to check” on your dashboard lists what waits for you.",
        ]),
        new("How do I take attendance?",
        [
            "Open Attendance, choose the course and the day, and mark each student Present, Late or Absent. Press Save.",
            "Press “View attendance” to see each student's attendance. Students who miss a lot are sent to the guidance counselor by the system.",
        ], "teacher/attendance", "Open Attendance"),
        new("Where is the gradebook?",
        [
            "Open Gradebook and choose a course. Every student has a row, every assignment, quiz and task a column, and the average at the end. Type a score in a box and move on: it is saved by itself. Write the final grade of each student in the last column, then press “Save final grades”. At the top right: “Add Activities” adds a column for extra scores (an assignment, quiz, task performance, seatwork or recitation) and “Add Exam” adds an exam (it has no module to choose); “Export to Excel”, next to “Save final grades”, makes a file that shows how the grades are worked out. The two arrows under the name of a column move it to the left or to the right (the file follows the same order).",
        ], "teacher/gradebook", "Open Gradebook"),
        new("How do I give a due date with the calendar?",
        [
            "Open Calendar. Assignments without a due date are listed at the right: drag one onto a day, or press “Set date”.",
            "Press an assignment on the calendar to see who has handed it in.",
        ], "calendar", "Open Calendar"),
        new("How do I find my students, and make friends?",
        [
            "Open Users. Students are the students of your courses (with the course and section they share); Teachers are all the other teachers of the school; Staff / Non-teaching are the registrar, the guidance counselor, the academic coordinator and the academic provider; Friends are the people who accepted your request.",
            "Green dots show who is online. The dashboard also lists your students who are online.",
        ], "users", "Open Users"),
        new("How do I write to my class?",
        [
            "Open Announcements and press “New announcement”. Choose the class (one of your courses), what it is about (an exam or other), and write it. Only the students of that class see it, and they are told with a notification.",
            "“From the school” lists the notices of the academic coordinator for the teachers: exams, holidays, the semester break and teachers' meetings.",
        ], "teacher/announcements", "Open Announcements"),
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
            "Open Users Management and choose the tab: Students, Teachers or Staff. A green dot on a person's picture means the person is online now.",
            "Press the Add button (Add staff, on the Staff tab), or Edit on a row. In Edit you can change the name and email, set a new password, and switch the account on or off.",
            "A teacher who works in more than one section has a “sections” button: press it to see them all.",
            "Students are added by the registrar when enrolling them. A switched-off account can't sign in.",
            "The school has one administrator, one registrar, one guidance counselor and one academic coordinator; it can have several academic providers. In Add staff, a role that the school has already is not in the list.",
        ], "admin/users", "Open Users Management"),
        new("How do I make a new staff role, like Assistant Registrar?",
        [
            "Open Users Management, the Staff tab, and press Add staff. Next to the Role list press “Add role”.",
            "Type the name of the role (for example Librarian) and press “Add this role”: it is chosen in the Role list, so you can finish adding the person.",
            "Many people can have a role you made. The staff list and the person's card show its name.",
        ], "admin/users/staff", "Open the staff list"),
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
            "Open Security Logs. The tabs filter the lines: Last login (the day each person last logged in), Failed, Accounts and System. You can search by email or address and choose dates.",
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
            "Choose the term, the grade level, the track and strand, the schedule and the section (A, B or C), then the courses. Press Enroll.",
        ], "registrar/enrollments", "Open Enrollments"),
        new("Where do I see the enrolled students?",
        [
            "Open Student's Accounts. Filter by term, strand, schedule or search by name or LRN. Press a student's courses to see them.",
            "You can also see who is enrolled for the next term, and unenroll a student.",
        ], "registrar/student-accounts", "Open Student's Accounts"),
        new("The section I need is not in the list.",
        [
            "Sections A and B exist for each strand, for Grade 11 and Grade 12. If one is missing, ask the academic coordinator to make it in Academic setup.",
        ]),
        new("How do I find people and make a group?",
        [
            "Open Users: Students and Teachers are everybody of the school, and Staff / Non-teaching are the registrar, the guidance counselor, the academic coordinator and the academic provider. A green dot means the person is online now. The button with the speech bubble writes to the person (it is only there when your role may write to them).",
            "Open Groups and press Add a group: give it a name and choose the people to add. Everybody in the group can write in it.",
        ], "users", "Open Users"),
    ]);

    private static HelpSection Coordinator() => new("For the academic coordinator", "clipboard-list",
    [
        new("How do I set up the school years, terms, tracks, strands and courses?",
        [
            "Open Academic setup. The parts are the tabs at the left: School years, Tracks, strands and courses, and Rooms. The search box at the top searches the tab that is open.",
            "School years keep themselves: the school year that is running and the next two are always there, each with three terms. When a school year is over it goes away by itself (its students, grades and attendance stay). Every box is ready to type in: change a name or a day and leave the box, and it is saved. There is no Edit button.",
            "Tracks, strands and courses are three cards side by side: press a track to see its strands, press a strand to see its courses. Change a name in place, or use the row at the bottom of a card to add one. Each card has its own search box.",
            "To fill many rows at once press the Excel button at the top and choose “Template and import (Excel)”: the workbook has a sheet for each step. “Export to Excel” gives the whole setup in the same workbook: change it and import it again. A row that already exists is changed, nothing is deleted.",
        ], "coordinator/academic-setup", "Open Academic setup"),
        new("Where are the sections?",
        [
            "Sections has its own place in the menu. A section is A or B for each strand and grade level. Double click a section to choose its adviser (a teacher of the same strand) and to see the students in it. “View all class schedules” slides every schedule in from the right.",
        ], "coordinator/sections", "Open Sections"),
        new("How do I set the days and the time of a course (MWF or TTH)?",
        [
            "Open the Class schedule (in the menu of the school). It is a week like a spreadsheet: the days are the columns and the hours, 7:00 AM to 6:00 PM, are the rows. Choose the school year, the term and the room at the top.",
            "At the left are the sections with their courses. Drag a course to a day and a time: dropped on a Monday, Wednesday or Friday it meets MWF, dropped on a Tuesday or Thursday it meets TTH. Use the box on a class to change how long it is, the X to take it off, or drag it to move it.",
            "Every room has its own schedule: press the room box to pick a room, add a room or remove one. Two classes can't be in one room at the same time, a section can't have two classes at the same time, and a teacher can't have two classes at the same time.",
            "Double click a section to choose its adviser (a teacher of the same strand) and to see the students in it. Sections has the button “View all class schedules”.",
        ], "coordinator/class-schedule", "Open the class schedule"),
        new("Who sends the syllabus, modules and assignments to the teachers?",
        [
            "The academic provider of the central team. They prepare the course library and send it to the teachers; you do not send course materials.",
            "If a course has no syllabus or a teacher did not get the modules, write to the academic provider (Messages).",
        ]),
        new("How do I evaluate the teachers?",
        [
            "Open Teacher evaluation and make a questionnaire (you can start from the default questions). Add a question by choosing how the students answer: a 5-point Likert scale, a 5-point performance scale, a frequency scale, a binary (Yes/No) scale, a ranking, select-all-that-apply, or a comment. Choose who it is about and the due date, then send it.",
            "Students answer once for each of their teachers; answers are anonymous. The page shows how many answered and who has not.",
            "Close it early, or let it close on its due date.",
            "To write many questions at once press “Template and import (Excel)”: download the template, fill it in and import it. “Export” makes the same file from a questionnaire.",
        ], "coordinator/evaluations", "Open Teacher evaluation"),
        new("Where are the results of the evaluation?",
        [
            "Open Analytics. Choose the questionnaire to see the rating of each teacher, the ratings by area, the Yes/No, ranking and select-all results, and the comments. “Export to Excel” puts everything of the page in one workbook, one sheet for each part, with the result of every teacher.",
        ], "coordinator/analytics", "Open Analytics"),
        new("How do I write an announcement?",
        [
            "Open Announcements and press “New announcement”. Choose what it is about (an exam, a holiday, the semester break, a teachers' meeting or other), write the title and the text, choose who reads it (everybody, the students, the teachers; a teachers' meeting is for the teachers) and, if you want, the last day it is shown.",
            "To write many announcements at once press “Template and import (Excel)”; “Export” makes the same file from the announcements you have.",
            "The megaphone button at the top, next to Messages, shows the latest announcements to everybody.",
        ], "coordinator/announcements", "Open Announcements"),
        new("How do I find people and make a group?",
        [
            "Open Users: Students and Teachers are everybody of the school, and Staff / Non-teaching are the registrar, the guidance counselor, the academic coordinator and the academic provider. A green dot means the person is online now. The button with the speech bubble writes to the person (it is only there when your role may write to them).",
            "Open Groups and press Add a group: give it a name and choose the people to add. Everybody in the group can write in it.",
        ], "users", "Open Users"),
    ]);

    private static HelpSection Provider() => new("For the academic provider", "send",
    [
        new("How do I send a syllabus and modules to the teachers?",
        [
            "Open Course library and choose a course. Add the syllabus first, then the modules (“Template and import (Excel)” in the same box adds many at once). Press a syllabus or a module to open it and see its text and files.",
            "You send the syllabus and the modules to the teachers (they are drafts; the teacher posts them) and you post the summative exams straight to the students. The teacher makes the seatwork, assignments, quizzes and task performances in their own copy of the course.",
            "You can attach files (a PDF, a picture, a text file ...) to the syllabus, a module and a summative exam.",
            "Press “Send to teachers”, choose what to send (the syllabus is the first thing on the list) and which teachers get it.",
            "Each teacher gets their own copy as a draft, with the files: they edit the text and the dates and post it to their students when they are ready. A PDF or a picture opens in a box in the middle of the screen, where it can be downloaded.",
        ], "provider/library", "Open Course library"),
        new("How do I make a summative exam?",
        [
            "Open Course library, choose a course and press “Add summative exam” (or the same button in the card Summative exams).",
            "Write the title, the instructions, attach the paper if you have it, and the highest score. Choose when it opens and when it closes. It must close after it opens.",
            "To make many exams at once press “Excel” in the card: download the template, fill it in and import it, like the modules. “Export” gives the exams of the course in the same file.",
            "Add its questions with the Excel button (the sheet Questions) and choose the quiz settings (time limit, anti-cheating watch). Then press “Post to students” and choose the sections: the exam goes straight to the students, opens and closes at the times you set, and the system gives the scores. The teacher sees them in the gradebook.",
        ], "provider/library", "Open Course library"),
        new("Who has not posted what I sent?",
        [
            "Open Tracking. It has three parts: Syllabus, Modules and Summative exams. In the first two the buttons are Not posted, Scheduled and Not opened. “Not posted” shows the teachers who received a syllabus or a module and have not posted it to their students yet; “Scheduled” shows what a teacher has scheduled for later, with the time it will appear.",
            "Each line has the syllabus or module, the teacher and the section. The search box and the grade and course boxes narrow the list. A module that is scheduled to post later is not late: it is on “Scheduled” and is posted when its time comes.",
        ], "provider/tracking", "Open Tracking"),
        new("Who has not opened it?",
        [
            "Choose “Not opened” in Syllabus or Modules. A student is on this list only after the teacher posted it: until then the teacher is the one on “Not posted”. Once the student opens it, the student leaves the list.",
            "In Summative exams the buttons are Not opened, Not handed in and Handed in. They list the students of the exams that are posted: an exam that is not posted yet has no students here. “Handed in” shows the scores.",
        ]),
        new("Can I remind the people on the list?",
        [
            "Yes. The button “Message” at the top right of the list tells everybody who is on it, and says how many people that is. The words are filled in for you and you can change them. The button is off when the list is empty.",
        ]),
        new("What do the analytics show?",
        [
            "Open Analytics to see how fast the teachers open and post, how many students open, and the numbers by course, by kind (syllabus, module, assignment, quiz, task performance), by section and week by week. “Export to Excel” puts everything of the page in one workbook: the numbers of the page, every teacher, every student and the result of each student in each course.",
        ], "provider/analytics", "Open Analytics"),
        new("How do I find people and make a group?",
        [
            "Open Users: Students and Teachers are everybody of the school, and Staff / Non-teaching are the registrar, the guidance counselor, the academic coordinator and the academic provider. A green dot means the person is online now. The button with the speech bubble writes to the person (it is only there when your role may write to them).",
            "Open Groups and press Add a group: give it a name and choose the people to add. Everybody in the group can write in it.",
        ], "users", "Open Users"),
    ]);

    private static HelpSection Guidance() => new("For the guidance counselor", "heart-handshake",
    [
        new("What do I look after?",
        [
            "You look after two things only: attendance and violations. Failing grades and students at risk in a course are for the teacher.",
            "Open Referrals. Every student of the school is in the list, one line each, so you can write a concern for anybody. Use the search box (name, LRN, grade level, section, course) and the choices Show and Concern to find students.",
        ], "guidance/referrals", "Open Referrals"),
        new("Where do the attendance concerns come from?",
        [
            "The system finds them by itself: a student whose attendance is under 80% over at least 5 marked days. You get a notification, and the student shows with an Attendance badge.",
            "To see only the students who have a concern choose “Only students with a concern” in Show, or use “With concerns” or “Attendance” under Referrals in the menu.",
        ], "guidance/referrals?show=concerns", "Open the students with a concern"),
        new("How do I write a violation or follow up a concern?",
        [
            "Press the “At risk” button on a student (it is red while the student has a concern and white when there is none). A box shows the concerns. To write a new one choose “What is it?” (it starts on None), then choose “What happened?” from the list, add more words if you want, and press “Add the concern”.",
            "Press “Notes” (top right) to choose the help you gave (talked with the student, called the parents, talked with the teacher, other) and write your notes. When the case is over press “Solved”: the concerns move to “Previous concerns” and the button is white again. Several attendance concerns found on the same day are one line: press “View attendance” to see each course.",
        ]),
        new("How do I find people and make a group?",
        [
            "Open Users: Students and Teachers are everybody of the school, and Staff / Non-teaching are the registrar, the guidance counselor, the academic coordinator and the academic provider. A green dot means the person is online now. The button with the speech bubble writes to the person (it is only there when your role may write to them).",
            "Open Groups and press Add a group: give it a name and choose the people to add. Everybody in the group can write in it.",
        ], "users", "Open Users"),
    ]);
}
