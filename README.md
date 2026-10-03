# SEAITT-LMS

The e-LMS for SEAITT (Senior High School, Legazpi). Blazor Web App on .NET 9 with a MySQL database,
set up without a `dotnet new` template and with its own login (no ASP.NET Core Identity package).

What exists so far:

- **Custom login**: email + password, PBKDF2 password hashing, cookie sign-in, account lockout, "keep me signed in".
- **Users and roles**: Admin, Teacher, Student (with LRN), Registrar, Guidance Counselor, each with its own table
  holding the person's name and details.
- **Classes and enrollment** by school year and **term**: tracks, strands, subjects, sections, class offerings, enrollments.
- **Pages**: a **Home** page for everybody (greeting, the current term, your account details) and an **Admin** page
  for administrators (accounts per role, the school setup at a glance, the latest accounts).
- **A header and a menu** with icons, light and dark mode (it follows your computer), and a phone-friendly layout.

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
3. Open http://localhost:5000 and sign in as **admin@legazpi.seaitt.edu.ph** with the administrator password from step 1.

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
existing administrator, run this in MySQL Workbench (emails are stored in lower case):

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
click the lightning bolt. It deletes the `seaittlms` schema and builds everything again, including the five roles and the
four tracks. Then run `dotnet watch` to create the administrator.

Any other users you had are gone, and there is no screen for adding users yet.

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

## Pages and styling

Every page and component has **its own stylesheet next to it**, named after it: `Login.razor` is styled by
`Login.razor.css`, `Admin.razor` by `Admin.razor.css`, and so on. Blazor (it is called *CSS isolation*) applies each
file only to its own component, so the same class name, like `.card` or `.row`, can be used in two files without them
affecting each other. There is no big shared stylesheet.

| Files | What they style |
|---|---|
| `Components/App.razor.css` | The colors, sizes and dark mode, written once as variables (`--brand`, `--surface`, `--text`, ...), and the page background and font. Change a color here and it changes everywhere. |
| `Components/Layout/` | The header (`MainLayout`), the menu on the left (`NavMenu`), the account menu (`UserMenu`) and the sign-in layout (`AuthLayout`). |
| `Components/Shared/` | Small building blocks the pages are made of: `Card`, `StatCard`, `Badge`, `Avatar`, `PageHeader`, `EmptyState`, `ButtonLink`, `Brand` and `Icon`. |
| `Components/Pages/`, `Components/Account/` | One page each: `Home`, `Admin`, `Login`, `Error`. |

Good to know:

- To style something another component draws (the `<a>` that `NavLink` makes, the `<input>` of `InputText`), start the
  selector part with `::deep`. `Components/Layout/NavMenu.razor.css` shows it.
- **Icons** are from [Lucide](https://lucide.dev). Use them with `<Icon Name="house" />`. The available ones are listed in
  `Components/Shared/IconSet.cs`; to add one, open its `.svg` file from Lucide, copy what is inside the `<svg>` tag onto
  one line and add it there. The licence is in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
- **A new page**: add `Components/Pages/Something.razor` starting with `@page "/something"` (and
  `@attribute [Authorize(Roles = AppRoles.Admin)]` if only administrators may open it), give it a
  `Something.razor.css`, then add a link in `Components/Layout/NavMenu.razor`.
- The data a page shows is read by small query classes in `Data/Queries/`, so the page itself stays mostly markup.
- `wwwroot/app.js` only closes the menus when you click elsewhere or press Escape. The pages work without it.
- Times are stored in UTC and shown in Philippine time (UTC+8), see `Components/Shared/PhilippineTime.cs`.

## Project layout

```
Program.cs                 Wiring: database, login cookie, pages
Auth/                      Custom login: PasswordHasher, AuthService, cookie checks, UserAccountService
Data/                      AppDbContext, entities, EF configurations, migrations, startup initializer
Data/Queries/              What the Home and Admin pages read from the database
Components/App.razor.css   The theme: colors, sizes, dark mode
Components/Account/        Login and access-denied pages
Components/Layout/         Header, menu, account menu, sign-in layout
Components/Shared/         Card, StatCard, Badge, Avatar, Icon, ... (each with its own .razor.css)
Components/Pages/          Home, Admin and the error page
wwwroot/                   favicon.svg, app.js
appsettings.Local.example.json   Copy it to appsettings.Local.json (git ignores that one) and put your passwords in it
database/                  The SQL file for starting over in MySQL Workbench and the script that generates it
docs/database.md           Tables, relationships and the rules the database enforces
THIRD-PARTY-NOTICES.md     Licence of the icons
```

## How the login works

1. `/login` is a normal form. `AuthService` looks the user up by email, checks the password hash and the lockout,
   and the page writes the sign-in cookie.
2. Every page needs a signed-in user unless it has `@attribute [AllowAnonymous]`. Restrict a page to roles with
   `@attribute [Authorize(Roles = AppRoles.Admin)]`. A signed-in user who opens a page they may not see gets an
   *Access denied* page.
3. Wrong passwords are counted. After 5 in a row the account is locked for 15 minutes (`Auth` section in `appsettings.json`).
4. A signed-in user is re-checked against the database every few minutes, so deactivating an account (or changing
   its `security_stamp`) signs it out.
5. Two-factor sign-in is not built yet. `users.phone_number` is stored (as +639...) for when it is.
