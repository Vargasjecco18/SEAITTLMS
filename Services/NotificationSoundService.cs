using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>What the Settings of a person say about the sound of notifications: their choice, the file they added (if any) and whether the school has a sound.</summary>
public sealed record SoundInfo(string Choice, string? OwnName, string? SchoolName, long OwnVersion, long SchoolVersion)
{
    public bool HasOwn => OwnName is not null;
    public bool HasSchool => SchoolName is not null;
}

/// <summary>A sound file with its type, to be sent to the browser.</summary>
public sealed record SoundFile(string Name, string ContentType, byte[] Data);

/// <summary>
/// The sound of a new message or notification. Each person can choose one of the sounds the browser makes by itself (chime, bell, pop, knock, two tones), the sound of the school (the one
/// the administrator puts in System Settings) or a file they add themselves (mp3, ogg, wav or m4a, up to 2 MB). The files are in the database. The browser plays the sound (wwwroot/app.js).
/// </summary>
public sealed class NotificationSoundService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider clock, IServiceProvider services)
{
    /// <summary>The biggest sound file (a sound is a second or two long).</summary>
    public const int MaxBytes = 2 * 1024 * 1024;

    /// <summary>The choices a person can make. "school" and "custom" need a file; the others are made by the browser.</summary>
    public static readonly string[] Choices = ["tones", "chime", "bell", "pop", "knock", "school", "custom"];

