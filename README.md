# SEAITT-LMS

The e-LMS for SEAITT (Senior High School, Legazpi). Blazor Web App on .NET 9 with a MySQL database,
set up without a `dotnet new` template and with its own login (no ASP.NET Core Identity package).

What exists so far:

- **Custom login**: email + password, PBKDF2 password hashing, cookie sign-in, account lockout, "keep me signed in".
- **Users and roles**: Admin, Teacher, Student (with LRN), Registrar, Guidance Counselor, each with its own profile table.
- **Classes and enrollment** by school year and **term**: strands, subjects, sections, class offerings, enrollments.

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
that already exists. To change the email of an existing administrator, run this in MySQL Workbench (emails are
stored in lower case):

```sql
UPDATE seaittlms.users SET email = 'admin@legazpi.seaitt.edu.ph' WHERE email = 'admin@seaitt.edu.ph';
```

### Creating the tables

Pick one way. They all create the same tables.

1. **Automatically** (default in Development): just run `dotnet watch`, as in step 2 above.
2. **In MySQL Workbench**: open [database/seaittlms_schema.sql](database/seaittlms_schema.sql) with
   *File > Open SQL Script...* and click the lightning bolt. Run it once, on an empty `seaittlms` schema. The app
   starts normally afterwards because the script also records the migrations. To see the tables as a diagram, use
   *Database > Reverse Engineer...* and pick `seaittlms`.
3. **With the EF command line**:

   ```bash
   dotnet tool install --global dotnet-ef --version 9.0.20
   dotnet ef database update
   ```

### Already created the tables? Upgrade instead of starting over

Don't run `seaittlms_schema.sql` a second time (the tables already exist). To get the newest changes **and keep
your data**, either start the app in Development (`dotnet watch`, it upgrades by itself), or in Workbench run
`SELECT * FROM __EFMigrationsHistory;` and then run the files in [database/upgrades](database/upgrades) that are newer
than the last row, oldest first.

### Something broke? Start over

Use this when the database is broken, for example you dropped or changed tables by hand, an SQL script stopped halfway
("Table ... already exists" when the app starts), or MySQL Workbench shows *Error loading schema content ... Error Code:
1356 ... View 'seaittlms.v_...' references invalid table(s) or column(s)*. (The `v_...` views read `users` and the role
tables, so they break when those are missing or changed.)

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

Only want to clear the Workbench error without erasing anything? Run this in Workbench (it only drops the five views):

```sql
DROP VIEW IF EXISTS seaittlms.v_administrators, seaittlms.v_guidance_counselors, seaittlms.v_registrars,
                    seaittlms.v_teachers, seaittlms.v_students;
```

If Workbench stops an SQL file with *Error Code: 1175 ... safe update mode*, your copy of the file is old: pull the
latest version (current files work with Workbench's default settings).

The SQL files are generated from the migrations, don't edit them by hand. After a schema change, regenerate them with
`bash database/export-schema.sh` (on Windows, in Git Bash; it needs the `dotnet-ef` tool and a running MySQL). The same
command creates `database/seaittlms_schema.sql` and `database/upgrades/` again if they are missing from your copy.

### Production

Set `ConnectionStrings__Default` and `Seed__Admin__Password` as environment variables (and `Seed__Admin__Email` if
you want a different one) and run `dotnet ef database update` (or `Database__MigrateOnStartup=true` for the first
start). Don't copy `appsettings.Local.json` to a server. The sign-in cookie is https-only outside Development.
Persist the ASP.NET Core Data Protection keys if you run in a container.

## Project layout

```
Program.cs                 Wiring: database, login cookie, pages
Auth/                      Custom login: PasswordHasher, AuthService, cookie checks, UserAccountService
Data/                      AppDbContext, entities, EF configurations, migrations, startup initializer
Components/Account/        Login and access-denied pages
Components/Layout/         Page layouts
Components/Pages/          Home page and error page
appsettings.Local.example.json   Copy it to appsettings.Local.json (git ignores that one) and put your passwords in it
database/                  The SQL files for MySQL Workbench (new schema, upgrades/, reset and rebuild) and the script that generates them
docs/database.md           Tables, relationships and the rules the database enforces
```

## How the login works

1. `/login` is a normal form. `AuthService` looks the user up by email, checks the password hash and the lockout,
   and the page writes the sign-in cookie.
2. Every page needs a signed-in user unless it has `@attribute [AllowAnonymous]`. Restrict a page to roles with
   `@attribute [Authorize(Roles = AppRoles.Admin)]`.
3. Wrong passwords are counted. After 5 in a row the account is locked for 15 minutes (`Auth` section in `appsettings.json`).
4. A signed-in user is re-checked against the database every few minutes, so deactivating an account (or changing
   its `security_stamp`) signs it out.
5. Two-factor sign-in is not built yet. `users.phone_number` is stored (as +639...) for when it is.
