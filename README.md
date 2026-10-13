# SEAITT-LMS

The e-LMS for SEAITT (Senior High School, Legazpi). Blazor Web App on .NET 9 with a MySQL database,
set up without a `dotnet new` template and with its own login (no ASP.NET Core Identity package).

What exists so far:

- **A public home page** at `/` for visitors: photos of the school, its courses (read from the database) and a
  **Log in** button.
- **Custom login** with the school's picture next to the form: email + password, PBKDF2 password hashing, cookie
  sign-in, account lockout, "keep me signed in". After signing in, everybody lands on the page of their role.
- **Users and roles**: Admin, Teacher, Student (with LRN), Registrar, Guidance Counselor, **Academic Coordinator** and **Academic Provider** (of the central team),
  each with its own table holding the person's name and details.
- **Registrar pages** (for the registrar only): *Dashboard*, *Enrollments* (the form that enrolls a student; the
  student's email and password are made automatically) and *Student's Accounts* (a searchable list).
- **Administrator pages** (for the administrator only): *Dashboard* (accounts, and who is **enrolled** or **not enrolled**) and *Users Management*: one list per role, with
  search, **edit**, **delete** and add (for staff).
- **Academic coordinator pages** (for the academic coordinator only): *Dashboard*, *Academic setup* (school years, terms, tracks, strands, courses and sections),
  *Teacher evaluation* (a questionnaire the students answer about their teachers), *Announcements* and *Analytics* (the results of the teacher evaluation).
- **Academic provider pages** (the central team; for the academic provider only): *Dashboard*, *Course library* (the syllabus, modules, activities, quizzes and task performances
  that are **sent to the teachers** as drafts: only the provider sends them), *Tracking* (did the teacher open it, post it, did the students open it; with a tab for the teachers and one for the
  students and what they opened) and *Analytics*.
- **For students and teachers**: *Users* (classmates, all students, friends and teachers / friends, students and all teachers), *Groups* (made for you by the system, and your own), *Resources* (a locker for your files and the files shared with you),
  an *Online students* box in the dashboard, and a **profile picture** in the settings menu.
- **More for the administrator**: *Security Logs*, *Database Backup & Restore* and *System Settings*.
- **A Help page** for every role (the link is above the profile card in the menu), and **pop-ups in the menu**: rest the pointer on a page and a box shows what is inside it.
- **The first page** (`/`, before signing in) lists **Our courses** by track: each track, its strands with their full names, and, when a strand is opened, the courses it studies by grade level and term.
- **Enrollment** by school year and **term**: tracks, strands, schedules (AM, MID, PM), subjects, sections,
  enrollments. The database comes with the school year 2026-2027, its two terms, the ACAD and TECH-PRO tracks, their
  strands and the subjects of grade 11.
- **A header** with a search bar in the middle and, at the right, **messages**, **notifications** and **settings**
  (light / dark / auto look, animations on or off). The signed-in person's name and role are at the bottom of the menu.
  Everything is phone-friendly.

Lessons, assignments and quizzes are next. The database design is in [docs/database.md](docs/database.md).

## Requirements

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- MySQL 8 with an empty schema named `seaittlms`, and a MySQL user that can create tables in it
- VS Code with the C# Dev Kit extension (recommended in `.vscode/extensions.json`)

## First-time setup

1. **Create your private settings file.** In the project folder, copy `appsettings.Local.example.json` to
   `appsettings.Local.json` (in VS Code: right-click the file, *Copy*, *Paste*, then rename the copy). Open it and
   replace the two placeholders:
   - `YOUR_MYSQL_PASSWORD` with your MySQL password,
   - `choose-a-long-password` with the password you want for the first administrator.

   Git ignores `appsettings.Local.json`, so your passwords never go to GitHub. **Never write passwords in
   `appsettings.json` or in `appsettings.Local.example.json`**: those files are committed and published (the example is
   only a template, the app does not read it). If a password ever ended up in a committed file, change that password.
2. **Run the app** in the project folder. In Development the tables are created automatically on startup:

   ```bash
   dotnet watch
   ```
3. Open http://localhost:5000. You see the public home page. Click **Log in** and sign in as
   **admin@legazpi.seaitt.edu.ph** with the administrator password from step 1. You land on the Administrator page.

If your MySQL uses another host, port or user, change the connection string in the same file. If the app says
**Can't connect to MySQL**, check the password in it, that MySQL is running, and that the `seaittlms` schema exists.

Other ways to give the app its passwords (the private file wins over all of them): `dotnet user-secrets set
"ConnectionStrings:Default" "..."` and `dotnet user-secrets set "Seed:Admin:Password" "..."`, or the environment
variables `ConnectionStrings__Default` and `Seed__Admin__Password`.

### The first administrator (seed account)

The first time the app starts on a database that has no administrator, it creates one: **System Administrator**,
email `admin@legazpi.seaitt.edu.ph` (the `Seed:Admin:Email` setting in `appsettings.json`), with the password you put
in your private settings file. To use other details, add them to the `Admin` part of that file before the first start:

```json
"Seed": {
  "Admin": {
    "Email": "principal@legazpi.seaitt.edu.ph",
    "FirstName": "Your",
    "LastName": "Name",
    "PhoneNumber": "09171234567",
    "Password": "choose-a-long-password"
  }
}
```

The seed only runs when there is no administrator yet, so changing these settings later does not touch an account
that already exists. The name and phone number are saved in the `administrators` table. To change the email of an
existing administrator, use **Users Management > Administrators > Edit** in the app, or run this in MySQL Workbench (emails are
stored in lower case):

```sql
UPDATE seaittlms.users SET email = 'admin@legazpi.seaitt.edu.ph' WHERE email = 'admin@seaitt.edu.ph';
```

### Creating and upgrading the tables

The app does it for you. Run `dotnet watch`: on a new empty `seaittlms` schema it creates every table, and on a
database from an older version it upgrades the tables **and keeps your data** (for example the names that used to be in
`users` are moved to the tables of each role). No SQL file is needed.

Prefer the command line? This does the same:

```bash
dotnet tool install --global dotnet-ef --version 9.0.20
dotnet ef database update
```

To see the tables as a diagram in MySQL Workbench use *Database > Reverse Engineer...* and pick `seaittlms`.

### Something broke? Start over

Use this when the database is broken, for example you dropped or changed tables by hand, an SQL script stopped halfway
("Table ... already exists" when the app starts), or MySQL Workbench shows *Error loading schema content ... Error Code:
1356 ... View 'seaittlms.v_...' references invalid table(s) or column(s)*. (An older version of the database had
`v_...` views that read `users` and the role tables, so they broke when those were missing or changed. The newest
version has no views.)

**Both ways erase all the data in `seaittlms`.** There is no undo.

**Quickest, needs only the app:**

1. In MySQL Workbench run `DROP DATABASE IF EXISTS seaittlms;`
2. Run `dotnet watch`. The app creates the database and every table again, and the administrator
   (`admin@legazpi.seaitt.edu.ph`, with the password from your `appsettings.Local.json`).

**Without running the app:** in Workbench open
[database/seaittlms_reset_and_rebuild.sql](database/seaittlms_reset_and_rebuild.sql) (*File > Open SQL Script...*) and
click the lightning bolt. It deletes the `seaittlms` schema and builds everything again, including the seven roles and
the school setup (school year, terms, tracks, strands, schedules, subjects). Then run `dotnet watch` to create the
administrator.

Any other users you had are gone. Add the staff again in **Users Management**, and the students by enrolling them.

**Sample accounts:** to get sign-ins to try the app (1 administrator, 1 registrar, 1 guidance counselor, **1 academic coordinator, 1 academic provider**, 10 teachers and 20 students), open
[database/seaittlms_sample_accounts.sql](database/seaittlms_sample_accounts.sql) in Workbench and click the lightning bolt.
It only adds what is missing (and gives the sample accounts of an earlier version the new email and password), so running it twice is harmless. The sign-ins are **easy on purpose**,
to try the system: the email is the last name (or the role) + `@legazpi.seaitt.edu.ph`, and the password is the role with a number:

| Who | Email | Password |
|---|---|---|
| Administrator | `admin@legazpi.seaitt.edu.ph` | `Admin123` (see the note below) |
| Registrar | `registrar@legazpi.seaitt.edu.ph` | `Registrar1` |
| Guidance counselor | `guidance@legazpi.seaitt.edu.ph` | `Guidance1` |
| Academic coordinator | `coordinator1@...` | `Coordinator1` |
| Academic provider | `provider1@...` | `Provider1` |
| Teachers | `vargas@...`, `villanueva@...`, `bautista@...`, ... | `Teacher1` (Vargas), `Teacher2`, `Teacher3`, ... `Teacher10` |
| Students | `delacruz@...`, `abad@...`, `alcantara@...`, ... | `Student1` (Dela Cruz), `Student2`, ... `Student20` |

The school has **one** administrator, registrar, guidance counselor, academic coordinator and academic provider, so the file never adds a second one and **never changes the password of the administrator you already have** (the last lines of the file, written as a
comment, can give that administrator `Admin123` too). The file holds only the hashes of the passwords, not the passwords. Give real people a harder password in **Users Management > Edit**.

**Sample data:** the accounts have no courses or enrollments yet (the students are listed under *Not enrolled*). To fill the tables with practice data, run
[database/seaittlms_sample_data.sql](database/seaittlms_sample_data.sql) **once**, after the accounts file (open it in Workbench, press the lightning bolt, wait a minute). It enrolls the 20 students in
**four groups of 5** (Grade 11 and Grade 12, each in the ACAD track, strand BUSSENT, and in the TECH-PRO track, strand HT1; section A, AM schedule), gives the 10 teachers their courses (30 courses), and adds, with dates around the day you run it: the library of the academic provider and what it sent to every teacher (some opened, some
still waiting, some posted), modules, activities and quizzes with questions, who opened what, the work handed in and graded, attendance, announcements, messages, notifications, friends,
the automatic groups and three groups the students made, files in the lockers, the security log, the settings, a syllabus (PDF) and a notes file attached to each course of the library and to the teachers' copies, announcements of every category
(an exam, a holiday, the semester break, a teachers' meeting, others, and two from a teacher to a class) and one teacher evaluation with every kind of question and its answers. It adds nothing if any of these students is enrolled already (so a second run does nothing).
To start over: run the reset file, then the accounts file, then this one.

Only want to clear the Workbench error without erasing anything? Run this in Workbench (it only drops the five old views,
and does nothing if you don't have them):

```sql
DROP VIEW IF EXISTS seaittlms.v_administrators, seaittlms.v_guidance_counselors, seaittlms.v_registrars,
                    seaittlms.v_teachers, seaittlms.v_students;
```

If Workbench stops an SQL file with *Error Code: 1175 ... safe update mode*, your copy of the file is old: pull the
latest version (current files work with Workbench's default settings).

The reset file is generated from the migrations, don't edit it by hand. After a schema change, regenerate it with
`bash database/export-schema.sh` (on Windows, in Git Bash; it needs the `dotnet-ef` tool and a running MySQL).

### Production

Set `ConnectionStrings__Default` and `Seed__Admin__Password` as environment variables (and `Seed__Admin__Email` if
you want a different one) and run `dotnet ef database update` (or `Database__MigrateOnStartup=true` for the first
start). Don't copy `appsettings.Local.json` to a server. The sign-in cookie is https-only outside Development.
Persist the ASP.NET Core Data Protection keys if you run in a container.

## The header

| What | How it works |
|---|---|
| **Search** (middle) | A box that opens a **pop-up** (there is no search page): results appear as you type, in groups: the pages you can open and, depending on your role, your courses and the people you work with. Ctrl+K (Cmd+K) opens it from anywhere, Enter opens the first result, Escape closes it. An administrator finds every account, a registrar finds students, a teacher finds the students of their courses (the hit opens the gradebook of that course) and the guidance counselor finds students (the hit opens the referrals page for that student). |
| **Messages** (speech bubble) | A box with your conversations. Type a name to write to somebody. A student can write to teachers, the guidance counselor, the registrar and their friends; a teacher also to students and the other staff (the academic provider too). **Messages can't be sent from 7:00 PM to 7:00 AM Philippine time** (the administrator can change the hours or turn the closing off in System Settings): the box says so and the message field is switched off. Reading is always possible. The number on the icon is how many messages are unread. |
| **Notifications** (bell) | What the system wants you to know (a new message, and later new modules, grades and deadlines). The number is how many are new. Click one to open the page it is about. |
| **Settings** (gear) | Your **profile picture**, then **Light**, **Dark** (black) or **Auto** (follows the computer), and **Animations** on or off. The choice is kept in your browser **and in a cookie**, so the server draws the same look on every page (a page that is opened by a click on a link used to go back to light). |
| **Name and role** (bottom of the menu) | Who is signed in. The arrow button signs you out. |

The code: `Components/Layout/HeaderTools.razor` (the three buttons), `MessagesPanel.razor` (the Messages box),
`Services/MessageService.cs` (the rules and the 7 PM to 7 AM closing time), `Services/NotificationService.cs`.

## Everything keeps up by itself (no F5)

- **Messages and notifications** arrive at once: when somebody sends a message or something happens for you, the number on the Messages button, the bell and
  the open conversation change by themselves (`Services/LiveUpdates.cs` tells the pages that are open; they also look again every 5 to 10 seconds, in case the news
  came from somewhere else).
- **The dashboards, the calendar, the lists of courses, the grades, the analytics and the registrar's student list** look again every 30 seconds
  (`<AutoRefresh />`, the script at the end of `wwwroot/app.js`). It only changes what is different, keeps the place on the page, and waits while you are typing, a pop-up is
  open or the tab is hidden. The calendar, due dates and announcements in the column at the right look again every 30 seconds too.

## Who sees what

After signing in (and whenever a signed-in person opens `/`), the app sends everybody to the page of their role
(`Auth/RoleHome.cs`). A page that a role may not open shows *Access denied*. The menu only lists what the role can open.

| Role | Lands on | Can open |
|---|---|---|
| Admin | `/admin` | Dashboard (with **Enrolled** and **Not enrolled** under *Accounts*), **Users Management**, **Archive Accounts**, **Analytics**, **Security Logs**, **Database Backup & Restore**, **System Settings** |
| Academic coordinator | `/coordinator` | **Dashboard**, **Academic setup**, **Teacher evaluation**, **Announcements**, **Analytics** (teacher evaluation). It no longer sends course materials. |
| Academic provider (central team) | `/provider` | **Dashboard**, **Course library** (and sending), **Tracking** (with the **Teachers** and **Students** tabs), **Analytics** |
| Registrar | `/registrar` | Dashboard, **Enrollments**, **Student's Accounts** |
| Teacher | `/teacher` | **Dashboard**, **Courses** (modules, activities, quizzes, task performances, grading), **Attendance**, **Calendar**, **Announcements** (to their classes), **Gradebook**, **Analytics**, **Users**, **Groups**, **Resources** (in this order in the menu) |
| Student | `/student` | **Dashboard**, **Courses** (modules, work, quizzes), **Calendar**, **Grades**, **Users**, **Groups**, **Resources** (in this order in the menu) |
| Guidance counselor | `/guidance` | **Dashboard**, **Referrals** (with the grade level and course of each student), **Analytics** |

Every role also has **Help** (`/help`, the link above the profile card in the menu): the answers to the usual questions of the role, with the steps.

There is no "My account" page: people sign in, work, and sign out. Somebody whose role has no page of their own (it should not happen) is sent to *Access denied*.

The registrar pages belong to the registrar: **an administrator cannot open them**. To get a registrar, sign in as the
administrator, open **Users Management > Registrars**, press **Add registrar** and give the person an email and a first
password. Then sign in as that registrar to enroll students.

## Courses, modules and work

(In the app the subjects of the school are called **courses**. The database tables keep the name `subjects`.)

**The administrator** gives each teacher their courses: **Users Management > Teachers > Courses**, choose the section and
the course (only the courses of that section's strand are listed). A course of a section has one teacher. A new teacher can get
their track, strand, section and courses right away in the **Add teacher** form (see *Users Management* below).

**Teacher > Courses** shows one colored card per course (a teacher with more than one section also gets a chip per section to show one
section at a time). The two buttons at the right of the chips **change the view**, like the student's page: **Cards** (with the calendar at the right) or a **List** (course, section,
students, modules posted with a bar, what is to check). **Open** shows its modules. The **syllabus** and the modules the academic coordinator sent are drafts here too, marked
*From the academic coordinator*. A new module is a **draft**: students
cannot see it. **Post now** shows it to the students (they get a notification), **Schedule** picks the day and time it
appears by itself, **Hide** takes it back. At the bottom of a module are its **Activities**, **Quizzes** and **Task
performances**; each has a due date (and can have its own time to appear). Open an item to see who handed in, read the
work (with the formatting the student gave it), give a score and feedback (the student is told).

**Modules are closed lists.** On a course page (the teacher's and the student's) every module is a line with its name: **press the name** to open it and see its activities, quizzes and task
performance (and its buttons); press it again to close it. Several can be open at once. Code: `Teacher/Subject.razor`, `Student/SubjectModules.razor`.

**Quizzes** have questions (multiple choice, true or false, short answer) that are graded by themselves, an optional time
limit, shuffled questions and choices, and the **anti-cheating watch**: while a student takes the quiz the page tells the
system when they leave the tab or window, copy, cut or paste (those are blocked too). Each of those is a warning; after
the number you choose the quiz is handed in by itself and **flagged** for you, with a log of what happened. The time is
checked on the server, and a quiz that was left open is handed in when its time is over.

**Student > Courses** shows the courses they are enrolled in as cards (or a list). The chips are **All**, **Enrolled** (the term has not ended) and **Completed** (the term is over, or the enrollment was completed), each with its number. The **list** has the columns course, teacher, section, schedule, term, status, modules and work, **progress**, next due, grade and attendance. The **progress** (a bar and a percent, also on every card)
is how far along the student is: the modules they opened and the work they handed in, out of everything posted so far. **Open** shows the
posted modules; each ends with three tabs: **Activities**, **Quizzes**, **Task performance**. **Read lesson** opens the
module's text. A module that was never opened is marked **New**.

**Your work: the editor.** An activity or a task performance is answered in **an editor like a word processor** (`Components/Shared/RichTextEditor.razor`, script `seaittEditor` in `wwwroot/app.js`).
The bar above the box has: undo and redo, the **paragraph style** (normal, heading 1 to 3, quote), the **font** (Arial, Calibri, Cambria, Georgia, Times New Roman, Verdana, Courier New) and its **size** (8 to 48),
**bold, italic, underline, strikethrough, subscript, superscript**, the **text color** and the **highlight color**, clear formatting, **alignment** (left, center, right, justify), **bullets and numbering**, decrease and
increase **indent**, the **line spacing**, a **table** (3 × 3; Tab moves between its cells and adds a row after the last one), a **link** and a **line**. The tools that are on where the cursor is look pressed, and under the box are the **word and character count**
(at most 20,000 characters). Pasting keeps the words only (no pictures or fonts from other pages come in). The answer is saved with its formatting in `submissions.answer_html` (and the plain words in `answer_text`);
**the server cleans it again** (`Services/RichText.cs`: only a short list of tags, links and style rules is kept; scripts, pictures, frames and event handlers are taken out), so nothing from the browser is trusted. The student sees it
again in the editor to change it until it is graded; the teacher sees it with its formatting. Work handed in before the editor existed opens as paragraphs. **Quizzes with questions** are different: the questions are answered one by one on the quiz page and graded by the system. A **quiz that is only a file** (the provider or the teacher attached the questions as a file and added none) is answered in the same editor, in a card called *Your answers*, and the teacher reads it and gives the score by hand, like an activity.

The code: `Services/TeachingService.cs`, `ModuleService.cs`, `WorkService.cs`, `QuizService.cs`, `AutomationService.cs`
(the background job), the pages in `Components/Pages/Teacher/` and `Components/Pages/Student/`, and the anti-cheating watch in
`wwwroot/app.js` (`seaitt.startQuizGuard`).

## Dashboards, calendar and the widgets at the right

**Student > Dashboard** (`/student`) shows what to do first. At the left: the teacher **evaluations** to answer and then **four cards in two columns and two rows**: at the top left the **Modules not opened**, at the top right the **Due dates** (the work to do, overdue ones in red), at the bottom left the **Recent grades** and at the bottom right the **Progress** (every course with its bar, a link to the course). Under them are the **achievements**: fourteen to earn (perfect score, high grades, quiz ace, always on time, curious mind, perfect attendance and first step, quiz taker, triple perfect, all-rounder, early bird, bookworm, top of the class, nothing missed): earned ones are colored, the others show how far you are. **When a student earns a new achievement, a box opens in the center of the screen** to celebrate it (several at once are shown one after the other, with *Next*); once it was shown it never opens again (the award is written in the `achievement_awards` table with the time it was seen; the achievements themselves are always worked out from the scores). At the right, in this order: the **calendar**, the **announcements**, the **online students** and the **online teachers**.

**Teacher > Dashboard** (`/teacher`) shows the **unchecked activities** (work handed in that still needs a score), the
modules students can't see yet (drafts and scheduled ones) and the number of flagged quizzes (it links to the calendar, where the flagged attempts are
found). At its right are the calendar, the **online students** and the **online teachers**.

The column at the right of some pages (`Components/Shared/AsideWidgets.razor`) holds the widgets that page needs, in this order. The **student
dashboard** has the **calendar** first (a dot on every day with something, click a day to see it), then the announcements, the online students and the online teachers. **Online teachers** and **online students** list the people you can reach
who have the app open right now (a student sees their friends, classmates and teachers; a teacher their friends, students and colleagues), with a link to the Users page. **Show more:** any card at the right that lists items (announcements, activities, unassigned activities, due dates, unchecked activities, the people online) shows the first 5 and, when there are 6 or more, a **Show more (N)** button at the bottom that opens the rest (and **Show less** closes it again). The
student's **Courses** pages also have a **Due dates** list. The teacher's
**Courses** page has the calendar and *Needs your attention*. The grades pages and the teacher's attendance page have no column. On a phone the widgets
go under the page.

**Calendar** (`/calendar`, students and teachers): a **Month** and a **Week** view with Previous, Today and Next, and an **Add event** button. It shows the
deadlines of the work (activity, quiz, task performance), the modules that appeared (for a teacher also the ones scheduled), the
modules posted and **the events the person wrote** (violet). Done work is struck through, overdue work is red. Click a day number to open its week. **The week** has seven columns, Sunday to Saturday: a day with something on it says **what** in a few words ("2 activities", "1 quiz", "1 event") and a day with nothing says so. **Click the date (or the words) and a box opens** with everything on that day, each line a link: an activity, quiz or task performance opens its page (a teacher sees who handed it in), a module opens the course, an event opens to be changed. In the week view the **Unchecked activities** and **Unassigned activities** cards are shown **in two columns** side by side. At the right a student has an **Activities** card (the work to do, with its due text) and then *Due dates*; the Next button has its `>` icon. A **teacher clicks** an activity, quiz or task performance to see **who handed it in** (and who did not), with the score and, for a quiz, the anti-cheating warnings: a flagged attempt is marked in red. *Open and grade* goes to the page of the work. Code: `CalendarWorkDialog.razor`, `CalendarDayDialog.razor`.

**Add event.** The button opens a box with a title, a day, an optional time (empty means all day) and an optional note. An event is **private to the person who wrote it**: it shows on their own calendar only (never on a classmate's or on the teacher's), can be opened to **change or delete** it, and a person can keep up to 1,000. The links are plain addresses (`/calendar?event=new`, `?event=12`, `?day=2026-10-06`), so the boxes also work after a reload. Code: `CalendarEntryDialog.razor`, `Services/CalendarService.cs` (table `calendar_entries`).

**Teacher > Calendar: unassigned activities.** The card **Unassigned activities** at the right lists the teacher's activities, quizzes and task performances that have **no due date**. **Drag** one onto a day (month or week view) and a box opens with that day (5 PM Philippine time) as the due date, where the teacher can also change the title, instructions, highest score, when it appears and, for a quiz, the time limit and the **anti-cheating** settings (watch on or off, warnings before it is handed in, shuffling, showing the result). **Save** puts it on that day for the teacher and the students; **Cancel** leaves it unassigned. The **Set date** link next to an activity does the same without the mouse. Code: `Components/Pages/Account/Calendar.razor`, `ItemScheduleDialog.razor`, the drag script in `wwwroot/app.js`.

**Student > Grades** (`/student/grades`) is the grade and progress page: one **card per subject**, closed at first, showing the subject, its average (points earned divided by
points possible of the graded work) and the grade. **Press the subject** to open its progress (a bar with the passing mark of 70 drawn on it, the number of works handed in and graded) and its graded work, with a link to the course. A grade under 70 is shown in red with a warning; a quiz whose result the teacher hides is not counted yet.
(The separate "Progress by course" section is gone: the progress lives in the subject's card.)

The code: `Services/DashboardService.cs`, `CalendarService.cs`, `GradeService.cs`, `AchievementService.cs`.

## Attendance and the gradebook (teacher)

**Teacher > Attendance** (`/teacher/attendance`): choose the course and the date, press **Present**, **Late** or **Absent** for each
student (or **Mark all present** first), then **Save attendance**. An earlier day can be opened and corrected; a day that has
not come yet can't be marked. Under it the **Attendance summary** shows each student's present, late and absent days and the
attendance rate (days present or late divided by days marked; under 80% is red).  Students see their rate on **Grades**, and five days all present earn the *Perfect attendance* badge.

**Teacher > Gradebook** (`/teacher/gradebook`): choose the course; every student is a row, every activity, quiz and task
performance of a posted module is a column (with its highest score), and the last column is the average. A score under 70% is
red, work waiting for a score says *To check* (click it to open the work). **Edit grades** turns a row into boxes; **Save**
changes the scores (all of them are checked first, a wrong one saves nothing) and tells the student. Feedback the teacher wrote
is kept.

## Guidance counselor: referrals

Nobody has to send a student to the guidance counselor: **the system refers them by itself**. It looks at every student (every minute, and when
the counselor opens a page):

| Problem | When |
|---|---|
| **Failing grades** | the grade in a subject is **below 70** |
| **At risk** | the grade is from 70 up to 75, or **three or more works are missing** (due, posted, not handed in) |
| **Not attending class** | attendance is **under 80%** over at least 5 marked days |

Each problem becomes one referral for that student and subject (it is not repeated, and not sent again for 14 days after it was resolved) and the
counselor gets a notification. **Guidance > Dashboard** shows the new referrals and the students below 70. **Referrals** lists them with the student, the **grade level** (a narrow column), the **section** and the **course** (the strand) in the three columns next to it, then the subject and its teacher, the problem, the **grade** and the status, with filters (status, problem, grade level) and **one search bar** that looks in all of these at once: type any of **the name or LRN, the grade level (11 or 12), the section, the course (the strand), the subject, the teacher or the problem** (failing, at risk, attending), and several words together all have to match, for example `garcia accounting 11 failing`. Press **Work on it** to set the status (*Open*, *In progress*, *Resolved*), the help given (talked with the student,
called the parents, tutoring ...), notes only the counselor reads and, when resolving, how it ended (*Improved* or *No change*). A student is told
when the counselor starts. (There is no separate "Students at risk" page: the cards of the dashboard and the menu open **Referrals** already filtered by problem.)

## Analytics

**Analytics** (`/analytics`) is for the administrator and the guidance counselor (the whole school) and for a teacher (only their subjects). It shows
the number of students, the average grade, the attendance rate, how many students are failing or at risk, the grades by band for each grade level
(each subject for a teacher), the **attendance** (the rate, how many were present, late and absent, and the rate of each section, or of each course for a teacher; there is no monthly trend any more), the **primary concern categories** (referrals by problem),
the **intervention effectiveness** (how many resolved referrals improved, by the help given) and the performance by strand. Everything is worked
out from the scores and the attendance that are recorded. (A teacher's page has no attendance card: the teacher's own attendance page and gradebook have it.)

## Graduation (archive)

**The archive is automatic.** When the **last term of a school year has ended** (the day after its last day, Philippine date), the system archives, by itself, every grade 12 student who was enrolled in that term: they are marked *Graduated*
with the note "Graduated: <term> ended (automatic)", their enrollments are completed, referrals end and their sign-in is switched off. It runs in the background job every minute, is safe to repeat, and writes one line in the Security Logs
(*Students archived*). Code: `StudentLifecycleService.ArchiveFinishedAsync`. The page below is still there to archive somebody earlier, and to restore.

**Administrator > Archive Accounts**: the grade 12 students who are still at school are listed. Tick the ones who graduate (or **Select all**), add a note
("Class of 2027") and **Archive selected**: they are marked *Graduated*, their enrollments are completed and their **sign-in is switched off**; their
records stay. The *Archived* tab lists the graduates and can **Restore** one (the account works again; the registrar enrolls
the student again for a class). A student in the archive can't be enrolled until an administrator restores the account.

There is no withdrawal feature any more (the student page, the registrar page and the table were removed). A student who was *Withdrawn* before is
a student at school again (status *Active*) after the update; their sign-in stays switched off until an administrator turns it on in Users Management.

## The menu, profile pictures and Help

- **Pop-ups in the menu.** On a computer, rest the pointer on a page of the menu (or move to it with the keyboard) and a box opens beside it showing what is inside the page: a tab, a filter or
  a short list (for *Courses* of a student: **Enrolled** and **Completed**; for *Users*: the tabs; for *Tracking*: the states). A line with a link goes straight to that part of the page. The box closes when the
  pointer leaves, and after you click a link. On a phone there is no box (the menu is a drawer). Code: `Components/Layout/NavItem.razor`, the lists in `NavMenu.razor`, the position in `wwwroot/app.js`.
- **Smaller design.** The whole page is drawn about 12% smaller (`font-size: 87.5%` on the page, and every size is written in `rem`, so changing that one number scales everything).
- **Profile picture.** In the **settings** menu (the gear) press *Change picture*, choose an image (JPEG, PNG, WebP or GIF); the browser shrinks it to at most 256 by 256 pixels (under 1 MB) and the server
  keeps it in `profile_photos`. It shows on every avatar (menu, messages, lists, groups). *Remove* goes back to the initials. Code: `Services/ProfilePhotoService.cs`, the picture is served at `/photos/{userId}`.
- **Help** (`/help`): the link is above the profile card at the bottom of the menu. The first part is for everybody (signing in, forgetting the password, messages, the pictures), the second for the role of the
  person, with the steps and a button to the page. The text is in `Components/Pages/Account/HelpContent.cs`; when a page changes, change what is written there.

## Users, groups and resources (students and teachers)

| Page | What it does |
|---|---|
| **Users** (`/users`) | A **student** has the tabs *Classmates* (the students of their section), *Students* (**all** the students of the school), *Friends* and *Teachers* (of their courses); a **teacher** has *Friends*, *Students* (of their courses) and *Teachers* (**all** the other teachers). Search, show only who is **online**, write a message, send a **friend request** (the other accepts or declines; you can take it back; *Unfriend* asks first). Friends can write to each other even when their roles would not otherwise allow it. |
| **Online students** | A box in the right column of the student's and the teacher's dashboard: the students who have a page open now (a page counts as open for 45 seconds after its last sign of life). Code: `Services/PresenceService.cs`. |
| **Groups** (`/groups`) | Some groups are **made by the system** and kept up to date by itself (marked *Automatic*): a **student** is in *Grade 11 students* (or their own grade level), the students of their **course** (strand, such as *BUSSENT students*, both grade levels) and *All students*; a **teacher** is in *Grade 11 teachers* (a group for each grade level they teach in, this school year) and *All teachers*. Nobody owns or leaves those. **Add a group** makes your own study group or team of students and teachers: the person who makes it is its owner and adds or removes people and can delete it; anybody can leave. Every group has a **wall** (posts and replies), its **members** and the **files** shared with it. Code: `GroupService.SyncAutomaticAsync` (`study_groups.auto_kind` and `auto_key`). |
| **Resources** (`/resources`) | **Locker**: your own files (upload several at once; rename, delete, **share** with people, a group or one of your courses). **Uploaded files**: what others shared with you (a teacher can share a file with a whole course); *Keep a copy in my locker*. A file is at most 10 MB and a locker 100 MB (both set in System Settings). Files are kept in the database. A **PDF, a picture, a text file, a sound or a video opens in a new tab of the browser** (the browser's own viewer, with its download and print buttons; *Open*), anything else is downloaded (*Download*), and web pages and SVG pictures are never shown (they could run as part of the site); program files (`.exe`, `.bat`, `.js`, ...) are refused. Code: `Services/FileService.cs`, `Services/FilePreview.cs`. |

The code: `Services/PeopleService.cs`, `FriendService.cs`, `GroupService.cs`, `FileService.cs`, `Components/Pages/Account/Users.razor`, `GroupList.razor`, `GroupPage.razor`, `Resources.razor`.

## Administrator: Security Logs, Database Backup & Restore, System Settings

**Security Logs** (`/admin/security-logs`) lists who signed in, who typed a wrong password (and from where), locked accounts, accounts that were added, changed, switched off or deleted, new passwords, backups and changes
of the settings, newest first, with the time, the email, who did it, the address and the browser. Filter by group (sign-ins, failed sign-ins, account changes, system), kind, dates and words; **Download CSV** gives the same list
for Excel; **Clear old lines** deletes the older ones (the last 30 days are always kept, and the system clears by itself the lines older than the number of days in System Settings). Code: `Services/SecurityLogService.cs`.

**Database Backup & Restore** (`/admin/backups`) saves a copy of **all the data** as an SQL text file in the `Backups/` folder next to the app (git ignores it), made from one consistent snapshot of the database.
*Back up now* makes one; each backup can be **downloaded** or **deleted**; a backup file from another computer can be **uploaded**. **Restore** asks you to type `RESTORE`, first makes a safety backup of what is there now,
checks that the file is one of ours (only our tables and plain statements are accepted), puts the data back while the system is closed to everybody except administrators, and **goes back by itself** if something fails.
An automatic backup at an hour you choose (and how many to keep) is set in System Settings. Code: `Services/BackupService.cs`, `MaintenanceWorker.cs`.

**System Settings** (`/admin/settings`) changes, without editing files: **maintenance mode** (while it is on only an administrator can use the system; everybody else sees a page with your message), and
the **automatic backup** (the hour and how many to keep) and how long the **log** is kept. Every change is written in the security log. (The other values the system uses, the name of the school, the wrong passwords before a
lock and the minutes locked, the hours messages are closed, and the size of a file and of a locker, keep their usual values; they are in the `system_settings` table if you ever need to change one.)
Code: `Services/SystemSettingsService.cs`; the values are kept in the `system_settings` table and read once when the app starts.

**Files attached to course materials.** A syllabus, a module and every activity, quiz or task performance can have up to 10 files (at most the size of System Settings, 10 MB at first): the provider attaches them
in the library, they are **copied to the teacher's draft** when it is sent, the teacher can add and remove files in their copy (and in what they write), and the students of the course open them once the module is
posted. The same rule shows a file in the browser or saves it (`/files/a/<id>`; `?download=1` always saves it). Only the people who may see a module can open its files. Code: `Services/AttachmentService.cs`,
`Components/Shared/AttachmentList.razor`, `AttachmentPicker.razor` (tables `attachments` and `attachment_contents`).

## Academic setup (academic coordinator)

**Academic coordinator > Academic setup** (`/coordinator/academic-setup`) has one tab per part of the setup, each with its number. The coordinator
(and nobody else: the administrator no longer changes it) can **add, edit and delete**:

| Tab | What it holds and the rules |
|---|---|
| School years | Name (like 2027-2028), first and last day. One with terms or sections can't be deleted. |
| Terms | School year, number, name, dates (inside the school year) and **the current term** (making one current takes it away from the other: only one term is current). The term after the current one is the **next term**. A term with enrollments can't change its number or school year and can't be deleted. |
| Tracks | Code, name, offered or not. One with strands can't be deleted. |
| Strands | Code, name, track, **the schedules** it has classes in (at least one), offered or not, and **the courses of the strand** (the **Courses** button: pick grade level and term, add or remove courses). A strand with students can't move to another track; a schedule that a section uses can't be taken away; one that is or was in use can't be deleted (switch it off). |
| Courses | Code, name, description, in use or not. One in a strand's courses or taken by students can't be deleted. |
| Class schedule | The hours of **AM, MID and PM**, and **the days and the time of each course of a section** (see below). The number on the tab is how many courses have class times. |
| Sections | School year, grade level, strand, schedule (one of the strand's), name and adviser. One with students or teachers can only change its name and its adviser. The list shows the **schedule (AM, MID or PM) next to each section**, because *A* can exist once in each schedule of the same strand (that is not a duplicate), and can be filtered by grade level, strand and schedule. |

The courses a student takes are **not picked at enrollment**: they are the courses of the strand for that grade level and term, as set here. Changing the
courses of a strand changes the enrollments made from then on; students already enrolled keep theirs. Code: `Services/SchoolSetupService.cs`.

**Class schedule** (the *Class schedule* tab). Every section meets in a part of the day, **AM, MID or PM**, and now each of those has **hours** (they start as AM 7:00 AM – 12:00 PM, MID 10:00 AM – 3:00 PM and PM 1:00 PM – 6:00 PM). The first card of the tab changes them
(it must end after it starts, last at least one hour and stay between 5 AM and 10 PM; a shorter day that would leave a class outside it is refused and names the class). The second card sets **when each course meets**: choose a section (the ones of the school year that is running come first,
each with *how many of its courses have a time*) and, for each of its courses (the courses of its strand at its grade level, by term, with the teacher when there is one), choose the **days**, **MWF** (Monday, Wednesday, Friday) or **TTH** (Tuesday, Thursday), and **when the class starts and ends**, then press *Save* (*Clear* takes the time away).
The rules, checked again in the database before anything is saved:

* the time must be **inside the hours of the section's schedule** (the section 11-BUSSENT-A is AM, so 7:00 AM – 12:00 PM), and a class lasts from 30 minutes to 4 hours;
* **two courses of a section can't meet at the same time** on the same days **in the same term** (a 1st-term course and a 2nd-term course can use the same hours, they are never taught together; MWF and TTH never overlap);
* **a teacher can't be in two classes at the same time** (same school year, same term, same days, overlapping hours): it is checked when the time is set and when the course is given to the teacher (Users Management > Give the course);
* only the academic coordinator can set or clear them; a section that moves to another strand, grade level, schedule or school year starts again without class times, and deleting a course takes its class times with it.

The class times show as **MWF · 8:00 AM – 9:30 AM** on the teacher's *Courses* (cards and list), on the student's *Courses* (cards and list), on the registrar's enrollment receipt and on the **enrollment form to print** (a *Days and time* column next to each course). A course with no time simply shows none. Code: `Services/CourseScheduleService.cs`, `Components/Pages/Coordinator/Setup/ScheduleTab.razor` (tables `schedules`, `course_schedules`).

**Grade 11 and grade 12, sections A to C.** Both grade levels have courses: the migration `AddGrade12CurriculumAndSectionsAToC` gives every strand the grade 12 version of its courses (the grade 11
name with a **2**: BUSINESS ACCOUNTING 2, ADVANCE MATHEMATICS 2, ... and six common courses in the 2nd term), and the database comes with sections **A, B and C** for the current school year, for
every schedule of every strand, at grade 11 and grade 12 (sections D and E were removed; one that has students or a teacher would stay). The registrar **chooses** the section from a list when
enrolling a student (A to C, plus any other section that exists) and does not type it; the coordinator can still make a section with another name in the *Sections* tab.

**Administrator > Enrolled and Not enrolled** (`/admin/enrollment/enrolled`, `/admin/enrollment/not-enrolled`) are two lists the administrator can only look at (the registrar enrolls the
students). They are the **Enrolled** and **Not enrolled** cards under *Accounts* on the administrator's dashboard.

## Announcements

**Announcements** are about **exams, holidays, the semester break, teachers' meetings and others** (each has its icon and color).

* The **academic coordinator** (`/coordinator/announcements`) writes the notices of the school: pick what it is about, write the title and the text, and choose who reads it: **everybody** (students and
  teachers), **the students** or **the teachers** (a *teachers' meeting* is always for the teachers), with an optional *show until* day. The page can show all of them or one kind at a time (tabs), and the coordinator
  edits and deletes them. The people it is for get a notification when it is posted (editing does not notify again).
* A **teacher** (`/teacher/announcements`, after *Calendar* in the menu) writes to **one of their own classes**: choose the course (subject and section), *Exam* or *Other*, and write it. Only the **students of that
  course** see it and are told. The page has two tabs: **To my classes** (the teacher edits and deletes their own) and **From the school** (the notices of the coordinator for the teachers).
* There is no announcements button in the header any more: students see them in the **Announcements** card at the right of their dashboard (the title opens the whole text); the teacher's and coordinator's pages list them. A teacher's announcement form no longer asks "What is it about?": a class announcement is just a title and text (its kind is *Other*; the coordinator's announcements keep their kinds).

Announcements that an administrator or a registrar wrote before these rules stay in the database; the coordinator can still edit and delete them. Code: `Services/AnnouncementService.cs`
(`announcements.category` and `announcements.assignment_id`).

## Academic provider: course library, sending and tracking

The **academic provider** belongs to the central team. They prepare what the teachers teach with and watch whether it reaches the students. (The academic coordinator used to send
course materials; that was **taken away from the coordinator** and is only for the provider now.) An administrator adds providers in **Users Management > Providers**; there can be several.

**Course library** (`/provider/library`) is the provider's library. It opens on the **tracks** (Academic, Technical-Professional), each one a **row like a module of a course** (a numbered square, the name in bold, a line under it and an *Open* button, not a card); press a track to see its **courses** (the strands: ABM, STEM, HUMSS ...) the same way, and press a course to see its **subjects** as rows (with where it is studied, *Grade 11 · 1st Term*, a *Syllabus ready* or *No syllabus yet* badge and what is prepared: modules, activities, quizzes, task performances, teachers), one library per subject; a search bar over the tracks looks in all the subjects of the school. Open a subject to prepare its **syllabus** (one per course), its **modules**
and the **activities, quizzes and task performances** in each module (a suggested due date can be given). Open a syllabus or a module (press its title) to see its text, its files and
its activities, quizzes and task performances. **Files** (a PDF, pictures, a text file, a Word document ...) can be attached to the syllabus, to a module and to every activity, quiz or task performance.
A **quiz** is **questions, a file, or both** (the provider no longer sets the quiz settings: the time limit, the anti-cheating watch and the rest are the **teacher's** to set in their copy).
**Send to teachers** chooses what to send (the syllabus is the first thing on the list; if the subject has none, the dialog says so and offers to add it) and which teachers' courses (the teachers
who have the course this school year): each teacher's course gets **its own copy as a draft, with the files**. The students can't see a draft: the teacher changes what they want (the text, the points, the due
dates, when it is posted) and posts it. The teacher is told (the bell) and the copy says *From the academic provider*. A module sent to a course is not sent to it again; when the teacher deletes
the copy, it can be sent again. The syllabus comes first in the course and has a *Syllabus* badge for the students. Code: `Services/MaterialService.cs`, `Components/Pages/Provider/CourseLibrary.razor`.

**Tracking** (`/provider/tracking`) has three tabs: **Modules** (one line for each thing sent to one teacher's course, with its **state**, below), **Teachers** and **Students** (what used to be the Users page):

| State | Meaning |
|---|---|
| Not opened | Sent, and the teacher has not read it, edited it, opened one of its activities or posted it. (Only looking at the list of modules of the course does not count.) |
| Opened · draft | The teacher opened it and it is still a draft. |
| Scheduled | The teacher chose a later time for the students to see it. |
| Posted | The students can see it. The line then shows how many of the course's students opened it, and how many opens its activities had. |

The tabs count each state (the menu pop-up of *Tracking* links to them), and you can search a teacher, a course, a section or a module, narrow the lines with the lists **Teacher**, **Grade level**, **Section** and **Course** (they offer what was sent) and show only syllabi or only modules. The Teachers tab narrows by grade level, section and course, and the Students tab by grade level and section. **Details** of a line shows the steps
(sent, opened by the teacher, posted, opened by the students) with their dates, each activity (opened by, handed in by) and **each student** (who opened the module and when, how many activities),
with the students who have **not** opened it first and a switch to show only them. A student opens a module (or the syllabus) when they open its page, and an activity, quiz or task performance when they open
its page; the first time is kept. Code: `Services/ProviderService.cs`, `ModuleService.MarkDeliveryOpenedAsync` (the teacher), `ModuleService.OpenModuleAsync` and `WorkService.RecordViewAsync` (the students);
the data is in `material_deliveries.opened_at_utc`, `module_views` and `item_views`.

The **Teachers** tab lists each teacher who has a course this school year with what they received, opened and posted, how many are still waiting (and how old the oldest is) and
how long they take to open on average; the **Students** tab lists the students who were given something, with how much of it they opened (the ones who opened least first). A name opens that person's page
(`/provider/tracking/users/<id>`): for a teacher everything sent to them with its state; for a student everything posted to them with when they opened it (or *Not yet*).

**Analytics** (`/provider/analytics`): the share opened by the teachers, the average time to open and to post, the share of modules and of activities the students opened, **week by week**,
**by kind** (syllabus, module, activity, quiz, task performance), **by section** and **by course**, the teachers with the oldest unopened items, and the modules the students open least.
The **Dashboard** (`/provider`) sums it up: sent, opened, posted, opened by students, the lines waiting longest, what was opened lately and the courses that still have no syllabus.

## Academic coordinator: teacher evaluation and analytics

**Teacher evaluation** (`/coordinator/evaluations`): **New evaluation** starts with the usual 16 questions about a teacher in areas such as *Teaching* and *Classroom management*; the coordinator changes,
adds, copies and removes questions while it is a draft. A question is added by choosing **how the students answer**:

| Kind | The students see |
|---|---|
| 5-Point Likert Scale | Strongly disagree, Disagree, Neutral, Agree, Strongly agree (1 to 5) |
| 5-Point Performance Scale | Poor, Fair, Good, Very good, Excellent (1 to 5) |
| Frequency Scale | Never, Rarely, Sometimes, Often, Always (1 to 5) |
| Binary Scale | Yes or No |
| Ranking Scale | The options the coordinator wrote (2 to 10), each given its own place |
| Select-All-That-Apply | The options written (2 to 10), tick every one that fits (none is fine) |
| Comment | A few words |

The three 5-point scales make a teacher's **average** (at least one is needed); Yes/No, rankings and select-all questions have their own results in the analytics (the share of Yes and No, the average place and
how many put an option first, the share who ticked each option). **Send to students** picks the teachers' courses it is about and a **due
date**: the students of those courses are told and find, in their dashboard and in **Teacher evaluation** (`/student/evaluations`), one questionnaire for each teacher. A student answers
once for each teacher before the due date. The answers are **anonymous**: the coordinator sees averages and comments, never who wrote them, only **who has not answered yet** (by name,
to remind them). **Close now** ends it early; the due date can be moved later. Code: `Services/EvaluationService.cs`.

**Analytics** (`/coordinator/analytics`) is about the teacher evaluation: the answers and how many students answered, the average rating, the teachers **ranked** by their average (with
*Outstanding / Very satisfactory / Satisfactory / Fair / Needs improvement*), how the students answered the 5-point scales (1 to 5), the Yes/No, ranking and select-all results, the averages **by area** and **by question**, the trend from one evaluation to the
next, and for each teacher a *Details* view with the courses, the questions and the comments without names.

## Enrolling a student (registrar)

Menu: **Registrar > Enrollments** (`/registrar/enrollments`). Fill in:

| Box | What to put |
|---|---|
| LRN | The 12 digits of the Learner Reference Number. |
| First name, middle name, last name | The student's name. The middle name is optional. |
| Term | The term to enroll in. The current term is chosen for you. |
| Grade level | 11 or 12. |
| Track, strand | ACAD (BUSSENT, EDUC, ENG, MED, USP) or TECH-PRO (CD1, HT1, HT2). Pick the track first, it filters the strands. |
| Schedule | AM, MID or PM. Only the schedules the strand runs in are listed (for example HT2 only has PM). When there is only one, it is chosen for you. |
| Section | Pick a section that already exists for that strand and schedule, or type a new one, for example `A`. The section is made if it is new. |
| Phone number | **Optional.** A mobile number like `0917 123 4567`, saved as `+639171234567` on the student's account. A student who already has a number keeps it. |

There is nothing to pick for the subjects: the student gets **all the subjects of the strand** for that grade level and term (the form tells which). Press
**Enroll student**. The page then shows what was saved and, for a new student, the **sign-in details to hand over**
(with *Copy the login* and *Print*).

### The enrollment form to print

The **Print** button of an enrollment prints the **enrollment form** on two A4 pages, as a clean document with **no frame and no box for a picture** (the screen shows only the receipt; `Components/Shared/EnrollmentPrintSheet.razor`).
**Page 1** has the letterhead (the **school logo at the upper left**, the **school name** with its tagline and contact line next to it, the enrollment number and the date at the right), the title *Enrollment Form* with the school year and term, and four parts written on fine lines: **Learner information** (the name in capitals, the LRN in twelve boxes, the contact number), **Enrollment details** (the **grade level and the schedule written out as words**, then the track, strand, section and term), **Courses enrolled** (a numbered list, with a *Days and time* column when the courses have class times) and **e-LMS account of the learner** (email and the first password, only for a new account), and at the bottom the **signature lines** (learner or parent, registrar, date).
**Page 2** is the **terms and conditions in twelve parts** (correct information, enrollment and sections, attendance, grades and academic standing, honesty, the e-LMS account, proper use, conduct, data privacy, the parent or guardian, records, changes). **At the bottom of the page**, side by side, are the **student's signature over the printed name** (the name is printed in capitals over the line) and the **parent's or guardian's signature over the printed name**, each with a date line.

### The automatic email and password

A student who is not in the system yet gets an account made from the last name and the **last 6 digits of the LRN**:

| | Rule | Example (LRN 111111111111, "Jecco seth C. Vargas") |
|---|---|---|
| Email | `lastname` + `.` + last 6 digits + `@legazpi.seaitt.edu.ph` | `vargas.111111@legazpi.seaitt.edu.ph` |
| Password | `lastname` + last 6 digits | `vargas111111` |

The last name is written in lower case without accents, spaces or punctuation (`Dela Cruz` becomes `delacruz`). The part
after the `@` is the `Enrollment:EmailDomain` setting in `appsettings.json`. If that email is already taken, or the
password would be shorter than 8 characters, more digits of the LRN are used, so every student gets a different email.
The code is `Services/StudentCredentials.cs`.

**Please know:** anybody who knows a student's last name and LRN can work out the password. There is **no page yet where a
student changes the password**. Until there is, an administrator can set a new one in **Users Management > Students > Edit**.

### A student who is already in the system

Typing the LRN of a student that has an account shows their name (locked) and adds a **new enrollment**, for example
for the 2nd term. The account and the password do not change. Enrolling the same student twice in one term is refused.

The form starts from where the student was the last time: the **grade level** (one more when the term is in a newer school year), the **track** and
the **strand**, and the **schedule** and **section** they had. The registrar only changes what is different (usually the schedule or the section).
The **Enroll** link of the *Next term* column opens the form this way, so a student who was never enrolled is the only one who needs track and strand picked.

### Student's Accounts

**Registrar > Student's Accounts** (`/registrar/student-accounts`) lists every enrolled student, newest first, 20 per page. Search by name
or LRN and filter by term, strand, schedule and **next term** (enrolled or not). The **Courses** button of a row (for example "8 courses") opens a small pop-up over the page with the
student's courses, so the columns do not move. The **Final grade** column, just left of *Next term*, is the average of the grades of the student's courses in that term (points
earned divided by points possible of the graded work; courses nobody graded yet are left out; "No grades yet" when none is), with what it means (*Satisfactory*, ...). The dashboard (`/registrar`) shows how many students are enrolled this
term, today, by strand, **by section** (every section of the school year with the students enrolled in it this term) and by schedule, and under *By schedule* the **Not enrolled** section.

**The next term and unenrolling.** The next term is the term after the current one (set in the school setup). The **Next term** column says whether the
student is enrolled for it: *Enrolled* with an **Unenroll** button (it asks first, then removes the student's enrollment in the next term and its subjects;
the current term is untouched), or *Not enrolled* with an **Enroll** link to the form, already filled with the LRN and the next term. The registrar enrolls a
student for the next term with the same form (pick that term).

**Not enrolled** means: a student at school (not archived) with no enrollment in the next term (or in the current term when there is no next term).
It is shown on the registrar's dashboard (with an Enroll link for each student) and, for the administrator to look at, on the *Not enrolled* card of the administrator's dashboard.

## Managing users (administrator)

**Administrator > Users Management** (`/admin/users`). There is one tab per role (Students, Teachers, Registrars, Guidance,
Coordinators, Providers, Administrators), each with its number of accounts, and a search box (name, LRN or employee number, email). The students' list has a **Courses** column (the courses of
their enrollment) and the teachers' list a **Section** column (the sections a teacher teaches in) and, **separately, a Courses column** (the courses with their section). The Courses column is a small **"6 courses" button**: press it and a pop-up lists them (the same pop-up as the registrar's list; for teachers each line is the course and its section, like *BUSINESS ACCOUNTING · 11-BUSSENT-A*). A **Status filter** (all, active, inactive) sits next to the search box. There can be several academic coordinators and academic providers (there is only one
administrator, one registrar and one guidance counselor).

- **Edit** changes the name, email, LRN (students) or employee number (staff), phone number and the **new password**
  (leave it empty to keep the old one). The **Status** choice (**Active** or **Inactive**) decides whether the person can sign in (the Add form has it too, so an account can start inactive).
  Changing the email or the password, or switching an account off, signs that person out.
  Changing a student's name or LRN also updates it in their enrollments.
- **Add** (teachers, registrars, guidance counselors, administrators) makes an account with the password you type.
  Students are not added here: they get their account when the registrar enrolls them. **Add teacher** also asks where the teacher works
  (all optional): **Track**, **Strand**, **Section** (the sections of that strand) and the **Courses** of that section, with a tick box for each
  (and *Select all*). The courses are given to the teacher for that section right away, and the teacher's track, strand, section and grade level are saved on
  the teacher. The choices are checked first (the strand must be in the track, the courses must be courses of that strand at that grade level, and a
  course another teacher already teaches to that section can't be given), and a wrong choice adds nothing. Code: `UserManagementService.AddTeacherAsync`.
- **Delete** asks first and says what else goes: a deleted student loses their enrollments, a deleted teacher's
  sections lose their adviser, and the enrollments a registrar made stay. Deleting **cannot be undone**,
  switching the account off is the gentler choice.
- You cannot delete or switch off **your own** account.

## The public home page

`/` is the page visitors see (`Components/Pages/Public/Index.razor` and `Index.razor.css`). A signed-in person who opens it is
sent to their own page instead. To change it:

- The words (welcome text, mission, vision, about) are written directly in `Index.razor`.
- The **Get in Touch** boxes come from the `Contacts` list at the bottom of the same file. **The email there,
  `your-email@example.com`, is a placeholder: replace it.**
- The **courses** are read from the database (`Data/Queries/PublicCourses.cs`): each strand with its schedules and its
  subjects per term. **Click a strand** to see its **grade levels** (Grade 11, Grade 12) and, under each, its **terms** (1st Term, 2nd Term) with
  the number of courses; **click a term** to see its subjects (one term is open at a time; plain `<details>` boxes, no script). To change them, change the tables `strands`, `subjects` and `strand_subjects` (see
  [docs/database.md](docs/database.md)).
- The big picture at the top is four photos that change by themselves (the dots pick one). They are the files
  `wwwroot/images/hero/hero-1.jpg` ... `hero-4.jpg`: replace a file to change a photo (keep them under about 400 KB so the
  page opens fast).
- The school's logo is `wwwroot/images/logo.png` (it is the icon in the header, on the sign-in page and on the home page)
  and `wwwroot/favicon.png` is the little icon in the browser tab. The picture next to the sign-in form is
  `wwwroot/images/hero/login-photo.jpg`.

## Pages and styling

Every page and component has **its own stylesheet next to it**, named after it: `Login.razor` is styled by
`Login.razor.css`, `Admin.razor` by `Admin.razor.css`, and so on. Blazor (it is called *CSS isolation*) applies each
file only to its own component, so the same class name, like `.card` or `.row`, can be used in two files without them
affecting each other. There is no big shared stylesheet.

| Files | What they style |
|---|---|
| `Components/App.razor.css` | The colors, sizes and dark mode, written once as variables (`--brand`, `--surface`, `--text`, ...), and the page background and font. Change a color here and it changes everywhere. |
| `Components/Layout/` | The header (`MainLayout`), the menu on the left (`NavMenu`), the account menu (`UserMenu`), the sign-in layout (`AuthLayout`) and the bare layout of the public home page (`BlankLayout`). |
| `Components/Shared/` | Small building blocks the pages are made of: `Card`, `StatCard`, `Badge`, `Avatar`, `PageHeader`, `EmptyState`, `Button`, `ButtonLink`, `Field`, `Modal`, `SearchField`, `EnrollmentTable`, `Brand` and `Icon`. |
| `Components/Pages/` | The pages, one folder for each part of the system, one page and one stylesheet each (see below). |

Good to know:

- **Every search bar is the same one**, `Components/Shared/SearchField.razor`: a wide box with a magnifier at the left, an **X** to clear it and a **Search** button inside, a short hint under it when it helps, and a label above. Lists put it with their other filters (labelled lists) in a `.filter-bar` row (the *Referrals*, *Tracking* and *Security logs* bars use `.filter-bar.one-row`: a **narrow search box with Status, Problem, Grade level and the other choices beside it on one single row**, no inner Search button: press Enter) (styled once in `Components/App.razor.css`): the search takes the room, the filters sit next to it, and everything wraps on a phone. Pages that look as you type use `Live`; pages that search on Enter or on the button use the default; static pages use `Static` (a plain form that reloads the page).
- To style something another component draws (the `<a>` that `NavLink` makes, the `<input>` of `InputText`), start the
  selector part with `::deep`. `Components/Layout/NavMenu.razor.css` shows it.
- **Icons** are from [Lucide](https://lucide.dev). Use them with `<Icon Name="house" />`. The available ones are listed in
  `Components/Shared/IconSet.cs`; to add one, open its `.svg` file from Lucide, copy what is inside the `<svg>` tag onto
  one line and add it there. The licence is in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
- **A new page**: add `Components/Pages/Admin/Something.razor` (in the folder of the role it is for) starting with
  `@page "/admin/something"` and `@attribute [Authorize(Roles = AppRoles.Admin)]`, give it a
  `Something.razor.css`, then add a link in `Components/Layout/NavMenu.razor`.
- The data a page shows is read by small query classes in `Data/Queries/`, and what a page *does* (enrolling, editing,
  deleting) is done by the classes in `Services/`, so the page itself stays mostly markup.
- The Enrollments form and the Users Management page are **interactive** pages (`@rendermode InteractiveServer`): they react while
  you type, over a live connection to the server. The other pages are plain pages.
- `wwwroot/app.js` closes the menus when you click elsewhere or press Escape, and copies/prints the login on the enrollment
  receipt. The pages work without it, apart from those two buttons.
- Times are stored in UTC and shown in Philippine time (UTC+8), see `Components/Shared/PhilippineTime.cs`.

## Project layout

```
Program.cs                 Wiring: database, login cookie, pages
Auth/                      Custom login: PasswordHasher, AuthService, cookie checks, UserAccountService, RoleHome
Services/                  What the people do: EnrollmentService, UserManagementService, MessageService, NotificationService, SearchService, ...
Data/                      AppDbContext, entities, EF configurations, migrations, startup initializer
Data/Queries/              What the pages read from the database
Components/App.razor.css   The theme: colors, sizes, dark mode
Components/Layout/         Header (with the messages, notifications and settings), menu, user card, sign-in layout, public layout
Components/Shared/         Card, StatCard, Badge, Avatar, Icon, Modal, Field, SearchField, AchievementCelebration, QuizSettings, AttachmentList and AttachmentPicker (files), AnnouncementCard, ... (each with its own .razor.css)
Components/Pages/          The pages, in one folder per part of the system:
  Public/                    Index (the home page), Login, AccessDenied, NotFound, Error
  Admin/                     Dashboard, UsersManagement, EnrollmentLists (+ Enrollment/: Enrolled and Not enrolled), Archive, SecurityLogs, DatabaseBackups, SystemSettings
  Coordinator/               Dashboard, AcademicSetup (+ Setup/: one component per tab), Announcements, Evaluations, EvaluationEditor, Analytics
  Provider/                  Dashboard, CourseList (tracks, courses, subjects) with CourseTable, CourseLibrary, MaterialQuestions (the library and sending), Tracking (+ TrackingUsers: its Teachers and Students tabs), TrackingDetail, UserDetail, Analytics
  Registrar/                 Dashboard, Enrollments (the form), StudentAccounts (the list)
  Teacher/                   Dashboard, Subjects, Subject (modules and work), ItemSubmissions (results and grading), QuizBuilder, Attendance, Gradebook, Announcements (to their classes)
  Student/                   Dashboard, Subjects, SubjectModules, ModuleLesson, WorkPage (hand in), QuizTake, Grades, Evaluations, EvaluateTeacher
  Guidance/                  Dashboard, Referrals
  Account/                   Calendar (students and teachers) with ItemScheduleDialog, CalendarDayDialog, CalendarEntryDialog, Analytics (admin, guidance, teachers), Users, GroupList, GroupPage, Resources, Help (+ HelpContent)
wwwroot/                   favicon.png, app.js and images/ (logo.png, hero/: the photos of the home page and the sign-in page)
appsettings.Local.example.json   Copy it to appsettings.Local.json (git ignores that one) and put your passwords in it
database/                  The SQL files for MySQL Workbench: start over (reset and rebuild), sample accounts, sample data, and the script that generates the first one
Backups/                   The backups made by Database Backup & Restore (created by the app; git ignores it)
docs/database.md           Tables, relationships and the rules the database enforces
THIRD-PARTY-NOTICES.md     Licence of the icons (the logo and the photos are the school's own)
```

## How the login works

1. `/login` is a normal form. `AuthService` looks the user up by email, checks the password hash and the lockout,
   and the page writes the sign-in cookie.
2. Every page needs a signed-in user unless it has `@attribute [AllowAnonymous]` (the public home page, the login page and
   the error pages have it). Restrict a page to roles with `@attribute [Authorize(Roles = AppRoles.Admin)]`
   (an administrator and a registrar each have their own pages: neither can open the other's). A signed-in user who opens a
   page they may not see gets an *Access denied* page.
3. Wrong passwords are counted. After 5 in a row the account is locked for 15 minutes (`Auth` section in `appsettings.json`).
4. A signed-in user is re-checked against the database every few minutes, so deactivating an account (or changing
   its `security_stamp`, which editing the email or password in **Users Management** does) signs it out.
5. Two-factor sign-in is not built yet. `users.phone_number` is stored (as +639...) for when it is.
