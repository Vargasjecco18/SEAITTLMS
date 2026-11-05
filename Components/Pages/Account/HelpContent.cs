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
            "On a phone, the menu button at the top left opens it. On some pages (the academic setup, the enrollment, the course library, the class schedule) the menu folds to its icons by itself so the page has more room: the button at the top of the menu, “Hide Nav bar”, shows the names again.",
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
            "When you open a course from Courses there is the Course Menu at the left: Courses (your courses, to go from one to another), Modules, Activities (Seatwork, Assignments, Quizzes and Task performances, and Exams when your teacher gave one), then Classmates, Teachers (with a Message button to write to the teacher) and Grades. It stays in view while you scroll, and the button at its top hides it or shows it again.",
            "Each row of the course (the syllabus and the modules) has its progress bar, at the left of what the module has (its activities). A module with slides is worth 50 for opening the module and 50 for the slides you saw (the Slides button opens them first, one slide at a time, from left to right: the arrows, the keys of the keyboard or the dots). A module with no slides is 100 as soon as you open it. The Slides and the Module buttons are in the middle of the row. At the right of the lesson are the activities of the module, each one a link with how you did; the button above them hides them. A reference link of a module is at the left of “Open in a new tab”.",
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
            "When you send it, the result is shown if your teacher allowed it.",
        ]),
        new("Where are my grades?",
        [
            "Open Grades. Every course is a card with your progress at the top (the bar has the passing mark drawn on it) and, under it, the gradebook of the course with your own row: a column for each activity (A assignment, Q quiz, TP task performance, S seatwork, SE summative exam), your Average and the Final grade of your teacher. It is only to read.",
            "Open Grades in the menu: your grade in every course and how it is going. A course below 70 is marked.",
            "The dashboard shows your recent grades and your average.",
        ], "student/grades", "Open Grades"),
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
            "Open Users. The tabs are Friends, Classmates, Students, Teachers and Staff / Non-teaching. Students and Teachers are tables with the name, the grade level, the strand, the section, the room (from the class schedule), a Message button and the friend status; Staff / Non-teaching has the name, the role, and at the right the Message button and the friend status. The Message button is there for everybody, friends or not.",
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
            "Open Courses. Each course you teach is a card (or a row, in the list view) with its section. Press Open to see its modules and assignments.",
            "The administrator gives you your courses (Users Management). If one is missing, ask them.",
        ], "teacher/subjects", "Open Courses"),
        new("How do I make a module and post it to my students?",
        [
            "Open a course and press “Add module”. Write the lesson and add a link to a file or a video if you have one. You can also attach slides (a PowerPoint file or a PDF) apart from the other files. Many modules at once: press “Add many modules in a sheet” in the same box: a row for each module with its Title, its Slides (optional), its Link and its Files. There is no Excel file to download.",
            "Press Open on a module, or a PDF of it, to read it inside the system, like the students do: the course menu is at the left and Edit module at the right.",
            "A module is a draft until you post it. In its row of the course, press the date (it says Draft · hidden, Posted Oct 6, 2026 or Scheduled for Nov 10, 2026, 8:00 AM): a small box has the time it appears. Choose a time and press Save time (a later time schedules it, an earlier one posts it from that date), or press Post now. The Hide from students button of the row makes it a draft again. Students see a module only after it is posted. The bar of each row is how far the class is: 50 for the slides and 50 for the module (100 for the module when it has no slides), the average of the class.",
            "Press “Add activities” at the top right of the course. It offers “Add activities” and “Add quiz”. “Add activities” opens a sheet inside the system: a row for each activity, with its Module and its Type (lists to choose from), the Title, the Instructions, the Points, the Due date and the Files; fill the rows you want and press Save (the sheet fits the box: no scroll bars). “Add quiz” asks for the module, then the box of the quiz: after saving it you write its questions in a sheet. To add one at a time, open Activities in the Course Menu, then a kind (for example “See all quizzes”) and press its Add button.",
        ]),
        new("What is the menu inside a course?",
        [
            "When you open a course from Courses there is the Course Menu at the left: Courses (your courses, to go from one to another), Modules, Activities (Seatwork, Assignments, Quizzes and Task performances, each one listing that kind of work for the whole course), then Students (each with a Message button), Gradebook and Attendance. The first page of the course lists the syllabus and the modules: each row shows how far the class has read the slides (the average of the class), with the Slides and the Module buttons.",
            "The menu stays in view while you scroll. The button at its top hides it, to give the page more room, and shows it again; the system remembers your choice.",
            "Every kind ends with an “Add seatwork” (or “Add assignment” ...) line, also when it already has activities. Inside an activity the Course Menu is at the left of the page, and a double click on the title of the activity shows what you gave (the instructions and the files). There are no exams in your menu: the summative exams come from the academic provider.",
        ]),
        new("Can I add many activities at once?",
        [
            "Yes. Press “Add activities” (or “Many in a sheet” in an Add menu): a sheet opens in the middle of the screen, with the step by step at the right. Each row is one seatwork, assignment, quiz or task performance. Choose its module and its type from the lists, type the title, the instructions and the points, choose the due date and add the files if there are any.",
            "To add more rows choose the type (Seatwork, Assignment, Quiz or Task performance) and the number at the bottom, then press Add (“Add [type] [number] rows”), like the questions of a summative exam. The Ready column says what is missing in a row. A row with nothing in it is ignored, and a row with a problem is left out. Press Save: every row becomes an activity of its module.",
            "The questions of a quiz are written in the quiz itself (Questions), in a sheet like the one of a summative exam: press “Add questions” (or “Edit the questions”), write a row for each question (its type and its right answer are chosen from lists), and use “Add [type] [number] rows” to add many rows of one type. There is no Excel for the questions any more.",
        ]),
        new("A student handed in after the due date. What do I do?",
        [
            "Open the assignment. A late row says “Late · accept?”. Press Accept to give it a score, or Decline. The student is told. Late work can be graded only after you accepted it.",
        ]),
        new("What are the drafts that came from the academic provider?",
        [
            "The academic provider (of the central team) can send you the syllabus and modules of a course, with their assignments and quizzes. They arrive in your course as drafts.",
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
            "Open the assignment, quiz or task performance to see who handed in. The Files column shows the files the student sent: press one to read it. Press the row of a student (or the Answer tag in the Answers column): a box opens with the students who handed in at the left, the answer in the middle, with a preview of the files the student sent, and the score and the comment at the right. Read it, save the score, and press another student at the left to go on.",
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
            "Open Gradebook and choose the course in the list at the top left of the card. Every student has a row, every assignment, quiz and task a column, and the average at the end. Scores are saved by themselves when you leave a box. Write the final grade of each student in the last column, then press “Save final grades”. At the top right: “Add Activities” adds a column for extra scores (an assignment, quiz, task performance, seatwork or recitation) and “Add Exam” adds an exam (it has no module to choose); “Export to Excel”, next to “Save final grades”, makes a file that shows how the grades are worked out. The two arrows under the name of a column move it to the left or to the right (the file follows the same order).",
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
            "Open Enrollment. Type the 12-digit LRN (digits only, like the phone number) and the names (letters only). If the student has an account it is found and filled in; otherwise their account is made, with a sign-in you can copy and give to them.",
            "Choose the grade level, the track and strand and the section (if the section is not in the list, choose “+ Add a section…” and type its name). The courses of the strand show under it. The sign-in details are under the student card, at the left, and stay where they are when the courses appear. Press “Print Enrollment” and the print preview of the browser opens at once, with the enrollment form of this student; then “Enroll student” to enroll.",
        ], "registrar/enrollments", "Open Enrollment"),
        new("Where do I see the enrolled students?",
        [
            "Open Student's Accounts. Filter by term, strand, schedule or search by name or LRN. Press a student's courses to see them.",
            "You can also see who is enrolled for the next term, and unenroll a student.",
            "“View Enrolled Student Today” (top right) lists the students enrolled today, the newest first, with their LRN, grade level, strand, section and term.",
        ], "registrar/student-accounts", "Open Student's Accounts"),
        new("The section I need is not in the list.",
        [
            "Sections A, B and C exist for each strand, for Grade 11 and Grade 12. If one is missing, open the Section list in Enrollment and choose “+ Add a section…”: type its name and it is added.",
        ]),
        new("How do I find people and make a group?",
        [
            "Open Users: Students and Teachers are everybody of the school, and Staff / Non-teaching are the registrar, the guidance counselor, the academic coordinator and the academic provider. A green dot means the person is online now. The button with the speech bubble writes to the person (it is only there when your role may write to them).",
            "Open Groups and press Add a group: give it a name and choose the people to add. Everybody in the group can write in it. Two groups are made for you: All staff (the registrar, guidance counselor, academic coordinator and academic provider) and Teachers and staff.",
        ], "users", "Open Users"),
    ]);

    private static HelpSection Coordinator() => new("For the academic coordinator", "clipboard-list",
    [
        new("How do I set up the school years, terms, tracks, strands and courses?",
        [
            "Open Academic setup. The parts are the tabs at the left: School years, Tracks, strands and courses, Class sections and Rooms; a tab opens a dropdown with what is inside (the school years, the tracks and strands, the grade levels, the rooms) to go straight to one. The menu of the school folds by itself on this page, and the button above the tabs (Hide tabs) hides only the tabs. The search box at the top searches the tab that is open.",
            "School years keep themselves: the school year that is running and the next two are always there, each with three terms. When a school year is over it goes away by itself (its students, grades and attendance stay). Every box is ready to type in: change a name or a day and leave the box, and it is saved. There is no Edit button.",
            "Tracks, strands and courses are three cards side by side, each with its columns (tracks: Code, Name, Strands, Offered, Delete; strands: Code, Name, Courses, Offered, Delete): press a track to see its strands, press a strand to see its courses. Change a name, the code or the unit in place, or use the row at the bottom of a card to add one. A course has the columns Code, Course, Unit, Offered and Delete: uncheck Offered and the registrar can't enroll students in it; Delete takes it out of the strand, after asking. Each card has its own search box. The button above the tabs hides them, so the cards have more room.",
            "To fill many rows at once press the Excel button at the top and choose “Template and import (Excel)”: the workbook has a sheet for each step. “Export to Excel” gives the whole setup in the same workbook: change it and import it again. A row that already exists is changed, nothing is deleted.",
        ], "coordinator/academic-setup", "Open Academic setup"),
        new("Where are the class sections?",
        [
            "Class sections is a tab of the Academic setup. The tab opens a list of the levels: Grade level (11 and 12), inside it the Strands and inside each strand the Sections A, B and C; press a grade level to see all its strands and sections. In the page there is a tab for each class too, and inside each strand the sections are cards. Press a card to choose its adviser (a teacher of the same strand) and to see the students in it.",
            "A card that says “Add section” makes the section that is missing (A, B and C). The bin on a card deletes the section, after asking.",
        ], "coordinator/academic-setup/sections", "Open Class sections"),
        new("How do I set the days and the time of a course (MWF or TTH)?",
        [
            "Open the Class schedule (in the menu of the school). It is a week like a spreadsheet: the days are the columns and the hours, 7:00 AM to 6:00 PM, are the rows. Choose the school year, the term and the room at the top (a school year or a term that is over is not in the lists). “View all class schedule” shows every class over all the screen.",
            "At the left are the sections with the courses that have no time yet. Each section says its room and how many courses are on the schedule (6 of 6), and a section with nothing left to place goes to the bottom. Drag a course to a day and a time (it is one hour long; the box on the class changes it): dropped on a Monday, Wednesday or Friday it meets MWF, dropped on a Tuesday or Thursday it meets TTH. Once it is on the week the course leaves the list; use the box on a class to change how long it is, the X to take it off (it goes back to the list), or drag it to move it.",
            "Every room has its own schedule: press the room box to pick a room, add a room or remove one. Two classes can't be in one room at the same time, a section can't have two classes at the same time, and a teacher can't have two classes at the same time.",
            "Double click a section to choose its adviser (a teacher of the same strand) and to see the students in it.",
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
            "Open Announcements and press “New announcement”. Choose what it is about (an exam, a holiday, the semester break, a teachers' meeting or other), write the title and the text, choose who reads it (everybody: the staff, the teachers and the students; the students; the teachers; the staff; or the staff and the teachers; a teachers' meeting is for the teachers) and, if you want, its Start and its End (the days it is about). It is shown until its End, and the people it is for see every day from Start to End in their calendar.",
            "To write many announcements at once press “Template and import (Excel)”; “Export” makes the same file from the announcements you have.",
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
        new("How do I send a syllabus and modules to the teachers?",
        [
            "Open Course library: the courses of the current term are cards, like the Courses of the teachers and the students. Use the search box and the lists Track, Grade level, Strand and Course to find one, then press Open (a course opens with a Course Menu at the left, where the Courses dropdown goes from track to strand to course without going back; the previous terms are hidden, because the library works on the current term). Add the syllabus first, then the modules (“Add many modules in a sheet” in the same box opens a sheet inside the system, in the middle of the screen: a row for each module with its title, its slides (optional), its link (optional) and its files (needed), and the step by step at the right; no Excel is needed. The teachers and the students get a Slides button when a module has slides). Press a syllabus or a module to open it and see its text and files.",
            "You send the syllabus and the modules to the teachers (they are drafts; the teacher posts them) and you post the summative exams straight to the students. The teacher makes the seatwork, assignments, quizzes and task performances in their own copy of the course.",
            "You can attach files (a PDF, a picture, a text file ...) to the syllabus, a module and a summative exam. There is no limit in MB for them. The path at the top (Library › Academic › the strand › the term) takes you back one step at a time.",
            "Press “Send to teachers”, choose what to send (the syllabus is the first thing on the list) and which teachers get it.",
            "Each teacher gets their own copy as a draft, with the files: they edit the text and the dates and post it to their students when they are ready. A PDF or a picture opens in a box in the middle of the screen, where it can be downloaded.",
        ], "provider/library", "Open Course library"),
        new("How do I make a summative exam?",
        [
            "Open Course library, choose a course and press “Add summative exam” (or the same button in the card Summative exams).",
            "Write the title and the instructions, then press “Add questions” (it is under the instructions, and the highest score is under the questions): a sheet opens where each row is a question, with its type and its right answer chosen from lists; “Add [type] [number] rows” adds many rows of one type. Choose when it opens and when it closes. It must close after it opens. The academic coordinator gets a notification and an announcement with the dates.",
            "In a course the buttons are in groups: Add module and Send to teachers | Add summative exam and Post to students | Complete | Copy to next school year. “Complete” marks the course as finished: it leaves the main page of the library and is only under Complete (the list at the right of All courses: Uncompleted or Complete); “Uncomplete” puts it back. The library also has a list view (a table with a line for each course). To make many exams at once press “Excel” in the card: download the template, fill it in and import it, like the modules. “Export” gives the exams of the course in the same file. “Copy to next school year” (top of a course) sends the syllabus and the modules to the teachers who have the course in the next school year, as drafts.",
            "Choose the Summative Exam Settings (time limit, anti-cheating watch, order, result; they are in four columns under the highest score). Then press “Post to students” and choose the sections: the exam goes straight to the students, opens and closes at the times you set, and the system gives the scores. The teacher sees them in the gradebook.",
        ], "provider/library", "Open Course library"),
        new("Who has not posted what I sent?",
        [
            "Open Tracking. It has three parts: Syllabus, Modules and Summative exams. In the first two the buttons are Not posted, Scheduled and Not opened. “Not posted” shows the teachers who received a syllabus or a module and have not posted it to their students yet; “Scheduled” shows what a teacher has scheduled for later, with the time it will appear; you can write to one teacher (or all of them) when it is scheduled too far away.",
            "Each line has the syllabus or module, the teacher and the section. The search box and the grade and course boxes narrow the list. A module that is scheduled to post later is not late: it is on “Scheduled” and is posted when its time comes.",
        ], "provider/tracking", "Open Tracking"),
        new("Who has not opened it?",
        [
            "Choose “Not opened” in Syllabus or Modules. A student is on this list only after the teacher posted it: until then the teacher is the one on “Not posted”. Once the student opens it, the student leaves the list.",
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
            "On the row of the student choose the Concern (Attendance or Violation) and then choose what happened from the list: the concern is recorded. The name of the student opens a box with the earlier concerns and a place for more words only you can read.",
            "Press the Notes button of the row (it says Noted when there are notes and None when there are not) to choose the help you gave and write your notes. When the case is over press Solved. For an attendance concern press “View attendance” to see each course in a box in the middle of the screen.",
        ]),
        new("How do I find people and make a group?",
        [
            "Open Users: Students and Teachers are everybody of the school, and Staff / Non-teaching are the registrar, the guidance counselor, the academic coordinator and the academic provider. A green dot means the person is online now. The button with the speech bubble writes to the person (it is only there when your role may write to them).",
            "Open Groups and press Add a group: give it a name and choose the people to add. Everybody in the group can write in it. Two groups are made for you: All staff (the registrar, guidance counselor, academic coordinator and academic provider) and Teachers and staff.",
        ], "users", "Open Users"),
    ]);
}