    private static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        [".mp3"] = "audio/mpeg",
        [".ogg"] = "audio/ogg",
        [".oga"] = "audio/ogg",
        [".wav"] = "audio/wav",
        [".m4a"] = "audio/mp4",
        [".aac"] = "audio/aac",
    };

    public static string DisplayChoice(string choice) => choice switch
    {
        "chime" => "Chime",
        "bell" => "Bell",
        "pop" => "Pop",
        "knock" => "Double knock",
        "school" => "The sound of the school",
        "custom" => "My own file",
        _ => "Two short tones",
    };

    /// <summary>The audio type of a file name, or null when it is not a sound the browsers play.</summary>
    public static string? TypeOf(string fileName) => Types.GetValueOrDefault(Path.GetExtension(fileName));

    public async Task<SoundInfo> GetInfoAsync(int userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var choice = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.NotificationSoundChoice).FirstOrDefaultAsync(ct);
        var own = await db.NotificationSounds.AsNoTracking().Where(n => n.UserId == userId).Select(n => new { n.Name, n.UpdatedAtUtc }).FirstOrDefaultAsync(ct);
        var school = await db.NotificationSounds.AsNoTracking().Where(n => n.UserId == null).Select(n => new { n.Name, n.UpdatedAtUtc }).FirstOrDefaultAsync(ct);

        // A choice that needs a file that is not there any more falls back to the sound of the school, or the two tones.
        var resolved = string.IsNullOrEmpty(choice) ? "" : choice;
        if (resolved == "custom" && own is null || resolved == "school" && school is null)
        {
            resolved = "";
        }

        return new SoundInfo(resolved, own?.Name, school?.Name, own?.UpdatedAtUtc.Ticks ?? 0, school?.UpdatedAtUtc.Ticks ?? 0);
    }

    public async Task<ServiceResult> SetChoiceAsync(int userId, string? choice, CancellationToken ct = default)
    {
        var value = string.IsNullOrWhiteSpace(choice) || choice == "default" ? null : choice.Trim().ToLowerInvariant();
        if (value is not null && !Choices.Contains(value))
        {
            return ServiceResult.Fail("That sound is not one of the choices.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (value == "custom" && !await db.NotificationSounds.AnyAsync(n => n.UserId == userId, ct))
        {
            return ServiceResult.Fail("Add your own sound file first.");
        }

        if (value == "school" && !await db.NotificationSounds.AnyAsync(n => n.UserId == null, ct))
        {
            return ServiceResult.Fail("The school has no sound.");
        }

        var changed = await db.Users.Where(u => u.Id == userId && u.IsActive).ExecuteUpdateAsync(s => s.SetProperty(u => u.NotificationSoundChoice, value), ct);
        return changed == 0 ? ServiceResult.Fail("Your account was not found.") : ServiceResult.Ok("Saved.");
    }

    /// <summary>Saves the file a person added (it replaces the one they had) and chooses it.</summary>
    public async Task<ServiceResult> SaveOwnAsync(int userId, string fileName, byte[] data, CancellationToken ct = default)
    {
        var problem = Check(fileName, data);
        if (problem is not null)
        {
            return ServiceResult.Fail(problem);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await db.Users.AnyAsync(u => u.Id == userId && u.IsActive, ct))
        {
            return ServiceResult.Fail("Your account was not found.");
        }

        await Upsert(db, userId, fileName, data, ct);
        await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(s => s.SetProperty(u => u.NotificationSoundChoice, "custom"), ct);
        return ServiceResult.Ok("Your sound was saved and it is the one you hear now.");
    }

    public async Task<ServiceResult> RemoveOwnAsync(int userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.NotificationSounds.Where(n => n.UserId == userId).ExecuteDeleteAsync(ct);
        await db.Users.Where(u => u.Id == userId && u.NotificationSoundChoice == "custom").ExecuteUpdateAsync(s => s.SetProperty(u => u.NotificationSoundChoice, (string?)null), ct);
        return ServiceResult.Ok("Your sound was removed.");
    }

    /// <summary>The sound of the school (System Settings). Only an administrator may change it.</summary>
    public async Task<ServiceResult> SaveSchoolAsync(int adminUserId, string fileName, byte[] data, CancellationToken ct = default)
    {
        var problem = Check(fileName, data);
        if (problem is not null)
        {
            return ServiceResult.Fail(problem);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await db.Users.AnyAsync(u => u.Id == adminUserId && u.IsActive && u.RoleId == AppRoles.AdminId, ct))
        {
            return ServiceResult.Fail("Only an administrator can change the sound of the school.");
        }

        await Upsert(db, null, fileName, data, ct);
        await WriteLogAsync(adminUserId, $"The sound of the school is now {fileName}.", ct);
        return ServiceResult.Ok("The sound of the school was saved. People who did not choose a sound of their own hear it.");
    }

    public async Task<ServiceResult> RemoveSchoolAsync(int adminUserId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await db.Users.AnyAsync(u => u.Id == adminUserId && u.IsActive && u.RoleId == AppRoles.AdminId, ct))
        {
            return ServiceResult.Fail("Only an administrator can change the sound of the school.");
        }

        await db.NotificationSounds.Where(n => n.UserId == null).ExecuteDeleteAsync(ct);
        await db.Users.Where(u => u.NotificationSoundChoice == "school").ExecuteUpdateAsync(s => s.SetProperty(u => u.NotificationSoundChoice, (string?)null), ct);
        await WriteLogAsync(adminUserId, "The sound of the school was removed.", ct);
        return ServiceResult.Ok("The sound of the school was removed.");
    }

    /// <summary>The file of a person (<paramref name="userId"/>) or of the school (null), or null when there is none.</summary>
    public async Task<SoundFile?> GetFileAsync(int? userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.NotificationSounds.AsNoTracking().Where(n => n.UserId == userId).Select(n => new SoundFile(n.Name, n.ContentType, n.Data)).FirstOrDefaultAsync(ct);
    }

    private static string? Check(string fileName, byte[] data)
    {
        if (TypeOf(fileName) is null)
        {
            return "Choose a sound file: .mp3, .ogg, .wav, .m4a or .aac.";
        }

        if (data.Length == 0)
        {
            return "The file is empty.";
        }

        if (data.Length > MaxBytes)
        {
            return $"The sound is bigger than {MaxBytes / (1024 * 1024)} MB. A sound of a second or two is enough.";
        }

        return null;
    }

    private async Task Upsert(AppDbContext db, int? userId, string fileName, byte[] data, CancellationToken ct)
    {
        var name = Path.GetFileName(fileName);
        if (name.Length > 255)
        {
            name = name[^255..];
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var existing = await db.NotificationSounds.Where(n => n.UserId == userId).ToListAsync(ct);
        var row = existing.FirstOrDefault();
        if (row is null)
        {
            row = new NotificationSound { UserId = userId };
            db.NotificationSounds.Add(row);
        }

        row.Name = name;
        row.ContentType = TypeOf(fileName)!;
        row.SizeBytes = data.Length;
        row.Data = data;
        row.UpdatedAtUtc = now;
        db.NotificationSounds.RemoveRange(existing.Skip(1));
        await db.SaveChangesAsync(ct);
    }

    private async Task WriteLogAsync(int adminUserId, string details, CancellationToken ct)
    {
        var log = services.GetService<SecurityLogService>();
        if (log is not null)
        {
            await log.AddAsync(SecurityEvents.SettingsChanged, adminUserId, details: details, ct: ct);
        }
    }
}
