using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Auth;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Data;

/// <summary>
/// Runs once at startup: optionally applies the migrations (Database:MigrateOnStartup, on in Development),
/// then makes sure there is an administrator to sign in with.
/// </summary>
public sealed class DatabaseInitializer(
    IDbContextFactory<AppDbContext> dbFactory,
    UserAccountService accounts,
    IConfiguration configuration,
    ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        try
        {
            if (configuration.GetValue<bool>("Database:MigrateOnStartup"))
            {
                await using var db = await dbFactory.CreateDbContextAsync(ct);
                logger.LogInformation("Applying database migrations...");
                await db.Database.MigrateAsync(ct);
            }

            await EnsureAdministratorAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Database setup failed. Check ConnectionStrings:Default (host, user and password), and that the tables " +
                "exist (run 'dotnet ef database update', or set Database:MigrateOnStartup to true). If tables were " +
                "dropped or changed by hand, rebuild the database: README.md, \"Start over\".");
            throw;
        }
    }

    private async Task EnsureAdministratorAsync(CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        if (await db.Users.AnyAsync(u => u.RoleId == AppRoles.AdminId, ct))
        {
            return;
        }

        var email = configuration["Seed:Admin:Email"];
        var password = configuration["Seed:Admin:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            logger.LogWarning(
                "There is no administrator account yet, so nobody can sign in. Put Seed:Admin:Password (a long " +
                "password) in appsettings.Local.json, or set it with 'dotnet user-secrets', and restart the app. " +
                "Its email is Seed:Admin:Email (now: '{Email}'). See README.md, \"First-time setup\".",
                email);
            return;
        }

        var result = await accounts.CreateAsync(new NewAccount(
            Role: AppRoles.Admin,
            Email: email,
            Password: password,
            FirstName: configuration["Seed:Admin:FirstName"] ?? "System",
            LastName: configuration["Seed:Admin:LastName"] ?? "Administrator",
            PhoneNumber: configuration["Seed:Admin:PhoneNumber"]), ct);

        if (result.Succeeded)
        {
            logger.LogInformation("Created the first administrator account for {Email}.", email.Trim().ToLowerInvariant());
        }
        else
        {
            logger.LogError("Could not create the first administrator account: {Errors}", string.Join(" ", result.Errors));
        }
    }
}
