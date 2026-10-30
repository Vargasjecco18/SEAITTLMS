# SEAITT-LMS

SEAITT Legazpi LMS, the learning management system of SEAITT (Senior High School, Legazpi). Blazor Web App on .NET 9 with a MySQL database,
set up without a `dotnet new` template and with its own login (no ASP.NET Core Identity package).

What exists so far:

- **A public home page** at `/` for visitors: photos of the school, its courses (read from the database) and a
  **Log in** button.
- **Custom login** with the school's picture next to the form: email + password, PBKDF2 password hashing, cookie
  sign-in, account lockout. **The sign-in ends when the browser is closed** (the cookie has no end date, and there is no "keep me signed in" box), and also after a long time without any page open. After signing in, everybody lands on the page of their role.
- **Users and roles**: Admin, Teacher, Student (with LRN), Registrar, Guidance Counselor, **Academic Coordinator** and **Academic Provider** (of the central team),
  each with its own table holding the person's name and details.
- **Registrar pages** (for the registrar only): *Dashboard*, *Enrollments* (the form that enrolls a student; the
  student's email and password are made automatically) and *Student's Accounts* (a searchable list).
- **Administrator pages** (for the administrator only): *Dashboard* (accounts, and who is **enrolled** or **not enrolled**) and *Users Management*: one list per role, with
  search, **edit**, **delete** and add (for staff).
- **Academic coordinator pages** (for the academic coordinator only): *Dashboard*, *Academic setup* (school years, terms, tracks, strands, courses and sections),
  *Teacher evaluation* (a questionnaire the students answer about their teachers), *Announcements* and *Analytics* (the results of the teacher evaluation).
- **Academic provider pages** (the central team; for the academic provider only): *Dashboard*, *Course library* (the syllabus, modules and **summative exams**
  that are **sent to the teachers** as drafts: only the provider sends them; the teacher makes the activities), *Tracking* (did the teacher open it, post it, did the students open it; with a tab for the teachers and one for the
  students and what they opened) and *Analytics*.
- **For students and teachers**: *Users* (classmates, all students, friends and teachers / friends, students and all teachers), *Groups* (made for you by the system, and your own), *Resources* (a locker for your files and the files shared with you),
  an *Online students* box in the dashboard, and a **profile picture** in the settings menu.
- **More for the administrator**: *Security Logs*, *Database Backup & Restore* and *System Settings*.
- **A Help page** for every role (the link is above the profile card in the menu), and **pop-ups in the menu**: rest the pointer on a page and a box shows what is inside it.
- **The first page** (`/`, before signing in; its picture and its sections use little space so more shows at once) lists **Our courses** by track: each track, its strands with their full names, and, when a strand is opened, the courses it studies by grade level and term.
- **Enrollment** by school year and **term**: tracks, strands, subjects (with their **units**), sections (a section is the school year, strand, grade level and name), the **schedule (AM, MID, PM)** of each course,
  enrollments. The database comes with the school year 2026-2027, its two terms, the ACAD and TECH-PRO tracks, their
  strands and the subjects of grade 11.
- **A header** with, at the right, **messages**, **notifications** and **settings** (there is no search bar in the header; every list has its own search)
  (light / dark / auto look, animations on or off). The signed-in person's name and role are at the bottom of the menu.
  Everything is phone-friendly and tablet-friendly: the menu is a drawer under 960 px; the course menu is a strip that scrolls sideways (with a soft
  shadow at the edge that has more, and the open page brought into view); the buttons at the top of a page share the width of a phone; a line of people puts
  its buttons under the name; a table that is wider than the screen scrolls inside its box with the same shadow, and the class schedule of the academic setup
  becomes a small card for each course; on a touch screen a button is at least 2.75 rem tall; on a tablet the widgets of a dashboard are in two columns; the
  boxes (dialogs) use nearly the whole width of a phone.

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
the school setup (school year, terms, tracks, strands, subjects, the hours of AM, MID and PM). Then run `dotnet watch` to create the
administrator.

Any other users you had are gone. Add the staff again in **Users Management**, and the students by enrolling them.

**Sample accounts:** to get sign-ins to try the app (1 administrator, 1 registrar, 1 guidance counselor, **1 academic coordinator, 1 academic provider**, 12 teachers and 40 students), open
[database/seaittlms_sample_accounts.sql](database/seaittlms_sample_accounts.sql) in Workbench and click the lightning bolt.
It only adds what is missing (and gives the sample accounts of an earlier version the new email and password), so running it twice is harmless. The sign-in of each person is made from the
name and the number the person has at school, the same way the app makes the sign-in of a new person:

| Who | Email | Example |
|---|---|---|
| Administrator | `admin@legazpi.seaitt.edu.ph` | (the one the app made from `Seed:Admin`) |
| Staff and teachers | the last name, a dot and the digits of the **employee number** | `villanueva.001@legazpi.seaitt.edu.ph` (employee number T-001) |
| Students | the last name, a dot and the last 6 digits of the **LRN** | `jacinto.111111@legazpi.seaitt.edu.ph` (LRN 105202111111) |

The SQL files hold only the hashes of the passwords. The list with every sign-in (name, email and password of the 57 accounts) is in [docs/sample_login.md](docs/sample_login.md). A student's password is the last name followed by the same 6 digits, the rule the registrar's Enrollments page shows when a student is enrolled. These are sample accounts: give real people a harder password in **Users Management > Edit**.
(the registrar's Enrollments page shows it when a student is enrolled); the list with every sign-in is kept out of the repository, ask for it when you need it. Give real people a harder password in **Users Management > Edit**.

The school has **one** administrator, registrar, guidance counselor, academic coordinator and academic provider, so the file never adds a second one and **never changes the password of the administrator you already have** (the last lines of the file, written as a
comment, can give that administrator the sample password too).

**Sample data:** the accounts have no courses or enrollments yet (the students are listed under *Not enrolled*). To fill the tables with practice data, run
[database/seaittlms_sample_data.sql](database/seaittlms_sample_data.sql) **once**, after the accounts file (open it in Workbench, press the lightning bolt, wait a minute). It enrolls the 40 students in
**10 sections of 8 strands** (Grade 11 and Grade 12, the ACAD and the TECH-PRO tracks; one student has the name extension *Jr.*), gives the 12 teachers their courses (each with its days, MWF or TTH, and its time inside the AM, MID or PM schedule of its section), and adds, with dates around the day you run it: the library of the academic provider and what it sent to every teacher (some opened, some
still waiting, some posted), modules with **PDF files** (the syllabus, the notes of the lesson, a worksheet and a rubric) and with the work the students handed in as PDF, assignments, quizzes with questions and task performances, who opened what, the work handed in and graded, attendance, announcements, messages, notifications, friends,
the automatic groups and three groups the students made, files in the lockers, the security log, the settings, announcements of every category
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
| **Messages** (speech bubble) | A box with your conversations. Type a name to write to somebody. A student can write to teachers, the guidance counselor, the registrar and their friends; a teacher also to students and the other staff (the academic provider too). **Files can be sent** with a message: the **paper clip** at the left of the Send button (up to 10 files; the sender and the person it is for can open them). **Messages can be sent at any hour** (there is no closing time). The number on the icon is how many messages are unread. **A new message only changes this icon**: it does not add a line to the bell. |
| **Notifications** (bell) | What the system wants you to know (grades, deadlines, a student who handed in work, an assignment added to a module that is already posted ...). A module that was just posted is **not** announced here. The number is how many are new. Click one to open the page it is about. |
| **Settings** (gear) | A **box**: your **profile picture** (upload, change or remove), **Change password** (the current password, the new one twice, "Show the passwords"; afterwards you sign in again with the new password), **Light**, **Dark** (black) or **Auto** (follows the computer), **Animations** on or off, and **Sound** on or off (a short sound when a new message or notification arrives, in every role). The picture and the look are kept as soon as they are chosen; **a Save button at the bottom of the box** keeps the chosen sound and the new password (when the password boxes are filled in). **Notification sound** lets each person choose the sound: one of five the browser makes by itself (two tones, chime, bell, pop, knock), the **sound of the school** (the administrator adds it in System Settings) or **their own file** (.mp3, .ogg, .wav or .m4a, up to 2 MB; *Play it* tries the sound, *Remove my own sound* goes back). The choice is kept in your browser **and in a cookie**, so the server draws the same look on every page (a page that is opened by a click on a link used to go back to light). |
| **Name and role** (bottom of the menu) | Who is signed in. The arrow button signs you out. |

The code: `Components/Layout/HeaderTools.razor` (the three buttons), `MessagesPanel.razor` (the Messages box),
`Services/MessageService.cs` (the rules), `Services/NotificationService.cs`.

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
| Teacher | `/teacher` | **Dashboard**, **Courses** (modules, assignments, quizzes, task performances, grading), **Attendance**, **Calendar**, **Announcements** (to their classes), **Gradebook**, **Analytics**, **Users**, **Groups**, **Resources** (in this order in the menu) |
| Student | `/student` | **Dashboard**, **Courses** (modules, work, quizzes), **Calendar**, **Grades**, **Users**, **Groups**, **Resources** (in this order in the menu) |
| Guidance counselor | `/guidance` | **Dashboard**, **Referrals** (one row for each student), **Analytics** |

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
students, **Modules** (posted, with a bar) and **Work** (the assignments, quizzes and task performances, with a **"2 to check"** button that opens a box with the work handed in and not scored yet, each one a link)). A card has the same look: the photo of the strand, the course, the **section** and the **class time** one under the other at the left, then the counts of **modules (with how many are posted), assignments, quizzes and task performances**, and a *to check* badge when something waits to be scored. **Open** shows its modules.

**Inside a course (teacher and student) there is a second menu** (at the left of the page, above it on a phone): **Modules** (the page that was there before), **Assignments**, **Quizzes** and **Task performances** (each opens the list of that kind for the whole course, module by module, with a count), and the pages that belong to the course: for the teacher **Attendance**, **Gradebook** and **Students** (the students of the course, each with a **Message** button that opens the messages on that student), for the student **Grades** and **Teachers** (the teacher of the course, with a **Message** button); **Exams** joins them once the course has an exam (see *the gradebook*). The school's own menu stays as it is. **The menu can be hidden and shown**: its first button (*Hide menu* / *Course menu*) folds it away so the page takes the whole width, and the choice is kept on that computer (`data-course-menu` on the page, kept in the browser). **It stays in view under the header while the page scrolls** (on a phone it is a strip under the header), so *Modules* is always there. It is on the course page, on the lesson a student reads (**Read lesson** and **Read again**) and on the module page of the teacher. **On those two reading pages the school's main menu folds to its icons** (a narrow column; the first button of the column shows the names again, and the next page has the full menu back): `Components/Shared/CourseLayout.razor` (`CollapseMenu`) puts a marker, `wwwroot/app.js` sets `data-rail` on the page and the width comes from `--sidebar-w` (`App.razor.css`, `MainLayout.razor.css`). The teacher's module page has no *Lesson* card: the lesson text is in **Edit module** at the right. The tab is in the address (`?tab=assignments`, `quizzes` or `tasks`). Code: `Components/Shared/CourseMenu.razor`.

**The Add button** (top right of a teacher's course; the provider's library has its own buttons: *Add syllabus*, *Add module* and *Add summative exam*): it opens a small menu, first the **module** (the lessons of the course), then **Assignment**, **Quiz**, **Task performance**, **Seatwork** or **Recitation**; the box for that kind opens at once (a seatwork and a recitation are handled like an assignment: the teacher gives the scores, in the gradebook or on the list of the work; each has its own tab in the course menu, and its own sheet in the Excel workbook, once the course has one; their short names in the gradebook are *S* and *R*). In a list of one kind (for example Assignments) the button only asks for the module. Code: `Components/Shared/AddItemMenu.razor`. In the second step there is also **Template and import (Excel)** for that module (see *Excel for activities*). 

**The box of an assignment, quiz or task performance** has no *Kind* box (the kind is the button that was pressed): the title, the instructions, the **highest score**, then **Appears on** (at the left) and **Due date** (at its right), the files and, for a quiz, its settings. **A quiz with a file has no questions**: as soon as a file is attached the box says so, asks for the highest score, saves without opening the question editor, and the question page of that quiz only says it is a file (take the file off to write questions; the service refuses questions for a quiz that has a file, and the library of the provider follows the same rule). The students answer a file quiz like an assignment (the editor and files) and the teacher gives the score.

**Excel for assignments, quizzes and task performances.** Next to *Add ...* in every group of a module there is an **Excel** button: it opens a box with the **template** (the column titles, an example and a sheet that says how to fill it), an **export** of what the module has of that kind (the same file, so it can be changed and imported again) and the **import** (every row becomes one item; a row with a problem is listed with the reason and does not stop the others; at most 100 rows). In the box of a new item, *Fill many from an Excel file instead* opens the same box. The page of the **questions of a quiz** has its own Excel tools: **Template** (three example questions), **Export the questions** and **Import from Excel** (the questions are added to the editor's list and kept when you press *Save questions*; at most 200). Dates are written in Philippine time, like `2026-11-20 17:00`. Code: `Services/ItemSheets.cs`, `Components/Shared/ItemExcelDialog.razor`, `QuestionExcelTools.razor`.

**Excel for activities (one workbook for a module).** The Add button's **Template and import (Excel)** (the teacher's course: first the module, then this) opens one box for the whole module. The **template** is one workbook with the sheets **Assignments**, **Quizzes**, **Quiz Questions** and **Task Performances** (and *How to fill*): each activity has a *Files* column, and every question in *Quiz Questions* has the **title of its quiz** in the column *Quiz*, which is what connects the questions to the quiz. In the box: choose the filled workbook, see every row with what it will be (*Ready · 3 questions · 1 file*, or the reason it can't be), then **choose the files the workbook names** (the box lists them, each one *not chosen yet* or chosen, and a row is ready only when its files are there); **Import** saves each row like the box of the page does (a quiz with a file has no questions and is refused with the questions), its questions and its files. *Export what the module has* writes the same workbook, so it can be changed and imported again. At most 100 rows per sheet. **Every Excel template of the system has the same kind of help** (`Services/TemplateGuide.cs`): the sheet **How to fill** starts with the steps (*1. Fill in the yellow cells ... 2. Save with Ctrl+S as .xlsx ... 3. Import it*) and a table with, for each column, **what to write** (needed or optional, and the rule) and **what to use in Excel** (*Type it*, *Choose it from the list*, *Type a number*, *Type a day like 2026-11-19*); and the columns that take a fixed answer are **drop-down lists** like a spreadsheet (the quiz of a question from the sheet *Quizzes*, the type of a question, a day of the week, Yes/No, a role, a strand...), the numbers accept only numbers, and a tip shows when a cell is selected (`Services/Spreadsheet.cs`: `ListChoice`, `ListFrom`, `Number`, `Tip`). The academic provider's **Add module** box has **Template and import (Excel)** too: a workbook with the sheet **Modules** (title, short description, lesson, link and *Files*). Code: `Services/ActivitySheets.cs`, `ActivityImport.cs`, `ModuleSheets.cs`, `Components/Shared/ActivitiesExcelDialog.razor`, `ModulesExcelDialog.razor`.

**The module page of the teacher.** In the course page, the name of a module's file (a PDF, a picture ...) opens the module **inside the system** (`/teacher/subjects/<course>/modules/<module>?file=<file>`), like the student reads it: **the menu of the course at the left, the lesson with the PDF in a viewer in the middle and the form *Edit module* at the right** (title, short description, lesson, link, files and slides; *Save module*). Every module also has an **Open** button. Code: `Teacher/ModuleView.razor`, `Components/Shared/ModuleEditor.razor` (the same form is the box of *Edit*), `LessonContent.razor` (the lesson as the student sees it), `CourseLayout.razor`.

**Slides.** The teacher's and the provider's *Edit module* box has a second file place, **Slides of the module (PowerPoint or PDF)** (`.ppt`, `.pptx`, `.pps`, `.ppsx`, `.odp`, `.key`, `.pdf`; ten of them, apart from the ten other files). They are stored as attachments with `is_slides` set, go to the teachers with the rest of what the provider sends, and the students read them in their own **Slides** card on the module page (a PDF in the viewer, a PowerPoint file as a download).

**Late work.** A student can still hand in an assignment, task performance or file quiz after its due date; it is marked late and **the teacher decides**: the teacher is told ("... (late)") and, on the item's page, the row says *Late · accept?* with **Accept** and **Decline** buttons (and **Read**). Late work can be graded **only after it is accepted**; declined work is not graded, is not counted as work to check, and can't be handed in again (the teacher can still change their mind until it is graded). The student is told either way. In the gradebook such a cell says *Late · decide* or *Declined* and links to the work. Column `submissions.late_decision` (`Pending`, `Accepted`, `Declined`), code `WorkService.DecideLateAsync`. The **syllabus** and the modules the academic coordinator sent are drafts here too, marked
*From the academic provider*. There is no *Add module* button and no button to move or delete a module: the academic provider sends the modules and the teacher
changes their text and dates (**Edit**) and adds assignments, quizzes and task performances (with the **Add** button; the provider sends none of them). A module the provider sent is a **draft**: students
cannot see it. **Post now** shows it to the students (they get a notification), **Schedule** picks the day and time it
appears by itself, **Hide** takes it back. At the bottom of a module are its **Assignments**, **Quizzes** and **Task
performances**; each has a due date (and can have its own time to appear). Open an item to see who handed in (the list has a **Files** column with the files each student sent), read the
work (with the formatting the student gave it), give a score and feedback (the student is told).

**Modules are closed lists.** On a course page (the teacher's and the student's) every module is a line with its name: **press the name** to open it and see its assignments, quizzes and task
performance (and its buttons); press it again to close it. Several can be open at once. Code: `Teacher/Subject.razor`, `Student/SubjectModules.razor`.

**Quizzes** have questions (multiple choice, true or false, short answer) that are graded by themselves, an optional time
limit, shuffled questions and choices, and the **anti-cheating watch**: while a student takes the quiz the page tells the
system when they leave the tab or window, copy, cut or paste (those are blocked too), **take a screenshot** (the Print Screen key, Windows + Shift + S, Command + Shift + 3, 4 or 5) or **print** the page. Each of those is a warning; after
the number you choose the quiz is handed in by itself and **flagged** for you, with a log of what happened. The time is
checked on the server, and a quiz that was left open is handed in when its time is over. **What it can't do:** a web page can't see a picture taken with **another device** (a phone pointed at the screen), so the quiz page is honest about it: it says so to the student and shows the student's **name and email** faintly all over the page, so a photo of the quiz leads back to the student, and the teacher can compare the answers.

**Student > Courses** shows the courses they are enrolled in as cards (or a list). The page **remembers the view you chose**: a chip, the Courses link of the menu or another page does not take you back to cards (the teacher's Courses and the calendar, Month or Week, do the same). It is a cookie (`seaitt-view-subjects`, `seaitt-view-calendar`) that `wwwroot/app.js` writes when a link with `view=` is followed and `Components/Shared/ViewPreference.cs` reads when the address has none. The chips are **All**, **Enrolled** (the term has not ended) and **Completed** (the term is over, or the enrollment was completed), each with its number. A **card** starts with the **photo of the course's strand** (see below) and the name of the course. Under it, **at the left, one under the other and all in the same style (a small icon and the words)**, are the **teacher**, the **section** and the **class time**, and then, after a thin line, how many **modules, assignments, quizzes and task performances** the course has. The **list** has the columns course (with its term and class time), teacher, status, **Modules** (how many are posted, with a *new* badge for the ones not opened yet) and **Work** (kept apart from the modules: a button **"3 to do"** and, under it, the short counts *2 A · 1 Q · 1 TP*, with the full words as the tooltip), **progress**, next due, grade and attendance. **Press "3 to do"** and a box opens with the assignments, quizzes and task performances still to do, each one a **link** to its page with its due text; when everything is done the list says *All done*. Code: `Components/Shared/SubjectCard.razor`, `CourseWorkButton.razor`, `TeachingService.GetStudentToDoAsync`. The **progress** (a bar and a percent, also on every card)
is how far along the student is: **the modules they opened**, out of the modules that are posted ("2 of 3 modules opened"). **Open** shows the
posted modules; each ends with three tabs: **Assignments**, **Quizzes**, **Task performance** (and the course has the second menu with the same lists for the whole course). **Read lesson** opens the
module: the **slides and the files of the lesson are shown right in the page, at the left** (a PDF, a text file, a picture, a sound or a video in a viewer; when there are several files, a button for each one switches the viewer, and there are links to open one in its own tab or save it; a Word or PowerPoint file can't be shown by the browser, so it only offers the download) and the lesson text at the right. There is no *Your work in this module* card on this page, so the student can focus on the lesson (the work is on the course page). A module that was never opened is marked **New**. Code: `Components/Shared/FilePreviewPane.razor`.

**The photo of a strand.** Each of the eight strands has its own photo on the top of its course cards (Business and Entrepreneurship, Education, Engineering, Medical and Health Sciences, Computer System Servicing - Video Game Development, and the two Kitchen Operations / Food and Beverage Services strands, and University Studies Preparatory), (a plain photo: no badge and no icon on it). The pictures are the files `wwwroot/images/strands/*.jpg` (for example `engineering.jpg`); to use a better picture, replace the file with another **720 x 414** JPEG of the same name. `Components/Shared/StrandArt.cs` says which file a strand uses (by its code, or by its name when the code was changed). A strand that has no picture (one the administrator added) keeps the colored banner with a book.

**Your work: the editor.** An assignment or a task performance is answered in **an editor like a word processor** (`Components/Shared/RichTextEditor.razor`, script `seaittEditor` in `wwwroot/app.js`).
The bar above the box has: undo and redo, the **paragraph style** (normal, heading 1 to 3, quote), the **font** (Arial, Calibri, Cambria, Georgia, Times New Roman, Verdana, Courier New) and its **size** (8 to 48),
**bold, italic, underline, strikethrough, subscript, superscript**, the **text color** and the **highlight color**, clear formatting, **alignment** (left, center, right, justify), **bullets and numbering**, decrease and
increase **indent**, the **line spacing**, a **table** (3 × 3; Tab moves between its cells and adds a row after the last one), a **link** and a **line**. The tools that are on where the cursor is look pressed, and under the box are the **word and character count**
(at most 20,000 characters). Pasting keeps the words only (no pictures or fonts from other pages come in). The answer is saved with its formatting in `submissions.answer_html` (and the plain words in `answer_text`);
**the server cleans it again** (`Services/RichText.cs`: only a short list of tags, links and style rules is kept; scripts, pictures, frames and event handlers are taken out), so nothing from the browser is trusted. The student sees it
again in the editor to change it until it is graded; the teacher sees it with its formatting. Work handed in before the editor existed opens as paragraphs. **The student sends files, not a link.** Under the editor, *Send files* attaches up to ten files to the work (a PDF, a picture, a document; programs and scripts are refused, each file up to the size set in System Settings). A file alone is enough to hand in. Until it is graded the student can take a file off, add another and hand in again. The teacher reads the files from the student's work **from the grading page** (a click on a file name shows it in the box in the middle of the screen; the grading page has no preview pane of its own, so the answers and the scores keep the whole width); nobody else can open them (another student, another teacher or the registrar get nothing). The files are stored with the work (`attachments.submission_id`, bytes in `attachment_contents`) and go with it if it is deleted. **Quizzes with questions** are different: the questions are answered one by one on the quiz page and graded by the system. A **quiz that is only a file** (the provider or the teacher attached the questions as a file and added none) is answered in the same editor, in a card called *Your answers*, and the teacher reads it and gives the score by hand, like an assignment.

The code: `Services/TeachingService.cs`, `ModuleService.cs`, `WorkService.cs`, `QuizService.cs`, `AutomationService.cs`
(the background job), the pages in `Components/Pages/Teacher/` and `Components/Pages/Student/`, and the anti-cheating watch in
`wwwroot/app.js` (`seaitt.startQuizGuard`).

## Dashboards, calendar and the widgets at the right

**Student > Dashboard** (`/student`) shows what to do first. At the left: the teacher **evaluations** to answer and then **four cards in two columns and two rows**: at the top left the **Modules not opened**, at the top right the **Due dates** (the work to do, overdue ones in red), at the bottom left the **Recent grades** and at the bottom right the **Progress** (every course with its bar, a link to the course). Under them are the **achievements**: fourteen to earn (perfect score, high grades, quiz ace, always on time, curious mind, perfect attendance and first step, quiz taker, triple perfect, all-rounder, early bird, bookworm, top of the class, nothing missed): earned ones are colored, the others show how far you are. **When a student earns a new achievement, a box opens in the center of the screen** to celebrate it (several at once are shown one after the other, with *Next*); once it was shown it never opens again (the award is written in the `achievement_awards` table with the time it was seen; the achievements themselves are always worked out from the scores). At the right, in this order: the **calendar**, the **announcements**, the **online students** and the **online teachers**.

**Teacher > Dashboard** (`/teacher`) shows the **unchecked assignments** (work handed in that still needs a score), the
modules students can't see yet (drafts and scheduled ones) and the number of flagged quizzes (it links to the calendar, where the flagged attempts are
found). At its right are the calendar, the **online students** and the **online teachers**.

The column at the right of some pages (`Components/Shared/AsideWidgets.razor`) holds the widgets that page needs, in this order. The **student
dashboard** has the **calendar** first (a dot on every day with something, click a day to see it), then the announcements, the online students and the online teachers. **Online teachers** and **online students** list the people you can reach
who have the app open right now (a student sees their friends, classmates and teachers; a teacher their friends, students and colleagues), with a link to the Users page. **Show more:** any card at the right that lists items (announcements, assignments, assignments without a due date, due dates, unchecked assignments, the people online) shows the first 5 and, when there are 6 or more, a **Show more (N)** button at the bottom that opens the rest (and **Show less** closes it again). The
student's **Courses** pages also have a **Due dates** list. The teacher's
**Courses** page has the calendar and *Needs your attention*. The grades pages and the teacher's attendance page have no column. On a phone the widgets
go under the page.

**Calendar** (`/calendar`, students and teachers): a **Month** and a **Week** view with Previous, Today and Next, and an **Add event** button. It shows the
deadlines of the work (assignment, quiz, task performance), the modules that appeared (for a teacher also the ones scheduled), the
modules posted and **the events the person wrote** (violet). Done work is struck through, overdue work is red. Click a day number to open its week. **The week** has seven columns, Sunday to Saturday: a day with something on it says **what** in a few words ("2 assignments", "1 quiz", "1 event") and a day with nothing says so. **Click the date (or the words) and a box opens** with everything on that day, each line a link: an assignment, quiz or task performance opens its page (a teacher sees who handed it in), a module opens the course, an event opens to be changed. In the week view the **Unchecked assignments** and **Without a due date** cards are shown **in two columns** side by side. At the right a student has one **Assignments** card (the work to do, with its due text; there is no *Due dates* card on this page, it would repeat the same list); the Next button has its `>` icon. A **teacher clicks** an assignment, quiz or task performance to see **who handed it in** (and who did not), with the score and, for a quiz, the anti-cheating warnings: a flagged attempt is marked in red. *Open and grade* goes to the page of the work. Code: `CalendarWorkDialog.razor`, `CalendarDayDialog.razor`.

**Add event.** The button opens a box with a title, a day, an optional time (empty means all day) and an optional note. An event starts **private to the person who wrote it** (*Only me*), can be opened to **change or delete** it, and a person can keep up to 1,000. **Who is it for?** is optional: *A group* (choose one of your groups: every member sees the event) or *Friends or classmates* (pick one or more people: your friends, the members of your groups and your classmates, for a teacher the students of their classes; at most 100). The people it is shared with see it on their own calendar as **From {name}**, in a read-only box with a *Take it off my calendar* button; the owner can change who it is shared with later. **Tell them** is a box that is off at first: when it is on, the people newly added get a notification (the bell) with a link to the day. A person can only share with people they can reach, so a group of strangers or an account that is not a friend or a classmate is refused. The links are plain addresses (`/calendar?event=new`, `?event=12`, `?day=2026-10-06`), so the boxes also work after a reload. Code: `CalendarEntryDialog.razor`, `Services/CalendarService.cs` (tables `calendar_entries` and `calendar_entry_invites`).

**Teacher > Calendar: assignments without a due date.** The card **Without a due date** at the right lists the teacher's assignments, quizzes and task performances that have **no due date**. **Drag** one onto a day (month or week view) and a box opens with that day (5 PM Philippine time) as the due date, where the teacher can also change the title, instructions, highest score, when it appears and, for a quiz, the time limit and the **anti-cheating** settings (watch on or off, warnings before it is handed in, shuffling, showing the result). **Save** puts it on that day for the teacher and the students; **Cancel** leaves it unassigned. The **Set date** link next to an assignment does the same without the mouse. Code: `Components/Pages/Account/Calendar.razor`, `ItemScheduleDialog.razor`, the drag script in `wwwroot/app.js`.

**Student > Grades** (`/student/grades`) is the grade and progress page: one **card per subject**, closed at first, showing the subject, its average (points earned divided by
points possible of the graded work) and the grade. **Press the subject** to open its progress (a bar with the passing mark of 70 drawn on it, the number of works handed in and graded) and its graded work, with a link to the course. A grade under 70 is shown in red with a warning; a quiz whose result the teacher hides is not counted yet.
(The separate "Progress by course" section is gone: the progress lives in the subject's card.)

The code: `Services/DashboardService.cs`, `CalendarService.cs`, `GradeService.cs`, `AchievementService.cs`.

## Attendance and the gradebook (teacher)

**Teacher > Attendance** (`/teacher/attendance`): choose the course and the date, press **Present**, **Late** or **Absent** for each
student (or **Mark all present** first), then **Save attendance**. An earlier day can be opened and corrected; a day that has
not come yet can't be marked. The **View attendance** button (top right) opens a box with each student's present, late and absent days and the
attendance rate (days present or late divided by days marked; under 80% is red; there is no row of "class days marked").  Students see their rate on **Grades**, and five days all present earn the *Perfect attendance* badge.

**Teacher > Gradebook** (`/teacher/gradebook`): choose the course; every student is a row, every assignment, quiz and task
performance of a posted module is a column (with its highest score; the headers are short: **A1, A2** for the assignments, **Q1** for the quizzes and **TP1** for the task performances, in the order of the columns, and the full title shows when you point at it), then the **average** and, at its right, the **final grade**. A score under 70% is
red, work waiting for a score says *To check* (click it to open the work). **There is no Edit button: every cell is a box.** Type a score and leave the box (Tab or Enter): it is saved by itself (a score above the highest is refused with the name of the column) and the student is told; an empty box leaves the cell as it is. Feedback the teacher wrote
is kept. Late work that waits for the teacher's decision shows *Late · decide* instead of a box. **Export to Excel** makes a file with the sheets **Grades** (every score, then *Points earned*, *Points possible* and *Average* as **formulas**, so changing a score changes the numbers), **Breakdown** (the same calculation for assignments, quizzes and task performances apart, and all together) and **How it is worked out** (the rule in words: only graded work counts). The hint and the buttons are at the **top right** of the table: **Export to Excel**, **Add Activities** (adds an assignment, quiz or task performance to a posted module: a new column, and the students see it too, to give **extra scores**), **Add Exam** (a midterm or a final: a new column with the code **E1, E2 ...**, in the Excel file too, and **Exams** appears in the course menu; the exam is an item of the kind `Exam`, which is not one of the three kinds a teacher adds to a module) and **Save final grades**. Code: `Services/GradebookExport.cs`. The **final grade** is a number the teacher writes for each student (0 to 100, two decimals at most; a blank takes it away) and the **Save final grades** button above the table saves all of them at once (everything is checked first). The student sees it with the grade of the course (*Grades*). Table `final_grades`, code `Services/GradebookService.cs`.
**Moving the columns, adding an exam.** Under the name of each column of the gradebook two small arrows move it to the left or to the right; the order is kept for the course (`module_items.gradebook_order`) and the Excel file follows it. The short names (*A1, Q1, TP1, S1, R1, E1*) count the work of each kind in the order of the modules, so a column that was moved keeps its name. **Export to Excel** is next to **Save final grades**. *Add Exam* asks only for the title and the highest score: the exam is a column of the gradebook and is listed under *Exams* in the course menu (it is kept in the latest posted module).

## Guidance counselor: referrals

The guidance counselor looks after **two things only: attendance and violations.** (Failing grades and students who are at risk in a course are the teacher's concern: the teacher sees them in
*Analytics* and the gradebook.)

| Concern | How it gets there |
|---|---|
| **Not attending class** | the system finds it by itself: attendance **under 80%** over at least 5 marked days. It looks at every student every few minutes and when the counselor opens a page; a concern is not repeated, and not sent again for 14 days after it was resolved. The counselor gets a notification. |
| **Violation** | the counselor writes it: press **At risk** on the student, choose *Violation* and write what happened (only the counselor can read it). |

**Guidance > Referrals** (`/guidance/referrals`) lists **every student of the school, one row for each student** (the whole school is there, so a concern can be written for anybody): the student, the **grade level**, the **course** (the strand), the **section**, the **concerns** (a badge for each kind) and **since** when. There is **one search bar** for all of these (name or LRN, grade level 11 or 12, section, course), the box **Show** (*All students* or *Only students with a concern*) and the box **Concern** (*All*, *Attendance*, *Violation*). The page opens on all students; the menu has the sub-links *All students*, *With concerns*, *Attendance* and *Violation*. **There is no status**: a concern is either open or ended.
The button on every row is **At risk** (it used to say *Work on it*): it opens the student's concerns, each with its details, and a form **Add a concern** (*What is it?* and *What happened?*). **Notes** on a concern is where the counselor chooses the help given (talked with the student, called the parents, talked with the teacher, other), writes notes only the counselor reads and, when the case is over, says how it ended (*Improved* or *No change*), which ends the concern. A student is told
when the counselor changes a concern: the student gets a notification **and a box in the center of the screen** the next time their page is open (it refreshes by itself), like an achievement, with an **OK** button and, when several messages are waiting, a **Next** button. Once it was shown (or the notification was read) it does not open again. Code: `Services/ReferralService.cs` (`ListAllStudentsAsync`, `AddConcernAsync`, `UpdateAsync`), `Components/Pages/Guidance/Referrals.razor`, `Components/Layout/HeaderTools.razor`. (The `status` column of `referrals` stays in the database; the pages no longer show it. `referrals.reason` is `LowAttendance` or `Violation`.)

## Analytics

**Analytics** (`/analytics`) is for the administrator and the guidance counselor (the whole school) and for a teacher (only their subjects). It shows
the number of students, the average grade, the attendance rate, how many students are failing or at risk (a teacher's page has no *Failing* and *At risk* cards), the grades by band for each grade level
(each subject for a teacher), the **attendance** (the rate, how many were present, late and absent, and the rate of each section, or of each course for a teacher; there is no monthly trend any more), the **primary concern categories** (referrals by problem),
the **intervention effectiveness** (how many resolved referrals improved, by the help given) and the performance by strand. Everything is worked
out from the scores and the attendance that are recorded. **Export to Excel** (a button at the top of the analytics pages of the administrator, the guidance counselor, the coordinator and the provider) saves **everything the page shows** as a workbook with a sheet for each table, **and every result behind it**: the sheets *Students* and *Student results* (every student, every course, with the progress and the grade) and *All teachers* / *All students* (for the provider and the administrator: every teacher and student of the page, not only the top ones) (the provider's file also has *Teachers waiting* and *Least opened*; the coordinator's file also has, for every teacher, their courses, their questions, **the comments of the students** (anonymous) and *Evaluation after evaluation*). Code: `Services/AnalyticsExport.cs`. (A teacher's page has no attendance card: its cards are *Performance by course*, with *Courses at a glance* at its right, and *Students who need attention* at the bottom; the teacher's own attendance page and gradebook have the attendance.) 

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
| **Resources** (`/resources`) | **Locker**: your own files (upload several at once; rename, delete, **share** with people, a group or one of your courses). **Uploaded files**: the files **you sent** to other people (who got each one and when; filter by person or course). **Received files**: what others shared with you (a teacher can share a file with a whole course); *Keep a copy in my locker*. A file is at most **200 MB** and a locker **500 MB** at first (both set in System Settings, *Files and lockers*). Files are kept in the database. A **PDF, a picture, a text file, a sound or a video opens in a new tab of the browser** (the browser's own viewer, with its download and print buttons; *Open*), anything else is downloaded (*Download*), and web pages and SVG pictures are never shown (they could run as part of the site); program files (`.exe`, `.bat`, `.js`, ...) are refused. Code: `Services/FileService.cs`, `Services/FilePreview.cs`. **The files of your courses are listed there too** (with the name, the size and the day it was created): a student gets the files the teachers sent to their courses (the files of posted modules and assignments, slides included) and the files they sent with their own work; a teacher gets the files the students sent with their work (with the student and the assignment) and the files they attached to their courses. Code: `AttachmentService.GetCourseFilesAsync`. |

The code: `Services/PeopleService.cs`, `FriendService.cs`, `GroupService.cs`, `FileService.cs`, `Components/Pages/Account/Users.razor`, `GroupList.razor`, `GroupPage.razor`, `Resources.razor`.

## Administrator: Security Logs, Database Backup & Restore, System Settings

**Security Logs** (`/admin/security-logs`) has a **Last login** list (each person, their role and **the day and time they last logged in**, newest first, with a search and a role choice; there are no "signed in" and "signed out" lines any more) and lists who typed a wrong password (and from where), locked accounts, accounts that were added, changed, switched off or deleted, new passwords, backups and changes
of the settings, newest first, with the time, the email, who did it, the address and the browser. Filter by group (last login, failed sign-ins, account changes, system), kind, dates and words; **Download CSV** gives the same list
for Excel; **Clear old lines** deletes the older ones (the last 30 days are always kept, and the system clears by itself the lines older than the number of days in System Settings). Code: `Services/SecurityLogService.cs`.

**Database Backup & Restore** (`/admin/backups`) saves a copy of **all the data** as an SQL text file in the `Backups/` folder next to the app (git ignores it), made from one consistent snapshot of the database.
*Back up now* makes one; each backup can be **downloaded** or **deleted**; a backup file from another computer can be **uploaded**. **Restore** asks you to type `RESTORE`, first makes a safety backup of what is there now,
checks that the file is one of ours (only our tables and plain statements are accepted), puts the data back while the system is closed to everybody except administrators, and **goes back by itself** if something fails.
An automatic backup at an hour you choose (and how many to keep) is set in System Settings. Code: `Services/BackupService.cs`, `MaintenanceWorker.cs`.

**System Settings** (`/admin/settings`) changes, without editing files: **maintenance mode** (while it is on only an administrator can use the system; everybody else sees a page with your message), and
the **automatic backup** (the hour and how many to keep), how long the **log** is kept, **Files and lockers** (the biggest **file**: 200 MB at first, from 1 to 1000; the size of each person's **locker**: 500 MB at first, from 10 to 50000) and **the sound of the school** (an .mp3, .ogg, .wav or .m4a of up to 2 MB that plays for everybody who has not chosen another; it can be removed). Files are kept in the database as one piece, so MySQL must accept a packet that big (`max_allowed_packet`): the system raises it by itself at start when the database user may (`SET GLOBAL`), and System Settings warns when the value is too small for the size chosen; otherwise set `max_allowed_packet=1G` in `my.ini` and restart MySQL. (The number of students a section can have is not here: the academic coordinator sets it in *Academic setup > Sections*, **Students in one section (the limit)**.) Every change is written in the security log. (The other values the system uses, the name of the school, the wrong passwords before a
lock and the minutes locked keep their usual values; they are in the `system_settings` table if you ever need to change one.)
Code: `Services/SystemSettingsService.cs`; the values are kept in the `system_settings` table and read once when the app starts.

**The login page.** An email that has no account says **User Not Found**; a real email with a wrong password says *Incorrect email or password* and counts toward the lock (the lock is a lock of the **browser**: after the allowed wrong passwords nobody can sign in from it for 5 minutes, not even with the right password). The email box shows `lastname.lrn@legazpi.seaitt.edu.ph` as an example. A person who is **already signed in** and opens the login page is sent to their own page with a message in the top center, **Can't log in, someone is logged in**.

**Files attached to course materials.** A syllabus, a module and every assignment, quiz or task performance can have up to 10 files (at most the size of System Settings, 200 MB at first): the provider attaches them
in the library, they are **copied to the teacher's draft** when it is sent, the teacher can add and remove files in their copy (and in what they write), and the students of the course open them once the module is
posted. `/files/a/<id>` sends the file; `?download=1` always saves it. **A click on a file name never saves it by itself: it opens a box in the middle of the screen** (`Components/Shared/FileViewer.razor`, in the main layout; `wwwroot/app.js` fills it) that shows a PDF, a picture, a sound or a video, or says that the file can't be shown here; the box has **Download**, **Open in a new tab** and a close button (Escape closes it). A link with `data-file="Name"` (and `data-show="1"` when the browser can show that kind of file) opens the box; the lists of files (`AttachmentList`), the messages, the grading page of a work and Resources use it. Only the people who may see a module can open its files. Code: `Services/AttachmentService.cs`,
`Components/Shared/AttachmentList.razor`, `AttachmentPicker.razor` (tables `attachments` and `attachment_contents`).

## Academic setup (academic coordinator)

**Academic coordinator > Academic setup** (`/coordinator/academic-setup`) is **one page** with all the parts of the setup, one under the other, so nothing has to be searched for. At the top
is a line of **numbered steps** (each with how many lines it has): a click on a step goes down to it (the page does not change). The steps go in the order the school builds its setup:
**1 School years, 2 Terms, 3 Tracks, 4 Strands, 5 Courses, 6 Sections, 7 Class schedule**. In every step the **fields are always open** at the top of its card: type, press the button (*Add ...*)
and the line is in the list below. **Edit** on a line puts that line in the fields (the button becomes *Save changes*, with *Cancel*); **Delete** asks first. A step that depends on another one reads it again by itself (a school year added in step 1 is in the list of step 2 at once).
The coordinator (and nobody else: the administrator no longer changes it) can **add, edit and delete**:

| Step | What it holds and the rules |
|---|---|
| School years | Name (like 2027-2028), first and last day. One with terms or sections can't be deleted. |
| Terms | School year, number, name, dates (inside the school year) and **the current term** (making one current takes it away from the other: only one term is current). The term after the current one is the **next term**. A term with enrollments can't change its number or school year and can't be deleted. |
| Tracks | Code, name, offered or not. One with strands can't be deleted. |
| Strands | Code, name, track, offered or not, and **the courses of the strand** (the **Courses** button: pick grade level and term, add or remove courses). A strand with students can't move to another track; one that is or was in use can't be deleted (switch it off). |
| Courses | Code, name, **units** (0.5 to 12, one decimal, 3 by default; the list shows them in a column and they are printed on the enrollment form), description, in use or not. The **Strands** column, **at the right of the code**, names the strands that study the course (the first ones as chips; a course in several strands has a **+N more** button that opens a box with all of them, each with its grade level and term, not only a number). One in a strand's courses or taken by students can't be deleted. |
| Sections | School year, grade level, strand, name and adviser. **A section depends only on the grade level and the strand** (11-BUSSENT-A): it has no schedule, because the schedule (AM, MID, PM) and the days and time belong to each **course** (see *Class schedule*). **Press the name of a section (or its students count) and the students of the section open under it**: last name first, with the LRN, the term and the state of the enrollment (*Enrolled*, *Dropped* ...). One with students or teachers can only change its name and its adviser. The list can be filtered by grade level and strand. **Choosing the adviser** opens a box with a **table of the teachers** (search by name, grade level, track or course): each row shows the teacher's **name, grade level, track and courses**, so the coordinator knows who they are choosing; tick the row. |
| Class schedule | The hours of **AM, MID and PM**, and **the days and the time of each course of a section** (see below). The number on the step is how many courses have class times. |

The courses a student takes are **not picked at enrollment**: they are the courses of the strand for that grade level and term, as set here. Changing the
courses of a strand changes the enrollments made from then on; students already enrolled keep theirs. Code: `Services/SchoolSetupService.cs`.

**Class schedule** (the *Class schedule* tab). The day is in three parts, **AM, MID and PM**, and each of those has **hours** (they start as AM 7:00 AM – 12:00 PM, MID 10:00 AM – 3:00 PM and PM 1:00 PM – 6:00 PM). The button *Edit the times of the day* changes them
(it must end after it starts, last at least one hour and stay between 5 AM and 10 PM; a shorter day that would leave a class outside it is refused and names the class). The table sets **when each course meets**: choose a section (the ones of the school year that is running come first,
each with *how many of its courses have a time*; **at the right of the section list is the Term list**: *All terms*, *1st Term* or *2nd Term* shows only the courses of that term) and, for each of its courses (the courses of its strand at its grade level, by term, with the teacher when there is one), choose the **schedule** (AM, MID or PM), the **days**, **MWF** (Monday, Wednesday, Friday) or **TTH** (Tuesday, Thursday), and **when the class starts and ends**, then press the **one Save button**, at the right of the section list, which saves the whole section. A course with nothing chosen has no class time (a row you empty is cleared when you save). If any row is wrong **nothing is saved** and every problem is listed above the table.
The rules, checked again in the database before anything is saved:

* the time must be **inside the hours of the course's schedule** (a course in AM is between 7:00 AM and 12:00 PM), and a class lasts from 30 minutes to 4 hours; two courses of one section can be in different schedules;
* **two courses of a section can't meet at the same time** on the same days **in the same term** (a 1st-term course and a 2nd-term course can use the same hours, they are never taught together; MWF and TTH never overlap);
* **a teacher can't be in two classes at the same time** (same school year, same term, same days, overlapping hours): it is checked when the time is set and when the course is given to the teacher (Users Management > Give the course);
* only the academic coordinator can set or clear them; a section that moves to another strand, grade level or school year starts again without class times, and deleting a course takes its class times with it.

The class times show as **AM · MWF · 8:00 AM – 9:30 AM** (the schedule, the days and the time) on the teacher's *Courses* (cards and list), on the student's *Courses* (cards and list), on the registrar's enrollment receipt and on the **enrollment form to print** (a *Schedule* column at the right of each course). A course with no time simply shows none. **The database comes with class times:** the migration `ClassTimesForSeededSectionsAndSharedEvents` gives every course of every section that has no class time (and no teacher yet) one: section A in the morning (AM), B at midday (MID) and C in the afternoon (PM), on MWF or TTH, one hour each, never two courses of a section at the same time and the same course never at the same time in two sections. The coordinator changes any of them in the tab. Code: `Services/CourseScheduleService.cs`, `Components/Pages/Coordinator/Setup/ScheduleTab.razor` (tables `schedules`, `course_schedules`: `section_id`, `subject_id`, `schedule_id`, `days`, `start_time`, `end_time`).

**Grade 11 and grade 12, sections A to C.** Both grade levels have courses: the migration `AddGrade12CurriculumAndSectionsAToC` gives every strand the grade 12 version of its courses (the grade 11
name with a **2**: BUSINESS ACCOUNTING 2, ADVANCE MATHEMATICS 2, ... and six common courses in the 2nd term), and the database comes with sections **A, B and C** for the current school year, for
every strand, at grade 11 and grade 12 (sections D and E were removed; one that has students or a teacher would stay). The migration `UnitsNameExtensionAndClassTimeSchedules` merged the sections that only differed by their schedule (nothing was lost: a copy nobody used was deleted, another one in use was kept as for example *A-PM*). The registrar **chooses** the section from a list when
enrolling a student (A to C, plus any other section that exists) and does not type it; the coordinator can still make a section with another name in the *Sections* tab.

**Administrator > Enrolled and Not enrolled** (`/admin/enrollment/enrolled`, `/admin/enrollment/not-enrolled`) are two lists the administrator can only look at (the registrar enrolls the
students). They are the **Enrolled** and **Not enrolled** cards under *Accounts* on the administrator's dashboard.

**Excel for the whole setup.** At the top of the page, **Export to Excel** saves everything the school has and **Template and import (Excel)** opens the box (template, export of what exists, the file to import). One workbook has a sheet for each part: *School Years, Terms, Tracks, Strands, Courses, Curriculum* (a row for each course a strand takes in a term of a grade level), *Sections* (with the adviser by employee number), *Day Parts* (the hours of AM, MID and PM) and *Class Schedule* (the part of the day, the days and the hours of each course of a section), and the sheet *How to fill* (the steps, and for each column what to write and what to use in Excel). The columns that point to another sheet (the school year of a term, the track of a strand, the strand and the course of the curriculum ...) have drop-down lists made from that sheet; the list only warns when another value is typed, so a row can point to something that is already in the system. The import shows each row (ready, or the problem), then saves the rows in the order of the tabs through **the same methods the tabs use** (`SchoolSetupService`, `CourseScheduleService`), so every rule of the tabs applies and only the academic coordinator can import: a row that already exists (the same code, name or section) is changed, nothing is deleted when a row is missing, and a row that breaks a rule is left out with its reason (*Sheet, row N: ...*). Exporting the whole setup and importing it again changes nothing. Code: `Services/SetupSheets.cs`, `SetupWorkbookService.cs`.

## Announcements

**Announcements** are about **exams, holidays, the semester break, teachers' meetings and others** (each has its icon and color).

* The **academic coordinator** (`/coordinator/announcements`) writes the notices of the school: pick what it is about, write the title and the text, and choose who reads it: **everybody** (students and
  teachers), **the students** or **the teachers** (a *teachers' meeting* is always for the teachers), with an optional *show until* day. The page can show all of them or one kind at a time (tabs), and the coordinator
  edits and deletes them. The people it is for get a notification when it is posted (editing does not notify again).
* A **teacher** (`/teacher/announcements`, after *Calendar* in the menu) writes to **one of their own classes**: choose the course (subject and section), *Exam* or *Other*, and write it. Only the **students of that
  course** see it and are told. The page has two tabs: **To my classes** (the teacher edits and deletes their own) and **From the school** (the notices of the coordinator for the teachers).
* There is no announcements button in the header any more: students see them in the **Announcements** card at the right of their dashboard (the title opens the whole text); the teacher's and coordinator's pages list them. A teacher's announcement form no longer asks "What is it about?": a class announcement is just a title and text (its kind is *Other*; the coordinator's announcements keep their kinds).

**Excel.** The coordinator's page has **Export to Excel** (the announcements of the list: *Title, Message, About, For, Last day to show*) and **Template and import (Excel)**: the template (with drop-down lists for *About* and *For*, an example and a sheet that says how to fill it), a preview of every row (a row with a problem says why and is left out) and the import, which writes each row like the form does, so the people it is for are told. Code: `Services/AnnouncementSheets.cs`, `Components/Shared/SheetImportDialog.razor`.

Announcements that an administrator or a registrar wrote before these rules stay in the database; the coordinator can still edit and delete them. Code: `Services/AnnouncementService.cs`
(`announcements.category` and `announcements.assignment_id`).

## Academic provider: course library, sending and tracking

The **academic provider** belongs to the central team. They prepare what the teachers teach with and watch whether it reaches the students. (The academic coordinator used to send
course materials; that was **taken away from the coordinator** and is only for the provider now.) An administrator adds providers in **Users Management > Providers**; there can be several.

**Course library** (`/provider/library`) is the provider's library. **The provider sends three things only: the syllabus, the modules and the summative exams.** The assignments, quizzes, task performances, seatwork and recitations are the **teacher's** to make in their own copy of the course (the provider has no *Add Activities* button any more, and the library has no pages for them). In a course the buttons are **Add syllabus**, **Add module** (its box has **Template and import (Excel)** for many modules at once, with their files), **Add summative exam** and **Send to teachers**, whose list says what each thing holds, like *3 modules, 1 summative exam · sent to 2 of 3*. It opens on the **tracks** (Academic, Technical-Professional), each one a **row like a module of a course** (a numbered square, the name in bold, a line under it and an *Open* button, not a card); press a track to see its **courses** (the strands: ABM, STEM, HUMSS ...) the same way, and press a course to see its **subjects**: **after the strand, choose the term** (*1st Term* or *2nd Term*, each with how many subjects it has and how many already have a syllabus) and then its subjects as rows (with where it is studied, *Grade 11 · 1st Term*, a *Syllabus ready* or *No syllabus yet* badge and what is prepared: modules, assignments, quizzes, task performances, teachers), one library per subject; a search bar over the tracks looks in all the subjects of the school. Open a subject to prepare its **syllabus** (one per course), its **modules**
and its **summative exams**. Open a syllabus or a module (press its title) to see its text and its files. **Files** (a PDF, pictures, a text file, a Word document ...) can be attached to the syllabus, to a module and to every summative exam.

**Summative exams.** Under the modules of a course is the card **Summative exams**. **Add summative exam** asks for the title, the instructions (the paper), the files, the **highest score** and the **schedule**: *Opens* and *Closes* (Philippine time; it must close after it opens). The card's **Excel** button works like the one of the modules: a **template** (the sheet *Summative exams* with an example and the sheet that says how to fill it), an **export** of the course's exams (changed and imported again) and the **import** (a row with a problem says why and does not stop the others). Code: `Services/ExamSheets.cs`. An exam is kept as an item of the kind `Exam` (`material_items`, `visible_from_utc` is when it opens and `due_at_utc` when it closes) inside a hidden module of the course (`material_modules.is_exam_set`).

**Send to teachers** chooses what to send (the syllabus is the first thing on the list; if the subject has none, the dialog says so and offers to add it; then the modules and the summative exams) and which teachers' courses (the teachers
who have the course this school year): each teacher's course gets **its own copy as a draft, with the files**. A summative exam arrives in a draft module called **Summative exams** (one for the course, used again for the next exam), as an item of the kind exam with its schedule. The students can't see a draft: the teacher changes what they want (the text, the points, the due
dates, when it is posted) and posts it. The teacher is told (the bell) and the copy says *From the academic provider*. A module sent to a course is not sent to it again; when the teacher deletes
the copy, it can be sent again. The syllabus comes first in the course and has a *Syllabus* badge for the students. Code: `Services/MaterialService.cs`, `Components/Pages/Provider/CourseLibrary.razor`.

**Tracking** (`/provider/tracking`) has the tabs **Modules** (one line for each thing sent to one teacher's course, with its **state**, below; the syllabus and the summative exams are there too), **Teachers** and **Students** (what used to be the Users page). The tabs **Assignments**, **Quizzes**, **Task performance**, **Seatwork** and **Recitations** are there only when something of that kind was sent by an older version of the library (one line for each of them a teacher has in a course that came from the library: the item, the teacher, the state, the due date, how many students **opened** it and how many **handed it in** (a bar for each), with the same search and the Teacher, Grade level, Section and Course lists). A short line under the tabs says what the tab shows. Code: `Components/Pages/Provider/Tracking.razor`, `TrackingItems.razor`, `ProviderService.ListItemTrackingAsync`.

| State | Meaning |
|---|---|
| Not opened | Sent, and the teacher has not read it, edited it, opened one of its assignments or posted it. (Only looking at the list of modules of the course does not count.) |
| Opened · draft | The teacher opened it and it is still a draft. |
| Scheduled | The teacher chose a later time for the students to see it. |
| Posted | The students can see it. The line then shows how many of the course's students opened it, and how many opens its assignments had. |

The tabs count each state (the menu pop-up of *Tracking* links to them), and you can search a teacher, a course, a section or a module, narrow the lines with the lists **Teacher**, **Grade level**, **Section** and **Course** (they offer what was sent) and show only syllabi or only modules. The Teachers tab narrows by grade level, section and course, and the Students tab by grade level and section. **Details** of a line shows the steps
(sent, opened by the teacher, posted, opened by the students) with their dates, each assignment (opened by, handed in by) and **each student** (who opened the module and when, how many assignments),
with the students who have **not** opened it first and a switch to show only them. A student opens a module (or the syllabus) when they open its page, and an assignment, quiz or task performance when they open
its page; the first time is kept. Code: `Services/ProviderService.cs`, `ModuleService.MarkDeliveryOpenedAsync` (the teacher), `ModuleService.OpenModuleAsync` and `WorkService.RecordViewAsync` (the students);
the data is in `material_deliveries.opened_at_utc`, `module_views` and `item_views`.

The **Teachers** tab lists each teacher who has a course this school year with what they received, opened and posted, how many are still waiting (and how old the oldest is) and
how long they take to open on average; the **Students** tab lists the students who were given something, with how much of it they opened (the ones who opened least first). A name opens that person's page
(`/provider/tracking/users/<id>`): for a teacher everything sent to them with its state; for a student everything posted to them with when they opened it (or *Not yet*).

**Analytics** (`/provider/analytics`): the share opened by the teachers, the average time to open and to post, the share of modules and of assignments the students opened, **week by week**,
**by kind** (syllabus, module, assignment, quiz, task performance), **by section** and **by course**, the teachers with the oldest unopened items, and the modules the students open least.
The **Dashboard** (`/provider`) sums it up: sent, opened, posted, opened by students, the lines waiting longest, what was opened lately and the courses that still have no syllabus.

## Academic coordinator: teacher evaluation and analytics

**Teacher evaluation** (`/coordinator/evaluations`): each evaluation has **Open** and, **at its right, Copy**: it makes a new draft *Copy of ...* with the same instructions and questions (nothing is sent, and the answers are not copied) and opens it to be changed. **New evaluation** starts with the usual 16 questions about a teacher in areas such as *Teaching* and *Classroom management*; the coordinator changes,
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
to remind them). **Close now** ends it early; the due date can be moved later. **Excel.** *Template and import (Excel)* (top right of the list) gives the template (the sheet **Evaluation** with the title, the instructions and the due date, and the sheet **Questions** with the question, its type (a drop-down list of the seven kinds), its area and its options) with one example of each kind of question; importing it **makes a new draft evaluation** with those details and questions, ready to be sent (a problem in the file is listed and nothing is made). Each row of the list has an **Excel** button and the evaluation page has **Export to Excel** (the same workbook, so it can be changed and imported again). Code: `Services/EvaluationService.cs`, `EvaluationSheets.cs`.

**Analytics** (`/coordinator/analytics`) is about the teacher evaluation: the answers and how many students answered, the average rating, the teachers **ranked** by their average (with
*Outstanding / Very satisfactory / Satisfactory / Fair / Needs improvement*), how the students answered the 5-point scales (1 to 5), the Yes/No, ranking and select-all results, the averages **by area** and **by question**, the trend from one evaluation to the
next, and for each teacher a *Details* view with the courses, the questions and the comments without names.

## Enrolling a student (registrar)

Menu: **Registrar > Enrollments** (`/registrar/enrollments`). Fill in:

| Box | What to put |
|---|---|
| LRN | The 12 digits of the Learner Reference Number. |
| First name, middle name, last name, name extension | The student's name. The middle name is optional. The **name extension** (Jr., Sr., III ...) is optional too, up to 10 characters; it is written after the last name on the form and in the lists, and a student who has one keeps it. |
| Term | The term to enroll in. The current term is chosen for you. |
| Grade level | 11 or 12. |
| Track, strand | ACAD (BUSSENT, EDUC, ENG, MED, USP) or TECH-PRO (CD1, HT1, HT2). Pick the track first, it filters the strands. |
| Section | Pick one of the sections that exist for that strand and grade level (A, B or C). There is no schedule to choose: each course has its own. |
| Phone number | **Optional.** A mobile number like `0917 123 4567`, saved as `+639171234567` on the student's account. A student who already has a number keeps it. It is kept for the school but **not printed** on the enrollment form. |

There is nothing to pick for the subjects: the student gets **all the subjects of the strand** for that grade level and term (the form tells which). Press
**Enroll student**. The page then shows what was saved and, for a new student, the **sign-in details to hand over**
(with *Copy the login* and *Print*).

**Names** are written with capital letters whatever the registrar types. **A section can have 40 students** (the academic coordinator changes the limit in *Academic setup > Sections*; the section choice says *(full)* when a section has reached it, the form and the import refuse a full section with the numbers, and the dashboard's **By section** card shows `n of 40`). **A student who changes strand starts again in Grade 11** (the form says so).

**Import from Excel.** The **Import from Excel** button (top right) opens a box: *Download the template* (LRN, names, grade level, track, strand and section, with drop-down lists and a sheet that says how to fill it), fill it (or paste the students), choose the **term** and the file. Every row goes through the same rules as the form (a student who has an account keeps it; the capacity; the strand), a row with a problem does not stop the others, and the result lists every row with its reason and can be **downloaded as Excel with the email and password of each new account** (at most 500 rows). Code: `Services/EnrollmentImportService.cs`.

### The enrollment form to print

The **Print** button of an enrollment prints the **enrollment form** on two A4 pages, as a clean document with **no frame and no box for a picture** (the screen shows only the receipt; `Components/Shared/EnrollmentPrintSheet.razor`).
**Page 1** has the letterhead (the **school logo at the upper left**, the **school name** with its tagline and contact line next to it, the enrollment number and the date at the right), the title *Enrollment Form* with the school year and term, and four parts written on fine lines: **Learner information** (the name in capitals, with the name extension, and the LRN in twelve boxes; there is no contact number), **Enrollment details** (the **grade level written out as words**, then the track, strand and section; the school year and the term are in the title, so there is no schedule or term here), **Courses enrolled** (a table without a number column: the **course**, its **units** and, **at its right, its schedule** (for example *AM · MWF · 8:00 AM – 9:30 AM*), and a last row with the **total units**) and **e-LMS account of the learner** (email and the first password, only for a new account). **There is no signature block on this page**: the page ends with the course table and the e-LMS account (the signatures are on page 2). The print page has no margin of its own, so the browser's header and footer (the date and title at the top, the address at the bottom) do not show when *Headers and footers* is off in the print window; the form sets the page margin itself.
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

**The courses are shown at the right of the sign-in details**, in the row above the Enroll button: the courses the student will take (the strand's courses for that grade level and term, with their code, units and class time (shown once the section is chosen) and the total units), so the registrar sees them before enrolling; they change when the strand, the grade level, the term or the section changes.

**Re-enroll** (the button of a student in *Student's Accounts* and on the dashboard) opens a **box over the page** (not a new page) already filled with the student's last **grade level, track and strand**; the **section is empty** and is chosen again; the box lists the courses and has a *Re-enroll* button, then says it is done. **Edit** (the registrar's list) changes a student's name, **term, strand, section and courses** (the courses of a section and strand), and **Drop out** is next to it. **If the registrar changes the strand (the course) in the box, the student leaves the old one:** the old open enrollments are ended (`Dropped`, kept in the history), the student starts in the new strand at **Grade 11** (a different strand cannot continue at Grade 12; the box says so after saving: *left <strand>, now in <strand>*), and the student's row follows. Before this rule the old account stayed enrolled, so the student was in two strands and came back to Grade 11 of the old one. Code: `Components/Shared/ReEnrollButton.razor`, `EnrollmentActions.razor`, `EnrollmentService.EnrollAsync` (`EnrollmentReceipt.LeftStrand`).

The form starts from where the student was the last time: the **grade level** (one more when the term is in a newer school year), the **track** and
the **strand**, and the **section** they had. The registrar only changes what is different (usually the section).
The **Enroll** link of the *Next term* column opens the form this way, so a student who was never enrolled is the only one who needs track and strand picked.

### Student's Accounts

**Registrar > Student's Accounts** (`/registrar/student-accounts`) lists every enrolled student, newest first, 20 per page. Search by name
or LRN and filter by term, strand and **next term** (enrolled or not). The **Courses** button of a row (for example "8 courses") opens a small pop-up over the page with the
student's courses, so the columns do not move. The **Final grade** column, just left of *Next term*, is the average of the grades of the student's courses in that term (points
earned divided by points possible of the graded work; courses nobody graded yet are left out; "No grades yet" when none is), with what it means (*Satisfactory*, ...). The dashboard (`/registrar`) shows how many students are enrolled this
term, today, by strand and **by section** (every section of the school year with the students enrolled in it this term), and the **Not enrolled** section. The dashboard has the **At a glance** cards, then **four cards in two columns and two rows**: at the top left **Not enrolled**, at the top right the **Latest enrollments**, at the bottom left **By strand** and at the bottom right **By section**.

**The next term and unenrolling.** The next term is the term after the current one (set in the school setup). The **Next term** column says whether the
student is enrolled for it: *Enrolled* with an **Unenroll** button (it asks first, then removes the student's enrollment in the next term and its subjects;
the current term is untouched), or *Not enrolled* with an **Enroll** link to the form, already filled with the LRN and the next term. The registrar enrolls a
student for the next term with the same form (pick that term).

**Not enrolled** means: a student at school (not archived) with no enrollment in the next term (or in the current term when there is no next term).
It is shown on the registrar's dashboard (with an Enroll link for each student) and, for the administrator to look at, on the *Not enrolled* card of the administrator's dashboard.

## Managing users (administrator)

**Administrator > Users Management** (`/admin/users`). There are **three lists**: **Students**, **Teachers** and **Staff / Non-teaching** (the registrar, the guidance counselor, the academic coordinators and the academic providers, in one list with a **Role** column; the administrator is not in the list). Each tab shows its number of accounts. Above the table: a search box (name, LRN or employee number, email), a **Role** choice (staff list), the **sort** (*Name A to Z*, *Newest to oldest*, *Oldest to newest*), at the left of the **Status** filter (all, active, inactive). **A green dot on the picture of a person means online** (that person has a page open now; grey means not; the list looks again every 30 seconds). The students' list has a **Courses** column (the courses of
their enrollment) and the teachers' list a **Section** column (the sections a teacher teaches in; a teacher in **more than one section** has a small **"2 sections" button** that opens a pop-up with them, like the Courses button) and, **separately, a Courses column** (the courses with their section). The Courses column is a small **"6 courses" button**: press it and a pop-up lists them (the same pop-up as the registrar's list; for teachers each line is the course and its section, like *BUSINESS ACCOUNTING · 11-BUSSENT-A*). There can be several academic coordinators and **several academic providers** (there is only one
administrator, one registrar and one guidance counselor). Names are written with **capital letters** (`juan dela cruz` becomes `Juan Dela Cruz`).

- **Edit** changes the name, email, LRN (students) or employee number (staff), phone number and the **new password**
  (leave it empty to keep the old one). The **Status** choice (**Active** or **Inactive**) decides whether the person can sign in (the Add form has it too, so an account can start inactive).
  Changing the email or the password, or switching an account off, signs that person out.
  Changing a student's name or LRN also updates it in their enrollments.
- **Add student** (Students tab) enrolls a new student for a term, with the same rules and the same automatic email and password as the registrar's form (see *Enrolling a student*), and shows the sign-in details to hand over. **Add teacher** and **Add staff** make an account with the password you type. **Add staff** (Staff tab) has a *Role* list that only has the roles the school can still have: the registrar, the guidance counselor and the academic coordinator are one each, so they are taken off the list once the school has them; the academic provider is always in it (there can be many providers). **The button *Add role* next to the Role list** makes a role of the school's own, like *Assistant Registrar*: type the name (2 to 40 letters, numbers, spaces or dashes; not the name of a role that is there already) and choose the role it **works like** (registrar, guidance counselor, academic coordinator or academic provider). The people who have it use the same pages and permissions as that role, the staff list and the card in the menu show the new name, and **many people can have it** (the one-registrar rule is only for the real registrar). At most 30 such roles; they are kept in the table `custom_roles` (`users.custom_role_id`). Code: `UserManagementService.AddRoleAsync`, `ListCustomRolesAsync`. **Add teacher** also asks where the teacher works
  (all optional): **Track**, **Strand**, **Section** (the sections of that strand) and the **Courses** of that section, with a tick box for each
  (and *Select all*). The courses are given to the teacher for that section right away, and the teacher's track, strand, section and grade level are saved on
  the teacher. The choices are checked first (the strand must be in the track, the courses must be courses of that strand at that grade level, and a
  course another teacher already teaches to that section can't be given), and a wrong choice adds nothing. Code: `UserManagementService.AddTeacherAsync`.
- **Drop out** (a student's row; the registrar has the same button on the enrollments list) asks for a note and sets the student's status to **Dropped out**: the account can't sign in, the enrollments are kept, and the student **is not in the graduating list** of the archive (the archive says *Dropped out*). The **Status** choice of a student's Edit box has *Active*, *Graduated* and *Dropped out*. Code: `StudentLifecycleService.DropOutAsync`.
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
- The **courses** are read from the database (`Data/Queries/PublicCourses.cs`): each strand with its
  subjects per term (the number of courses of each grade level and term, and for a course its units). **Click a strand** and its courses **drop down right under it** (not a box in the middle of the page): its **grade levels** (Grade 11, Grade 12) and, under each, its **terms** (1st Term, 2nd Term) with the courses of each. Pressing the strand again, Escape, or a click anywhere else folds it up, and opening another strand folds the first one (`data-drop-toggle` in `wwwroot/app.js`; the panel is plain HTML with `hidden`). To change them, change the tables `strands`, `subjects` and `strand_subjects` (see
  [docs/database.md](docs/database.md)).
- The big picture at the top is three photos that change by themselves (the dots pick one). They are the files
  `wwwroot/images/hero/hero-1.jpg` ... `hero-3.jpg` (each one a different scene): replace a file to change a photo (keep them under about 400 KB so the
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
| `Components/Shared/` | Small building blocks the pages are made of: `Card`, `StatCard`, `Badge`, `Avatar`, `PageHeader`, `EmptyState`, `Button`, `ButtonLink`, `Field`, `Modal`, `Notice`, `SearchField`, `FilePreviewPane`, `EnrollmentTable`, `Brand` and `Icon`. |
| `Components/Pages/` | The pages, one folder for each part of the system, one page and one stylesheet each (see below). |

Good to know:

- **Feedback is a pop-up.** What a page says after an action ("Saved", or what went wrong) is `Components/Shared/Notice.razor`: a box at the **top center of the screen**, like the box of the achievements, so it is easy to see (a green top edge for *Done*, a red one for *Please check*), with the message and an **OK** button (Escape closes it too). It opens over any form that is still open. The red lists of problems on a form (`FormErrors.razor`: a wrong value, a taken code, ...) are shown the same way, **fixed at the top center** with an **X** to close them, instead of a bar inside the page that moves the form down.
- **Every search bar is the same one and in the same place**: the search box is always the **first thing at the left** of the bar (you search names there) and the choices that narrow the list (the selectors) are **at its right on the same row**; on a phone they go under it. The rule is in `Components/App.razor.css` (`.filter-bar`), so a new list only has to put a `SearchField` and its `Field` lists in a `.filter-bar`. `Components/Shared/SearchField.razor` is a wide box with a magnifier at the left, an **X** to clear it and a **Search** button inside, a short hint under it when it helps, and a label above. Lists put it with their other filters (labelled lists) in a `.filter-bar` row (the *Referrals*, *Tracking* and *Security logs* bars use `.filter-bar.one-row`: a **narrow search box with Status, Problem, Grade level and the other choices beside it on one single row**, no inner Search button: press Enter) (styled once in `Components/App.razor.css`): the search takes the room, the filters sit next to it, and everything wraps on a phone. Pages that look as you type use `Live`; pages that search on Enter or on the button use the default; static pages use `Static` (a plain form that reloads the page).
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
Services/                  What the people do: EnrollmentService, UserManagementService, MessageService, NotificationService, ...
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
3. Wrong passwords are counted. After 5 in a row the account is locked for **5 minutes** (the `Auth` section in `appsettings.json`; the administrator can change both numbers in *System Settings*, and a number saved there wins). The lock runs from the **5th wrong try** (not from the first one), and while it lasts even the right password is refused. The login page then shows a **box over the email with a timer** (*Try again in 09:42*) that counts down by itself and says when the time is over; the timer is still there if the page is opened again. **The timer is a lock of that browser:** while it runs, no sign-in is accepted from it, with any email and any password (even the right one, and even the right one of another account), and the password is not looked at; the account also has its own lock in the database, so another browser is refused too. (On a shared computer, the next person waits for the timer as well.) An **email with no account** is answered with **User Not Found** (it is not counted and does not lock; the page is the same for every role). The moment the lock ends is kept in `users.lockout_end_utc`; the administrator can also unlock an account in *Users Management*.
4. A signed-in user is re-checked against the database every few minutes, so deactivating an account (or changing
   its `security_stamp`, which editing the email or password in **Users Management** does) signs it out.
5. **One sign-in at a time for an account.** While somebody is using an account in a browser or on a device, signing in with it somewhere else is refused: the login page shows an alert at the **top center** (*Can't log in, someone is logged in. This account is signed in on another device or browser ...*) and the form stays as it was. The open sign-in is kept in `users.active_session_id` (its number is also in the sign-in cookie) and `active_session_seen_utc`: every page that is open says it is still there once a minute, so a browser that was closed without *Log out* frees the account after `Auth:SessionIdleMinutes` (3). *Log out* and a password change free it at once. If the account is signed in somewhere else after an old sign-in was not seen for a while, the old page notices by itself, signs out and the login page says *You were logged out*. Turn the rule off with `Auth:SingleSession: false`. (A browser that is signed in already, opening `/login` again, is sent back to its page with the alert *Somebody is already signed in on this browser*.) Code: `Auth/AuthService.cs` (`LoginAsync`, `TouchSessionAsync`, `EndSessionAsync`), `AuthCookieEvents.cs`, `Components/Shared/TopAlert.razor`.
6. Two-factor sign-in is not built yet. `users.phone_number` is stored (as +639...) for when it is.

## The sound of the notifications

**The sound of a notification.** A short sound plays when a new message or notification arrives while a page is open. **Each person chooses it in Settings** (*Notification sound*: two tones, chime, bell, pop, knock, the sound of the school, or **their own file** that they add there; the choice is kept in `users.notification_sound_choice` and the files in the table `notification_sounds`: one row per person, and one with no person for the school, which the administrator adds in System Settings). The files are served by `/account/notification-sound` (the person's own) and `/account/school-sound` (the school's) to signed-in people only (`Services/NotificationSoundService.cs`). A browser keeps sound off until the person has clicked or typed on the page once; when a sound cannot play for that reason a small button *Turn on the sound* appears, and one click turns it on. With no choice and no school sound, a file called `notification.mp3` (or `.ogg`, `.wav`, `.m4a`) in `wwwroot/sounds/` is played if it exists; without it, two short tones that the browser makes are played. The system can't take a sound from a YouTube video by itself (that is not allowed and the site has to be reached); download a sound you may use and save it in that folder (see `wwwroot/sounds/README.txt`).
