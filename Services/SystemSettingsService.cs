using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SEAITT_LMS.Auth;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>Everything the administrator can change in System Settings, with the value each setting has until it is changed.</summary>
public sealed record SystemSettingsValues
{
    // ---- the school (shown on the sign-in page, the home page, the top of every page and the help page)
    public string SchoolName { get; init; } = "South East-Asia Institute of Trade and Technology";
    public string SchoolShortName { get; init; } = "SEAITT Legazpi";
    public string Tagline { get; init; } = "Senior High School";
    public string Address { get; init; } = "";
    public string ContactEmail { get; init; } = "";
    public string ContactPhone { get; init; } = "";

    // ---- signing in
    public int MaxFailedAttempts { get; init; } = 5;
    public int LockoutMinutes { get; init; } = 5;

    // ---- maintenance: while it is on only an administrator can use the system
    public bool MaintenanceOn { get; init; }
    public string MaintenanceMessage { get; init; } = "The system is being maintained. Please try again later.";

    // ---- messages
    /// <summary>When on, messages can't be sent between <see cref="MessagesCloseHour"/> and <see cref="MessagesOpenHour"/> (Philippine time).</summary>
    public bool MessagesHaveClosingHours { get; init; } = true;
    public int MessagesCloseHour { get; init; } = MessagingHours.ClosesAtHour;
    public int MessagesOpenHour { get; init; } = MessagingHours.OpensAtHour;

    // ---- backups and logs
    public bool AutoBackupOn { get; init; }
    public int AutoBackupHour { get; init; } = 2;
    public int BackupsToKeep { get; init; } = 7;
    public int LogKeepDays { get; init; } = 180;

    // ---- classes: how many students one section can have (the registrar sees "32 of 40" and can't enroll more)
    public int SectionCapacity { get; init; } = 40;

    // ---- files: the biggest file a person can upload, and the size of the locker of each person
    public int MaxFileMegabytes { get; init; } = 200;
    public int LockerMegabytes { get; init; } = 500;

    public string SchoolTitle => string.IsNullOrWhiteSpace(SchoolShortName) ? "LMS" : $"{SchoolShortName} LMS";
}

/// <summary>
/// The settings the administrator keeps in the system_settings table. They are read once when the app starts and kept in memory (so a page
/// that needs the school's name does not ask the database), and every change goes through <see cref="SaveAsync"/>, which writes the table,
/// the security log and the copy in memory. It lives as long as the app. Settings that were never changed use the value in the record above;
/// the sign-in limits start from appsettings.json (the "Auth" section) when nothing was changed.
/// </summary>
public sealed class SystemSettingsService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider clock, IOptions<AuthOptions> authOptions, IServiceProvider services)
{
    private SystemSettingsValues current = new();
    private volatile bool restoring;

    /// <summary>The settings now. A snapshot: it never changes under the reader, a save puts a new one in its place.</summary>
    public SystemSettingsValues Current => current;

    /// <summary>True while a backup is being restored: for that time the system is closed like in maintenance.</summary>
    public bool IsRestoring => restoring;

    /// <summary>True when only administrators may use the system (maintenance is on, or a backup is being restored).</summary>
    public bool IsClosed => restoring || current.MaintenanceOn;

    public string ClosedMessage => restoring
        ? "The system is being restored from a backup. Please try again in a few minutes."
        : current.MaintenanceMessage;

