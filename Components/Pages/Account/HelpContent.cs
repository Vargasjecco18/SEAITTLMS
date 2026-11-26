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
            "Open the address of the system and type your email and your password. An administrator may be asked for a code too: it is sent to the phone of the account, and typed on the next step of the same page.",
            "When you close the page, the tab or the browser you are signed out, without waiting: it takes about three seconds. A refresh, or moving from one page to another, does not sign you out. On a shared computer, press the arrow button to sign out before you leave.",
            "Under the password there is a box “Save account”. When you tick it, the system remembers your email on that computer (not your password), so the next time it is already typed and you only type the password. Under the form it says which email is saved, and “Forget” takes it away. Do not tick it on a computer that other people use.",
            "If your account was deactivated, a box over the page says so when you try to log in. Ask the school office (the registrar for students, the administrator for staff) to activate it again.",
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
            "Under Appearance choose Default (the blue and the yellow of SEAITT: it changes the page, the cards, the top bar and the menu), Light, Dark, Midnight, Paper or Auto (Auto follows your computer). When you sign out the look goes back to Auto, so choose it again after you sign in. You can also switch the animations off.",
        ]),
        new("How do I change my phone number?",
        [
            "Press the gear at the top right (Settings). Everybody except the administrator has a “Phone number” part: type your mobile number like 09171234567 and press “Change phone number”. It is kept at once (the Save button at the bottom is not needed). Leave the box empty and press the button to have no phone number.",
            "The administrator's number is the one that receives the login code, and it is changed in System Settings, in the card “Login code (OTP)”.",
        ]),
        new("What does the Save button of Settings keep?",
        [
            "The picture, and the look of the pages (Light, Dark, Auto or one of the five colors, the animations and the sound switch), are kept as soon as you choose them.",
            "The notification sound and the password are kept with the Save button at the bottom of Settings. Choose a sound (it plays so you can hear it), or fill in the password boxes, then press Save. The Settings box closes when it is saved. If something is wrong (for example the current password is not right), the box stays open and tells you why. A new password signs you out, and you log in again with it.",
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
        new("My guidance counselor wrote to me. How do I see the message again?",
        [
            "When your guidance counselor writes about you, a box opens in the middle of the screen. Press OK, or Going or Not going when the counselor asks if you will come to the guidance office.",
            "If you closed the box without answering, you can open it again: press the bell at the top, then press the message of your guidance counselor (it says “Press to see the message again”). While you have not answered, the box has the buttons Going and Not going again, and the bell says “You have not answered yet”.",
        ]),
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
            "Your answers are saved by themselves while you work (a small “Saved” note shows it, and a bulk save runs every few seconds). If the page closes, your connection drops or you refresh, open the quiz again: your answers are there and you go on from where you were.",
            "Some quizzes watch if you leave the page, copy, paste, print or take a screenshot. Each one is a warning, and too many warnings end the quiz and send what you have. Your name is shown faintly over the quiz. Some quizzes also start in full screen (leaving full screen is a warning), show one question at a time with Previous and Next, or do not let you go back to a question you left.",
            "A quiz can have several kinds of question: choose one answer, tick several, true or false, type a short answer, type the word of an identification, fill in the blanks of a sentence, match the items of Column A with Column B, or write an essay. Some questions have a picture. An essay is scored by your teacher, so the quiz waits for the score after you hand it in; every other question is checked by the system.",
            "When you send it, the result is shown if your teacher allowed it, and the quiz (or the exam) is marked Taken in the list of the course.",
        ]),
        new("Where are my grades?",
        [
            "Open Grades. Every course is a card with your progress at the top (the bar has the passing mark drawn on it) and, under it, the gradebook of the course with your own row. The columns are grouped like the grading system of the school: Written/Oral Works (assignments, seatwork, recitation, other work and quizzes) are 20%, Performance Task 50%, Summative Test 15% and Term Examination (TE) 15%. Your Final grade is the weighted average of the four parts, as a whole number, with its descriptor: Advancing (90 to 100), Benchmarking (80 to 89), Connecting (75 to 79), Developing (65 to 74) or Emerging (0 to 64). It is only to read.",
            "The Final grade is 0 until every score of the course is in (a score that is still missing counts as 0). The quizzes, the Summative Test and the Term Examination are checked by the system; the other work is scored by your teacher. 75 and above is Passed; below 75 is Failed.",
            "The dashboard shows your recent grades and your average.",
            "If your teacher hides the grades of a course, the card says “Hidden by your teacher” and shows no scores until your teacher shows them again.",
        ], "student/grades", "Open Grades"),
        new("Where are the details and the feedback of an activity?",
        [
            "Open an assignment, a quiz or a task. In the header, before “Back to the handout”, there are two buttons: Details (the status, the due date, the highest score and your score) and Feedback (what your teacher wrote about your work; a red dot means there is feedback). Each one opens a box that slides in from the right side of the screen (close it with the X or Escape).",
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
            "Open a course and press “Add handout”. Write the lesson and attach the files of the handout. You can also attach slides (a PowerPoint file or a PDF) apart from the other files. The steps are in a “How to fill this sheet” toggle at the top of the box: open it to read them, close it to have the room. Many handouts at once: press “Add many handouts in a sheet” in the same box: a row for each handout with its Title, its Slides (optional) and its Files. There are no reference links any more: put what the students need in the files.",
            "Press Open on a handout, or a PDF of it, to read it inside the system, like the students do: the course menu is at the left and Edit handout at the right. The Slides card is first and the handout card is second; the Slides button is at the right side of the slides card.",
            "A handout is a draft until you post it. In its row of the course, press the date (it says Hidden from students, Posted Oct 6, 2026 or Scheduled for Nov 10, 2026, 8:00 AM): a small box has the time it appears. Choose a time and press Save time (a later time schedules it, an earlier one posts it from that date), or press Post now. The Hide from students button of the row makes it a draft again. Students see a handout only after it is posted. The bar of each row is how far the class is: 50 for the slides and 50 for the handout (100 for the handout when it has no slides), the average of the class.",
            "Press “Add activities” at the top right of the course. It offers “Add activities” and “Add quiz”. “Add activities” opens a sheet inside the system: a row for each activity, with its Handout and its Type (lists to choose from), the Title, the Instructions, the Points, the Due date and the Files; fill the rows you want and press Save (the sheet fits the box: no scroll bars). “Add quiz” opens a sheet of questions: choose the Handout of the quiz and write its Title, then write a row for each question and press Save: the quiz is made with its questions. Set its due date, its time limit and the anti-cheat rules afterwards with Edit on the quiz. To add one at a time, open Activities in the Course Menu, then a kind (for example Quizzes) and press its Add button.",
        ]),
        new("How do I see the file I gave for an activity?",
        [
            "Open the activity (from the Course Menu at the left, or from the course page). At the top right there is the button “Activity”, at the left of “Back to the course”.",
            "Press it: a box opens in the middle of the screen with what you gave, the instructions and the file you uploaded. A PDF is shown inside the box, and you can open it in a new tab or download it. Press Escape or Close to go back to the results. A double click on the title of the activity does the same.",
        ]),
        new("What is the menu inside a course?",
        [
            "When you open a course from Courses there is the Course Menu at the left: Courses (your courses, to go from one to another), Handouts, Activities (Seatwork, Assignments, Quizzes and Task performances, each one listing that kind of work for the whole course), then Students (each with a Message button), Gradebook and Attendance. The first page of the course lists the syllabus and the handouts: each row shows how far the class has read the slides (the average of the class), with the Slides and the Handout buttons.",
            "The menu stays in view while you scroll. The button at its top hides it, to give the page more room, and shows it again; the system remembers your choice.",
            "Only Courses and Handouts have a “See all” line; every kind of activity ends with an “Add seatwork” (or “Add assignment” ...) line, also when it already has activities. Inside an activity the Course Menu is at the left of the page, and a double click on the title of the activity shows what you gave (the instructions and the files). There are no exams in your menu: the summative exams come from the academic provider.",
        ]),
        new("Can I add many activities at once?",
        [
            "Yes. Press “Add activities” (or “Many in a sheet” in an Add menu): a sheet opens in the middle of the screen. The step by step is a toggle at the top of the sheet (“How to fill this sheet”): open it when you need it. Each row is one seatwork, assignment or task performance (a quiz is added with “Add quiz”). Choose its handout and its type from the lists, type the title, the instructions and the points, choose the due date and add the files if there are any.",
            "To add more rows choose the type (Seatwork, Assignment or Task performance) and the number at the bottom, then press Add (“Add [type] [number] rows”), like the questions of a summative exam. The Ready column says what is missing in a row. A row with nothing in it is ignored, and a row with a problem is left out. Press Save: every row becomes an activity of its handout.",
            "The questions of a quiz are written in a sheet like the one of a summative exam, in a wide box with tall rows: “Add quiz” opens it for a new quiz, and “Questions” on a quiz that is there opens it again. Write a row for each question, and use “Add [type] [number] rows” to add many rows of one type.",
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
            "The right answer is chosen, not typed: each choice has a circle (one answer) or a box (several answers) in front of it, and true or false has two circles. Choose “How many choices” from the list (2 to 8) and the rows appear. A question with no right answer chosen is shown in the Ready column as a problem.",
            "In the quiz settings choose the time limit, whether the questions are shuffled, and the anti-cheat rules: how many times a student may leave the page, copy, print or take a screenshot, plus three switches: “Full screen only” (the quiz starts in full screen; leaving it is a warning), “One question at a time” (Previous and Next) and “No going back” (a question that was left can't be opened again; it needs one question at a time). A photo taken with another device can't be detected, but the student's name is shown faintly over the quiz. The same settings are in the summative exam of the academic provider.",
            "The answers of the students are saved by themselves while they work, so a closed page does not lose them. The quiz is checked by the system as soon as it is sent (an essay waits for your score), and it goes straight to the gradebook: you do not type its score.",
        ]),
        new("How do I check and grade the assignments of my students?",
        [
            "Open the assignment, quiz or task performance to see who handed in. In the Answers column one button (“View answer”, also when the student sent files) opens the work of the student; pressing the row does the same. A box opens with the students who handed in at the left, the answer in the middle, with a preview of the files, and the score and the feedback at the right.",
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
            "Open Gradebook and choose the course in the list at the top left of the card. Every student has a row and every piece of work a column, grouped by the grading system of SEAITT: Written/Oral Works 20% (assignments, seatwork, recitation, others and the quizzes), Performance Task 50%, Summative Test 15% (the summative exam of the academic provider) and Term Examination (TE) 15%. The last column is the Final grade, with its descriptor.",
            "Scores are saved by themselves when you leave a box, and a small message appears at the top right of the screen (it is not a box in the middle of the page). The quizzes, the Summative Test and the Term Examination are checked by the system: their cells are not editable, except an essay, which you score. Everything is worked out by the system: you do not calculate anything. If any score is missing the Final grade is 0 until it is in.",
            "At the top right: “Add section” adds a recitation, other work, a performance task or the Term Examination (TE) column, “Import class grades” reads an Excel file with the scores of the class (the box has “Download the Excel of this course” to start from), “Hide from students” (it becomes “Show to students”) hides the grades of the course from the students, and “Send final grades” sends the Final grade column to the students. The two arrows under the name of a column move it to the left or to the right (the file follows the same order).",
            "Under the table are two summary tables: the descriptors (Advancing 90 to 100, Benchmarking 80 to 89, Connecting 75 to 79, Developing 65 to 74, Emerging 0 to 64) and Passed (75 to 100) / Failed (0 to 74), each with MALE, FEMALE, TOTAL and the percent. They have no numerical-grade column and no blank column: the name of the line, then MALE, FEMALE, TOTAL and the percents. Scores are whole numbers (no decimals): the points of an activity, of a quiz question and of an exam, and the score you type.",
        ], "teacher/gradebook", "Open Gradebook"),
        new("How is the final grade worked out?",
        [
            "Each part is the points earned divided by the points possible of its work, as a percent. The four parts are weighted 20, 50, 15 and 15, which add up to 100, so the grade is the weighted average (if the administrator changes the weights so they do not add up to 100, the grade is scaled to 100%: each weight divided by the total of the weights). A part that has no work yet is left out and the others are scaled to 100% (so the grade as it stands is shown while the course goes on).",
            "The Final grade is strict: it is 0 until every column has a score for the student, and then it is a whole number (the average rounded). The administrator can change the weights in System Settings, under Grading system.",
            "Example: Written/Oral 90%, Performance Task 80%, Summative Test 70% and Term Examination 60% make (90×20 + 80×50 + 70×15 + 60×15) ÷ 100 = 77.5, so the Final grade is 78 (Connecting, Passed).",
        ], "teacher/gradebook", "Open Gradebook"),
        new("How do I refer a student to the guidance counselor?",
        [
            "Open the course and choose Students in the Course Menu. At the right of each student press “Refer”. A box asks for the factor (Absenteeism, Non-compliance or Behavioral), what happened, and more words (required for Others).",
            "The guidance counselor gets the referral in the list of Referrals and a notification, and the student's concern is set. Failing grades and students at risk are yours to help; refer a student when the concern is about attendance, following the rules or behavior.",
        ], "teacher/subjects", "Open Courses"),
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
            "Open Calendar and press “Announcement to my class”, or double click a day and choose Announcement. Tick the classes (one or more of your courses, or “All my classes”), and write it. One announcement is posted for each class that is ticked: only the students of that class see it, and they are told with a notification.",
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
            "Open Users Management, the Staff tab, and press Add staff. In “Name of the new role” type the role (the usual ones are in the list: Registrar, Guidance counselor, Academic coordinator, Academic provider, the two Admission roles, Admission Grade 11 and Admission Grade 12, and the roles you made before). A person of an Admission role records the students of that grade level and is listed with the staff (the Staff / Non-teaching list of the Users page shows the role, for example Admission Grade 11).",
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
        new("How do I archive students who graduate or drop out, and see their grades?",
        [
            "The archive is automatic. When the last term of a school year has ended, every Grade 12 student of that term is archived by the system: marked Graduated, enrollments completed, referrals ended and the sign-in switched off. You do not choose students any more, and there is nothing to tick.",
            "A student who left school is dropped out in Users Management (the Drop out button of the student, in the Students tab, or in the registrar's Student's Accounts). The student then moves to Archive Accounts with the note “Dropped out”, and is no longer in the Students list of Users Management (choose Archived in the Status list there to see the archived students). The sign-in is switched off.",
            "Archive Accounts is for the administrator only: nobody else can open the archived records. “Grades” on a row shows every course the student completed, term by term, with the final grade, the descriptor and the remarks (where the student started, a shift to another strand, and graduation).",
            "“Form 137” and “Form 138” download the academic records of the student as Excel files made to be printed: Form 137 is the permanent record (every school year and term), Form 138 is the report card of the last school year. “Export” saves the grades in Excel and “Export all grades” saves everybody in one table.",
            "An archived record is never deleted, by itself or by anybody. “Restore” takes a student out of the archive, the account works again and the registrar enrolls the student for a class.",
        ], "admin/archive", "Open Archive Accounts"),
        new("How do I change the grading system?",
        [
            "Open System Settings and look for the card “Grading system”: the four weights (Written/Oral Works 20, Performance Task 50, Summative Test 15, Term Examination 15, which add up to 100). Change a weight and press Save settings: the gradebook, the grades of the students, the analytics and the exported files use the new weights at once (scaled to 100% when they do not add up to 100). The descriptors (Advancing 90 to 100 and the rest) and the passing grade (75) are the rule of the school and do not change here.",
            "The number of students in a class section is 40 (the maximum) and is not a setting any more.",
        ], "admin/settings", "Open System Settings"),
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
        new("How do I turn on the login code (OTP)?",
        [
            "With the login code on, an administrator who types the right email and password also types a code of 6 digits that UniSMS sends to the phone number of the account (from the sender Unisoft). For now only administrators are asked; teachers, students and the staff sign in as before.",
            "First put the phone number of your account in System Settings, the card “Login code (OTP)”, the box “Phone number of your account” (like 09171234567), and press “Save my number”. The API key of UniSMS is never typed in the system: whoever runs the system puts it in the user secrets or in appsettings.Local.json (the README says how) and starts the system again.",
            "The card says whether UniSMS is set up and whether your account has a number. Press “Send a test message to my phone” to see that the message arrives, tick “Ask administrators for a code when they log in” and press Save settings. The switch can't be turned on until UniSMS is set up and your account has a number.",
            "At the login the page asks for the code. It lives 5 minutes, takes 5 wrong tries, and “Send a new code” asks for another one after 1 minute (4 codes in all for one sign-in). “Use another account” goes back to the password. If the message can't be sent (no credit, no internet) the administrator can't sign in: untick the switch in the database, see the README (One-time code).",
            "The security log has Code sent, Wrong or expired code and Code accepted (never the code itself). If the switch is on but UniSMS or the number is missing, the administrator signs in with the password only and the log says Signed in without a code.",
        ], "admin/settings", "Open System Settings"),
    ]);

    private static HelpSection Registrar() => new("For the registrar", "clipboard-check",
    [
        new("How do I enroll a student?",
        [
            "The Admission staff of Grade 11 and Grade 12 record the information and the documents of each student first, so the LRN is not typed. Open Enrollment and, in the Student list, choose the student that was recorded: the LRN (shown, not typed), the names, the phone number and the parents or guardian are filled in. If the student already has an account, the stored data is shown.",
            "If the student has no account yet it is made when you enroll, with a sign-in you can copy and give to them. The phone number can be changed in the box and is saved with the student.",
            "The students are enrolled one at a time: the Admission staff record each one first (the information and the documents), and you choose the student from the list. There is no Excel import.",
            "Choose the grade level, the track and strand and the section. A section holds 40 students and Section A has to be full before Section B opens (the later sections are greyed out with “fill A first”); the sections cannot be edited. The courses of the strand show under it. The sign-in details are under the student card, at the left. Press “Print Enrollment” to print the form: it has the student, the courses and the Parents and guardian block, already filled in.",
            "If something is missing when you press “Enroll student”, each box with a problem turns red with a message under it, a red alert at the top lists them, and the cursor goes to the first one. Fix the box and its message goes away.",
        ], "registrar/enrollments", "Open Enrollment"),
        new("Where do I see the enrolled students?",
        [
            "Open Student's Accounts (the sidebar folds). Filter by term, strand or next term, or search by name or LRN. Each row has Account (the email and, while the student has not changed it, the first password), Form (the printable enrollment form of the student), Info (the enrollment information and the parents and guardian in a box), Edit (the enrollment) and Drop out. The grade is a button, “Overall grade”: it opens every course of the student with its final grade, term by term (the earlier terms stay there). Each student has one line only, even after being enrolled again. Students who graduated or were archived are not in this list or in the numbers of the registrar.",
            "When a term ends, a note at the top of your dashboard and of this page says which term ended and which one is next. The last column is about the next term: “Enrolled” with the term (and Unenroll) when the student is enrolled for it, otherwise one button, “Re-enroll for 2nd Term” (or 1st, or 3rd Term). The button is off until the current term has ended; when it works, it opens a box with the grade level, the track and the strand filled in, and you choose the section.",
            "“View Enrolled Student Today” (top right) lists the students enrolled today, the newest first, with their LRN, grade level, strand, section and term.",
        ], "registrar/student-accounts", "Open Student's Accounts"),
        new("The section I need is not in the list.",
        [
            "A section holds 40 students at most, and the number is fixed (it cannot be edited). The students fill Section A first: Section B opens only when A has its 40, and C when B has its 40, so the later sections show “fill A first” while an earlier one has room.",
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
            "Open Teacher evaluation and make a questionnaire (you can start from the default questions). Add a question by choosing how the students answer: a 5-point Likert scale, a 5-point performance scale, a frequency scale, a binary (Yes/No) scale, a ranking, select-all-that-apply, or a comment. Choose who it is about. The Details of the evaluation have the Start date (the day and time it is posted; empty means when you send it) and the End date (the last moment the students can answer). When you send it, the same two dates are asked once more (“Post on” and “Close on”). An evaluation with a later start shows as Scheduled and the students see it only from its start. The questions are written in cards: a title-like line for the question, a round list for how the students answer, and the options in a soft box.",
            "Students answer once for each of their teachers; answers are anonymous. In an evaluation the Questions are first and the Details after them; “Answers by course” shows how many answered and who has not. “Message” writes to the students who have not answered in one course, and “Message all not answered” (at the right of the header of the card) writes once to every student who has not answered in any course.",
            "Close it early, or let it close on its close date and time.",
            "New evaluation opens a sheet like the one of a quiz: write the title and the instructions, then a row for each question. Choose its type from the list (5-Point Likert Scale, 5-Point Performance Scale, Frequency Scale, Binary Scale, Ranking Scale, Select-All-That-Apply or Comment), write its area and, for a ranking or a select-all, the options.",
        ], "coordinator/evaluations", "Open Teacher evaluation"),
        new("Where are the results of the evaluation?",
        [
            "Open Analytics. Choose the questionnaire to see the rating of each teacher, the ratings by area, the Yes/No, ranking and select-all results, and the comments. Press “Details” on a teacher: a box opens over the page with the rating by course, by question and the comments of the students. “Export to Excel” puts everything of the page in one workbook, one sheet for each part, with the result of every teacher.",
        ], "coordinator/analytics", "Open Analytics"),
        new("How do I write an announcement?",
        [
            "Open Announcements and press “New announcement”. Choose what it is about (an exam, a holiday, the semester break, a teachers' meeting or other), write the title and the text, choose who reads it with the round buttons (everybody: the staff, the teachers and the students; the students; the teachers; the staff; or the staff and the teachers; a teachers' meeting is for the teachers) and, if you want, its Start date and its End date. The announcement is scheduled: it shows from its Start date to its End date (with no Start date it shows now, with no End date it shows until you delete it), and the people it is for see every day from Start to End in their calendar.",
            "To write many announcements at once press “Announcements sheet”: each row has a Title, a Message, what it is About, who it is For, and a Start and an End day. The Ready column tells you what is missing; press Post and every ready row is posted. “View announcements” (top right) opens a box with the summary: the announcements at the left and the holidays of the year at the right. You can edit or delete an announcement and delete a holiday from there, and “Export to Excel” in that box saves the announcements and the holidays as a file.",
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
            "Open Course library: the courses of the current term are cards, like the Courses of the teachers and the students. Use the search box and the lists Track, Grade level, Strand and Course to find one, then press Open (a course opens with a Course Menu at the left, where the Courses dropdown goes from track to strand to course without going back; the previous terms are not listed, because the library works on the current term; there is no Hide button and no list of hidden courses: every course of the term is there). Add the syllabus first, then the handouts (“Add many handouts in a sheet” in the same box opens a sheet inside the system, in the middle of the screen: a row for each handout with its title, its slides (optional) and its files (needed); the step by step is a toggle at the top; no Excel is needed. The teachers and the students get a Slides button when a handout has slides). Press a syllabus or a handout to open it and see its text and files.",
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
            "Press “Post summative exam to students” at the right of the lists of the library to post the exam of every course in the list (choose a course in All courses to post only that one), (there is no Post to students inside a course). The box shows only the Summative Exam Settings (time limit, anti-cheating watch, order, result) and “Opens on” and “Closes on”; choose them and press “Post to students” (Cancel and Post to students are at the right): the exam goes straight to the students and the system gives the scores. A course with no summative exam stops it: the page goes to that course and tells you. The teacher sees the scores in the gradebook.",
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

    /// <summary>Round 46: the Admission staff of Grade 11 and of Grade 12 (an account of a role the administrator made with the Admission pages ticked).</summary>
    public static HelpSection Admission() => new("For the Admission staff", "users",
    [
        new("What do I do here?",
        [
            "You record the information and the documents of the new students for the registrar. There is no application form on the home page: the students do not apply online, the school records them here. Your menu has the Dashboard, the page of your students (Grade 11 Students or Grade 12 Students), Calendar and Users.",
            "The Dashboard shows the students of your grade level (Admission Grade 11 sees Grade 11, Admission Grade 12 sees Grade 12), with their strand, section, phone number and state. At the top of every page the school year and the term that is going on are written, at the right of the greeting.",
        ], "staff", "Open the dashboard"),
        new("How do I record a student?",
        [
            "Open Grade 11 Students (or Grade 12 Students) and press “Record a student”. The box has two columns: the student at the left (LRN of 12 digits, names, sex, the strand the student wants, phone number, previous school, address) and the parents and the guardian at the right (at least one of the father, the mother or a guardian, with their phones and the address).",
            "Press Save. The student is in the table with the strand, the section (it shows once the registrar enrolls the student), the phone number, the documents kept, and the state: Recorded, then Enrolled. Use the search box and the lists of state and strand to find a student.",
            "“View student info” opens the same two columns to read only (it has an Edit button), and “Edit” changes the record.",
        ]),
        new("How do I keep the documents?",
        [
            "Press “Documents” on the row of the student. Every kind of document has its own line and there is nothing to choose from a list: PSA birth certificate, Form 138 (report card), Form 137, Certificate of completion (Grade 10), Good moral certificate, 2x2 photo and Other document.",
            "At the left of a line is the kind, at the center what the school has: No document, Paper in hand (with the date) or Document (a photo counts as a document), and at the right the buttons: “Add photo” (a picture or a PDF), “Edit” (the paper is in hand, change the photo or take it away) and “View document”, which shows the photo in a box without downloading it first.",
        ]),
        new("What happens next?",
        [
            "The registrar chooses the student in Enrollment (the LRN is shown, not typed, and the parents fill the printable form). When the student is enrolled the row shows Enrolled and the section. In Student's Accounts the registrar opens the same information and the documents (to read) in one box, with the information at the left and the documents at the right. You do not enroll students and you do not see grades.",
        ]),
    ]);

    private static HelpSection Guidance() => new("For the guidance counselor", "heart-handshake",
    [
        new("What do I look after?",
        [
            "You look after the concerns of behavior and attendance: absenteeism, violations, non-compliance and behavioral concerns. Failing grades and students at risk in a course are for the teacher.",
            "Open Referrals. Every student of the school is in the list, one line for each, so you can write a concern for anybody. Each line has the student, the section, the concern, what happened, the notes, the status, the schedule and a Solved button. Use the search box and the choices Show and Concern to find students. Teachers can also refer a student to you from the students of their course: that referral comes into this list with a notification.",
        ], "guidance/referrals", "Open Referrals"),
        new("Where do the attendance concerns come from?",
        [
            "Absenteeism is not made by the system any more: it depends on the teachers. A teacher refers a student from the students of the course, or from View attendance (Refer, for absenteeism only). You get a notification, and the student shows Absenteeism in the Concern list. The attendance of each course is marked by the teacher; the Analytics card “Absent by day” lists who was absent.",
            "To see only the students who have a concern, choose “Only students with a concern” in Show, or use “With concerns” or “Absenteeism” under Referrals in the menu.",
        ], "guidance/referrals?show=concerns", "Open the students with a concern"),
        new("How do I record a violation or follow up a concern?",
        [
            "On the line of the student, choose the concern from the Concern list: Absenteeism, Violation, Non-compliance or Behavioral. Then choose what happened from the list next to it. The concern is recorded, and the Concern list is locked until you press Solved.",
            "If what happened is not in the list, choose “Others”. A box opens in the middle of the screen where you can type it. The name of the student opens a box with the earlier concerns and a place for more words that only you can read.",
            "Non-compliance is for a student who does not do what the school asks (the work, the uniform and ID, the rules). Behavioral is about the way the student behaves toward the others. A teacher can refer a student with the same factors.",
        ]),
        new("How do I write the notes and the help I gave?",
        [
            "Press the Notes button of the line. It says “Noted” when there are notes and “None” when there are not.",
            "In the box, choose the help you gave from the list “What help did you give?”: Talk with the student, Call the parents/guardian, Talk with the teacher, or Others. Others opens a box where you can type the help in your own words. Then write your notes. Only you can read the notes.",
            "When you save, the student is told. Nothing pops up for you: the line just changes. When the case is over, press Solved.",
        ]),
        new("What does the student answer, and how do I set a schedule?",
        [
            "The Status column (at the right of Notes) says “Not seen yet” until the student presses OK on your message. Then it says “Seen”. If the student closes the box with the X (or Escape) without answering, it says “Ignored”.",
            "A student who ignored your message can open it again: it stays in the bell at the top, and pressing it shows your message again, with the buttons Going and Not going while the student has not answered. When you save your notes again, the student gets the message again and the status starts over.",
            "The Schedule column (at the right of Status) shows the answer of the student: “Going” or “Not going”. When the student is going, press “Set schedule”, choose the day and the time, and save. The student is told, and the day goes to the calendar of both of you (in Calendar, as Counseling). Press the day again to move it (the student is told that it has moved) or to take it away.",
        ]),
        new("Which students go to the referrals by themselves?",
        [
            "No student goes to the referrals by itself any more, not even for absences: a teacher refers the student (absenteeism, non-compliance, behavioral or violation), or you record the concern. Only the notes you write make the box appear for the student (a case that is closed or a schedule shows in the bell, without a box).",
            "In Analytics, the card “Absent by day” lists each day of the last 30 days with the students who were marked absent and their courses (weekdays only, because classes are Monday to Friday).",
        ]),
        new("How do I find people and make a group?",
        [
            "Open Users: Students and Teachers are everybody of the school, and Staff / Non-teaching are the registrar, the guidance counselor, the academic coordinator and the academic provider. A green dot means the person is online now. The button with the speech bubble writes to the person (it is only there when your role may write to them).",
            "Open Groups and press Add a group: give it a name and choose the people to add. Everybody in the group can write in it. Two groups are made for you: All staff (the registrar, guidance counselor, academic coordinator and academic provider) and Teachers and staff.",
        ], "users", "Open Users"),
    ]);
}
