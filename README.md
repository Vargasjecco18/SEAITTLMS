# SEAITT-LMS

The e-LMS for SEAITT (Senior High School, Legazpi). Blazor Web App on .NET 9 with a MySQL database,
set up without a `dotnet new` template and with its own login (no ASP.NET Core Identity package).

What exists so far:

- **A public home page** at `/` for visitors: photos of the school, its courses (read from the database) and a
  **Log in** button.
- **Custom login** with the school's picture next to the form: email + password, PBKDF2 password hashing, cookie
  sign-in, account lockout, "keep me signed in". After signing in, everybody lands on the page of their role.
- **Users and roles**: Admin, Teacher, Student (with LRN), Registrar, Guidance Counselor, each with its own table
  holding the person's name and details.
- **Registrar pages** (for the registrar only): *Dashboard*, *Enrollments* (the form that enrolls a student; the
  student's email and password are made automatically) and *Student's Accounts* (a searchable list).
- **Administrator pages** (for the administrator only): *Dashboard* and *Users Management*: one list per role, with
  search, **edit**, **delete** and add (for staff).
- **My account** for everybody.
- **Enrollment** by school year and **term**: tracks, strands, schedules (AM, MID, PM), subjects, sections,
  enrollments. The database comes with the school year 2026-2027, its two terms, the ACAD and TECH-PRO tracks, their
  strands and the subjects of grade 11.
- **A header** with a search bar in the middle and, at the right, **messages**, **notifications** and **settings**
  (light / night / auto look, animations on or off). The signed-in person's name and role are at the bottom of the menu.
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
click the lightning bolt. It deletes the `seaittlms` schema and builds everything again, including the five roles and
the school setup (school year, terms, tracks, strands, schedules, subjects). Then run `dotnet watch` to create the
administrator.

Any other users you had are gone. Add the staff again in **Users Management**, and the students by enrolling them.

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
| **Search** (middle) | Type and press Enter: the search page lists the pages you can open and, depending on your role, the people you work with. An administrator finds every account, a registrar finds students. |
| **Messages** (speech bubble) | A box with your conversations. Type a name to write to somebody. A student can write to teachers, the guidance counselor and the registrar; a teacher also to students and the other staff. **Messages can't be sent from 7:00 PM to 7:00 AM Philippine time**: the box says so and the message field is switched off. Reading is always possible. The number on the icon is how many messages are unread. |
| **Notifications** (bell) | What the system wants you to know (a new message, and later new modules, grades and deadlines). The number is how many are new. Click one to open the page it is about. |
| **Settings** (gear) | **Light**, **Night** or **Auto** (follows the computer), and **Animations** on or off. The choice is kept in your browser. |
| **Name and role** (bottom of the menu) | Who is signed in. Click the name to open My account, the arrow button signs you out. |

The code: `Components/Layout/HeaderTools.razor` (the three buttons), `MessagesPanel.razor` (the Messages box),
`Services/MessageService.cs` (the rules and the 7 PM to 7 AM closing time), `Services/NotificationService.cs`.

## Who sees what

After signing in (and whenever a signed-in person opens `/`), the app sends everybody to the page of their role
(`Auth/RoleHome.cs`). A page that a role may not open shows *Access denied*. The menu only lists what the role can open.

| Role | Lands on | Can open |
|---|---|---|
| Admin | `/admin` | Dashboard, **Users Management**, My account |
| Registrar | `/registrar` | Dashboard, **Enrollments**, **Student's Accounts**, My account |
| Teacher | `/teacher` | **Dashboard**, **Subjects** (modules, activities, quizzes, task performances, grading), **Attendance**, **Gradebook**, **Calendar**, My account |
| Student | `/student` | **Dashboard**, **Subjects** (modules, work, quizzes), **Grades**, **Calendar**, My account |
| Guidance counselor | `/account` | My account (their pages come next) |

The registrar pages belong to the registrar: **an administrator cannot open them**. To get a registrar, sign in as the
administrator, open **Users Management > Registrars**, press **Add registrar** and give the person an email and a first
password. Then sign in as that registrar to enroll students.

## Subjects, modules and work

**The administrator** gives each teacher their subjects: **Users Management > Teachers > Subjects**, choose the section and
the subject (only the subjects of that section's strand are listed). A subject of a section has one teacher.

**Teacher > Subjects** shows one colored card per subject. **Open** shows its modules. A new module is a **draft**: students
cannot see it. **Post now** shows it to the students (they get a notification), **Schedule** picks the day and time it
appears by itself, **Hide** takes it back. At the bottom of a module are its **Activities**, **Quizzes** and **Task
performances**; each has a due date (and can have its own time to appear). Open an item to see who handed in, read the
work, give a score and feedback (the student is told).

**Quizzes** have questions (multiple choice, true or false, short answer) that are graded by themselves, an optional time
limit, shuffled questions and choices, and the **anti-cheating watch**: while a student takes the quiz the page tells the
system when they leave the tab or window, copy, cut or paste (those are blocked too). Each of those is a warning; after
the number you choose the quiz is handed in by itself and **flagged** for you, with a log of what happened. The time is
checked on the server, and a quiz that was left open is handed in when its time is over.

**Student > Subjects** shows the subjects they are enrolled in as cards (or a list), filtered by term. **Open** shows the
posted modules; each ends with three tabs: **Activities**, **Quizzes**, **Task performance**. **Read lesson** opens the
module's text. A module that was never opened is marked **New**.

The code: `Services/TeachingService.cs`, `ModuleService.cs`, `WorkService.cs`, `QuizService.cs`, `AutomationService.cs`
(the background job), the pages in `Components/Pages/Teacher/` and `Components/Pages/Student/`, and the anti-cheating watch in
`wwwroot/app.js` (`seaitt.startQuizGuard`).

## Dashboards, calendar and the widgets at the right

**Student > Dashboard** (`/student`) shows what to do first: the work still to do with its due dates (overdue ones in red), the
modules not opened yet, the grade of every subject, the latest scores and the **achievements** (perfect score, high grades,
quiz ace, always on time, curious mind: earned ones are colored, the others show how far you are).

**Teacher > Dashboard** (`/teacher`) shows the **unchecked activities** (work handed in that still needs a score), the
deadlines of the next two weeks with who handed in, the modules students can't see yet (drafts and scheduled ones) and the
quiz attempts the anti-cheating watch flagged.

The column at the right of the dashboards, the subject pages and the grades page (`Components/Shared/AsideWidgets.razor`) holds a
small **calendar** (a dot on every day with something, click a day to see it), the **to-do list** (add, check off, delete; each
person has their own) and an **Up next** list, so the screen has no empty space. On a phone the widgets go under the page.

**Calendar** (`/calendar`, students and teachers): a **Month** and a **Week** view with Previous, Today and Next. It shows the
deadlines of the work (activity, quiz, task performance), the modules that appeared (for a teacher also the ones scheduled), and the
to-do entries that have a date. Done work is struck through, overdue work is red. Click a day number to open its week.

**Student > Grades** (`/student/grades`) is the grade and progress page: the average of every subject (points earned divided by
points possible of the graded work), the passing mark of 70 drawn on the bars, the latest scores, and the graded works of
each subject. A grade under 70 is shown in red with a warning; a quiz whose result the teacher hides is not counted yet.

The code: `Services/DashboardService.cs`, `CalendarService.cs`, `TodoService.cs`, `GradeService.cs`, `AchievementService.cs`.

## Attendance and the gradebook (teacher)

**Teacher > Attendance** (`/teacher/attendance`): choose the subject and the date, press **Present**, **Late** or **Absent** for each
student (or **Mark all present** first), then **Save attendance**. An earlier day can be opened and corrected; a day that has
not come yet can't be marked. Under it the **Attendance summary** shows each student's present, late and absent days and the
attendance rate (days present or late divided by days marked; under 80% is red). The dashboard says in which subjects attendance
was not taken today. Students see their rate on **Grades**, and five days all present earn the *Perfect attendance* badge.

**Teacher > Gradebook** (`/teacher/gradebook`): choose the subject; every student is a row, every activity, quiz and task
performance of a posted module is a column (with its highest score), and the last column is the average. A score under 70% is
red, work waiting for a score says *To check* (click it to open the work). **Edit grades** turns a row into boxes; **Save**
changes the scores (all of them are checked first, a wrong one saves nothing) and tells the student. Feedback the teacher wrote
is kept.

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
| Enrolled subjects | The subjects of that strand for that grade level and term are ticked for you. Untick what the student does not take. At least one is needed. |

Press **Enroll student**. The page then shows what was saved and, for a new student, the **sign-in details to hand over**
(with *Copy the login* and *Print*).

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

### Student's Accounts

**Registrar > Student's Accounts** (`/registrar/student-accounts`) lists every enrolled student, newest first, 20 per page. Search by name
or LRN and filter by term, strand and schedule. The dashboard (`/registrar`) shows how many students are enrolled this
term, today, by strand and by schedule.

## Managing users (administrator)

**Administrator > Users Management** (`/admin/users`). There is one tab per role (Students, Teachers, Registrars, Guidance,
Administrators), each with its number of accounts, and a search box (name, LRN or employee number, email).

- **Edit** changes the name, email, LRN (students) or employee number (staff), phone number and the **new password**
  (leave it empty to keep the old one). The **Account is active** switch decides whether the person can sign in.
  Changing the email or the password, or switching an account off, signs that person out.
  Changing a student's name or LRN also updates it in their enrollments.
- **Add** (teachers, registrars, guidance counselors, administrators) makes an account with the password you type.
  Students are not added here: they get their account when the registrar enrolls them.
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
  subjects per term. To change them, change the tables `strands`, `subjects` and `strand_subjects` (see
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
| `Components/Shared/` | Small building blocks the pages are made of: `Card`, `StatCard`, `Badge`, `Avatar`, `PageHeader`, `EmptyState`, `Button`, `ButtonLink`, `Field`, `Modal`, `EnrollmentTable`, `Brand` and `Icon`. |
| `Components/Pages/` | The pages, one folder for each part of the system, one page and one stylesheet each (see below). |

Good to know:

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
Components/Shared/         Card, StatCard, Badge, Avatar, Icon, Modal, Field, ... (each with its own .razor.css)
Components/Pages/          The pages, in one folder per part of the system:
  Public/                    Index (the home page), Login, AccessDenied, NotFound, Error
  Admin/                     Dashboard, UsersManagement
  Registrar/                 Dashboard, Enrollments (the form), StudentAccounts (the list)
  Teacher/                   Dashboard, Subjects, Subject (modules and work), ItemSubmissions (results and grading), QuizBuilder, Attendance, Gradebook
  Student/                   Dashboard, Subjects, SubjectModules, ModuleLesson, WorkPage (hand in), QuizTake, Grades
  Account/                   MyAccount, Search, Calendar (students and teachers)
wwwroot/                   favicon.png, app.js and images/ (logo.png, hero/: the photos of the home page and the sign-in page)
appsettings.Local.example.json   Copy it to appsettings.Local.json (git ignores that one) and put your passwords in it
database/                  The SQL file for starting over in MySQL Workbench and the script that generates it
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
