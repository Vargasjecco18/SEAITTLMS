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

Run these in the repository folder. Replace `YOUR_MYSQL_PASSWORD` and the admin password with your own.

```bash
# 1. Tell the app how to reach MySQL (stored outside the repo, never committed)
dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost;Port=3306;Database=seaittlms;User=root;Password=YOUR_MYSQL_PASSWORD;"

# 2. The password of the first administrator account
dotnet user-secrets set "Seed:Admin:Password" "choose-a-long-password"

# 3. Run. In Development the tables are created automatically on startup.
dotnet watch
```

Open http://localhost:5000 and sign in as **admin@legazpi.seaitt.edu.ph** with the password from step 2.

`dotnet user-secrets` works because the project has a `UserSecretsId`. If your MySQL server uses another host,
port or user, change the connection string in step 1.

### The first administrator (seed account)

The first time the app starts on a database that has no administrator, it creates one: **System Administrator**,
email `admin@legazpi.seaitt.edu.ph` (the `Seed:Admin:Email` setting in `appsettings.json`), with the password you set
in step 2. To use other details, set any of these before that first start:

```bash
dotnet user-secrets set "Seed:Admin:Email" "principal@legazpi.seaitt.edu.ph"
dotnet user-secrets set "Seed:Admin:FirstName" "Your"
dotnet user-secrets set "Seed:Admin:LastName" "Name"
dotnet user-secrets set "Seed:Admin:PhoneNumber" "09171234567"
```

The seed only runs when there is no administrator yet, so changing these settings later does not touch an account
that already exists. To change the email of an existing administrator, run this in MySQL Workbench (emails are
stored in lower case):

```sql
UPDATE seaittlms.users SET email = 'admin@legazpi.seaitt.edu.ph' WHERE email = 'admin@seaitt.edu.ph';
```

### Creating the tables

Pick one way. They all create the same tables.

1. **Automatically** (default in Development): just run `dotnet watch`, as in step 3 above.
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

The SQL files are generated from the migrations, don't edit them by hand. After a schema change, regenerate them with
`bash database/export-schema.sh` (on Windows, in Git Bash).

### Production

Set `ConnectionStrings__Default` and `Seed__Admin__Password` as environment variables (and `Seed__Admin__Email` if
you want a different one) and run `dotnet ef database update` (or `Database__MigrateOnStartup=true` for the first start). The sign-in cookie is
https-only outside Development. Persist the ASP.NET Core Data Protection keys if you run in a container.

## Project layout

```
Program.cs                 Wiring: database, login cookie, pages
Auth/                      Custom login: PasswordHasher, AuthService, cookie checks, UserAccountService
Data/                      AppDbContext, entities, EF configurations, migrations, startup initializer
Components/Account/        Login and access-denied pages
Components/Layout/         Page layouts
Components/Pages/          Home page and error page
database/                  seaittlms_schema.sql and upgrades/ (run them in MySQL Workbench) and the script that generates them
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
