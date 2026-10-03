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
            // One short message with the reason and the quickest fix. The full stack trace is only written at Debug level.
            logger.LogError(
                "Database setup failed: {Reason}\n" +
                "  This usually means one of these:\n" +
                "   - the host, user or password in ConnectionStrings:Default is wrong, or MySQL is not running;\n" +
                "   - tables were dropped or changed by hand, or an SQL script stopped halfway.\n" +
                "  If you have no data to keep, the quickest fix is to run  DROP DATABASE IF EXISTS seaittlms;  in\n" +
                "  MySQL Workbench and start the app again: it builds everything. More: README.md, \"Something broke? Start over\".",
                ex.GetBaseException().Message);
            logger.LogDebug(ex, "Database setup failed (details)");
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
