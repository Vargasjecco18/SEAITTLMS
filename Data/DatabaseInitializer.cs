using Microsoft.EntityFrameworkCore;
using MySqlConnector;
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
    ILogger<DatabaseInitializer> logger,
    TimeProvider? clock = null)
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

            await AllowBigFilesAsync(ct);
            await EnsureAdministratorAsync(ct);
            await Services.TermClock.SyncAsync(dbFactory, clock ?? TimeProvider.System, ct);
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

    /// <summary>
    /// A file is saved in the database in one piece, so MySQL must accept a packet as big as the biggest file the settings allow (max_allowed_packet, 64 MB at first). The app asks the
    /// server for 1 GB while it runs (until MySQL is restarted); when the database user is not allowed to, a warning says how to put it in my.ini. Database:RaisePacketLimit turns this off.
    /// </summary>
    private async Task AllowBigFilesAsync(CancellationToken ct)
    {
        if (!configuration.GetValue("Database:RaisePacketLimit", true))
        {
            return;
        }

        const long wanted = 1024L * 1024 * 1024;
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var now = await db.Database.SqlQueryRaw<long>("SELECT CAST(@@global.max_allowed_packet AS SIGNED) AS `Value`").SingleAsync(ct);
            if (now >= wanted)
            {
                return;
            }

            await db.Database.ExecuteSqlRawAsync("SET GLOBAL max_allowed_packet = 1073741824", ct);
            MySqlConnection.ClearAllPools();   // the connections that are open keep the old size; the new ones get the new one
            logger.LogInformation("MySQL max_allowed_packet was {Old} MB; it is now 1024 MB, so files of the size the settings allow can be saved.", now / (1024 * 1024));
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "Could not raise max_allowed_packet ({Reason}). A file bigger than the packet MySQL accepts can't be saved. To allow big files, put  max_allowed_packet=1G  under [mysqld] in " +
                "the my.ini (or my.cnf) of MySQL and restart MySQL.", ex.GetBaseException().Message);
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