    /// <summary>
    /// The biggest packet the database accepts (MySQL max_allowed_packet), in bytes: a file is saved in one piece, so a file bigger than this can't be kept. Zero when the database does not say.
    /// The app asks for a big one when it starts (see DatabaseInitializer); this tells what the server really has.
    /// </summary>
    public async Task<long> GetDatabasePacketBytesAsync(CancellationToken ct = default)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            return await db.Database.SqlQueryRaw<long>("SELECT CAST(@@max_allowed_packet AS SIGNED) AS `Value`").SingleAsync(ct);
        }
        catch (Exception)
        {
            return 0;
        }
    }

    public void BeginRestore() => restoring = true;

    public void EndRestore() => restoring = false;

    public event Action? Changed;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await db.SystemSettings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.Value, ct);
        Apply(Parse(rows));
    }

    /// <summary>Checks the values, writes the ones that changed, and tells the security log. Only an administrator may do it.</summary>
    public Task<ServiceResult> SaveAsync(int actingUserId, SystemSettingsValues values, CancellationToken ct = default) =>
        SaveAsync(actingUserId, values, [AppRoles.AdminId], "Only an administrator can change the system settings.", ct);

    /// <summary>The number of students a section can have. It is a setting of the academic part of the school, so the academic coordinator changes it (the administrator can too).</summary>
    public Task<ServiceResult> SaveSectionCapacityAsync(int actingUserId, int capacity, CancellationToken ct = default) =>
        SaveAsync(actingUserId, current with { SectionCapacity = capacity }, [AppRoles.AcademicCoordinatorId, AppRoles.AdminId],
            "Only the academic coordinator can change how many students a section can have.", ct);

    private async Task<ServiceResult> SaveAsync(int actingUserId, SystemSettingsValues values, int[] allowedRoleIds, string notAllowed, CancellationToken ct)
    {
        var errors = Validate(values);
        if (errors.Count > 0)
        {
            return ServiceResult.Fail(errors);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await db.Users.AsNoTracking().AnyAsync(u => u.Id == actingUserId && u.IsActive && allowedRoleIds.Contains(u.RoleId), ct))
        {
            return ServiceResult.Fail(notAllowed);
        }

        var before = ToRows(current);
        var after = ToRows(Clean(values));
        var changed = after.Where(kv => !before.TryGetValue(kv.Key, out var old) || old != kv.Value).Select(kv => kv.Key).ToList();
        if (changed.Count == 0)
        {
            return ServiceResult.Ok("Nothing changed.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var existing = await db.SystemSettings.ToDictionaryAsync(s => s.Key, ct);
        foreach (var key in changed)
        {
            if (existing.TryGetValue(key, out var row))
            {
                row.Value = after[key];
                row.UpdatedAtUtc = now;
                row.UpdatedByUserId = actingUserId;
            }
            else
            {
                db.SystemSettings.Add(new SystemSetting { Key = key, Value = after[key], UpdatedAtUtc = now, UpdatedByUserId = actingUserId });
            }
        }

        await db.SaveChangesAsync(ct);
        Apply(Clean(values));

        // The log is looked up late (it needs this service for its own limits), so the two do not need each other to be made.
        var log = services.GetService<SecurityLogService>();
        if (log is not null)
        {
            await log.AddAsync(SecurityEvents.SettingsChanged, actingUserId, details: "Changed: " + string.Join(", ", changed.Select(Label)), ct: ct);
        }

        return ServiceResult.Ok("The settings were saved.");
    }

    /// <summary>The problems with a set of settings (an empty list when they are fine).</summary>
    public static List<string> Validate(SystemSettingsValues v)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(v.SchoolName) || v.SchoolName.Trim().Length > 150)
        {
            errors.Add("Enter the name of the school (up to 150 characters).");
        }

        if (string.IsNullOrWhiteSpace(v.SchoolShortName) || v.SchoolShortName.Trim().Length > 20)
        {
            errors.Add("Enter the short name of the school, like SEAITT Legazpi (up to 20 characters).");
        }

        if ((v.Tagline ?? "").Trim().Length > 80)
        {
            errors.Add("The line under the name can have at most 80 characters.");
        }

        if ((v.Address ?? "").Trim().Length > 250)
        {
            errors.Add("The address can have at most 250 characters.");
        }

        if (!string.IsNullOrWhiteSpace(v.ContactEmail) && EmailAddresses.Normalize(v.ContactEmail) is null)
        {
            errors.Add("Enter a valid contact email, or leave it empty.");
        }

        if ((v.ContactPhone ?? "").Trim().Length > 40)
        {
            errors.Add("The contact number can have at most 40 characters.");
        }

        if (v.MaxFailedAttempts is < 3 or > 20)
        {
            errors.Add("Wrong passwords before an account is locked: choose a number from 3 to 20.");
        }

        if (v.LockoutMinutes is < 1 or > 1440)
        {
            errors.Add("An account is locked for 1 to 1440 minutes.");
        }

        if (string.IsNullOrWhiteSpace(v.MaintenanceMessage) || v.MaintenanceMessage.Trim().Length > 300)
        {
            errors.Add("Write the maintenance message (up to 300 characters).");
        }

        if (v.MessagesCloseHour is < 0 or > 23 || v.MessagesOpenHour is < 0 or > 23)
        {
            errors.Add("The hours for the messages are from 0 to 23.");
        }

        if (v.MessagesHaveClosingHours && v.MessagesCloseHour == v.MessagesOpenHour)
        {
            errors.Add("Messages must open at a different hour than they close.");
        }

        if (v.AutoBackupHour is < 0 or > 23)
        {
            errors.Add("The hour of the automatic backup is from 0 to 23.");
        }

        if (v.BackupsToKeep is < 1 or > 60)
        {
            errors.Add("Keep from 1 to 60 automatic backups.");
        }

        if (v.LogKeepDays is < 30 or > 3650)
        {
            errors.Add("Keep the security log for 30 to 3650 days.");
        }

        if (v.SectionCapacity is < 1 or > 100)
        {
            errors.Add("A section can have from 1 to 100 students.");
        }

        if (v.MaxFileMegabytes is < 1 or > 1000)
        {
            errors.Add("The biggest file a person can upload is from 1 to 1000 MB.");
        }

        if (v.LockerMegabytes is < 10 or > 50000)
        {
            errors.Add("The locker of a person can hold from 10 to 50000 MB.");
        }

        if (v.LockerMegabytes < v.MaxFileMegabytes)
        {
            errors.Add("The locker can't be smaller than one file.");
        }

        return errors;
    }

    // ---------------------------------------------------------------- storage

    private static string Label(string key) => key switch
    {
        "school.name" => "school name",
        "school.short_name" => "short name",
        "school.tagline" => "line under the name",
        "school.address" => "address",
        "school.contact_email" => "contact email",
        "school.contact_phone" => "contact number",
        "signin.max_failed_attempts" => "wrong passwords before a lock",
        "signin.lockout_minutes" => "minutes locked",
        "maintenance.on" => "maintenance mode",
        "maintenance.message" => "maintenance message",
        "messages.closing_hours" => "closing hours of messages",
        "messages.close_hour" => "hour messages close",
        "messages.open_hour" => "hour messages open",
        "backup.auto_on" => "automatic backup",
        "backup.auto_hour" => "hour of the automatic backup",
        "backup.keep" => "automatic backups kept",
        "logs.keep_days" => "days the log is kept",
        "classes.section_capacity" => "students in a section",
        "files.max_mb" => "biggest file",
        "files.locker_mb" => "size of a locker",
        _ => key,
    };

    private static SystemSettingsValues Clean(SystemSettingsValues v) => v with
    {
        SchoolName = v.SchoolName.Trim(),
        SchoolShortName = v.SchoolShortName.Trim(),
        Tagline = (v.Tagline ?? "").Trim(),
        Address = (v.Address ?? "").Trim(),
        ContactEmail = string.IsNullOrWhiteSpace(v.ContactEmail) ? "" : EmailAddresses.Normalize(v.ContactEmail) ?? "",
        ContactPhone = (v.ContactPhone ?? "").Trim(),
        MaintenanceMessage = v.MaintenanceMessage.Trim(),
    };

    private static Dictionary<string, string> ToRows(SystemSettingsValues v)
    {
        string B(bool b) => b ? "1" : "0";
        string N(int n) => n.ToString(CultureInfo.InvariantCulture);
        return new Dictionary<string, string>
        {
            ["school.name"] = v.SchoolName,
            ["school.short_name"] = v.SchoolShortName,
            ["school.tagline"] = v.Tagline,
            ["school.address"] = v.Address,
            ["school.contact_email"] = v.ContactEmail,
            ["school.contact_phone"] = v.ContactPhone,
            ["signin.max_failed_attempts"] = N(v.MaxFailedAttempts),
            ["signin.lockout_minutes"] = N(v.LockoutMinutes),
            ["maintenance.on"] = B(v.MaintenanceOn),
            ["maintenance.message"] = v.MaintenanceMessage,
            ["messages.closing_hours"] = B(v.MessagesHaveClosingHours),
            ["messages.close_hour"] = N(v.MessagesCloseHour),
            ["messages.open_hour"] = N(v.MessagesOpenHour),
            ["backup.auto_on"] = B(v.AutoBackupOn),
            ["backup.auto_hour"] = N(v.AutoBackupHour),
            ["backup.keep"] = N(v.BackupsToKeep),
            ["logs.keep_days"] = N(v.LogKeepDays),
            ["classes.section_capacity"] = N(v.SectionCapacity),
            ["files.max_mb"] = N(v.MaxFileMegabytes),
            ["files.locker_mb"] = N(v.LockerMegabytes),
        };
    }

    // What was stored, on top of the defaults (a value that can't be read is ignored, the default stays).
    private SystemSettingsValues Parse(IReadOnlyDictionary<string, string> rows)
    {
        var auth = authOptions.Value;
        var v = new SystemSettingsValues { MaxFailedAttempts = auth.MaxFailedAttempts, LockoutMinutes = auth.LockoutMinutes };

        string Text(string key, string fallback) => rows.TryGetValue(key, out var x) ? x : fallback;
        bool Flag(string key, bool fallback) => rows.TryGetValue(key, out var x) ? x == "1" : fallback;
        int Number(string key, int fallback) => rows.TryGetValue(key, out var x) && int.TryParse(x, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : fallback;

        return v with
        {
            SchoolName = Text("school.name", v.SchoolName),
            SchoolShortName = Text("school.short_name", v.SchoolShortName),
            Tagline = Text("school.tagline", v.Tagline),
            Address = Text("school.address", v.Address),
            ContactEmail = Text("school.contact_email", v.ContactEmail),
            ContactPhone = Text("school.contact_phone", v.ContactPhone),
            MaxFailedAttempts = Number("signin.max_failed_attempts", v.MaxFailedAttempts),
            LockoutMinutes = Number("signin.lockout_minutes", v.LockoutMinutes),
            MaintenanceOn = Flag("maintenance.on", v.MaintenanceOn),
            MaintenanceMessage = Text("maintenance.message", v.MaintenanceMessage),
            MessagesHaveClosingHours = Flag("messages.closing_hours", v.MessagesHaveClosingHours),
            MessagesCloseHour = Number("messages.close_hour", v.MessagesCloseHour),
            MessagesOpenHour = Number("messages.open_hour", v.MessagesOpenHour),
            AutoBackupOn = Flag("backup.auto_on", v.AutoBackupOn),
            AutoBackupHour = Number("backup.auto_hour", v.AutoBackupHour),
            BackupsToKeep = Number("backup.keep", v.BackupsToKeep),
            LogKeepDays = Number("logs.keep_days", v.LogKeepDays),
            SectionCapacity = Number("classes.section_capacity", v.SectionCapacity),
            MaxFileMegabytes = Number("files.max_mb", v.MaxFileMegabytes),
            LockerMegabytes = Number("files.locker_mb", v.LockerMegabytes),
        };
    }

    private void Apply(SystemSettingsValues values)
    {
        current = values;
        MessagingHours.Configure(values.MessagesHaveClosingHours, values.MessagesCloseHour, values.MessagesOpenHour);
        Changed?.Invoke();
    }
}
