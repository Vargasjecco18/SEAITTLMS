using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>
/// The pictures people upload in the settings menu. The app keeps (in memory) which people have a picture and its version,
/// so an avatar can be drawn without asking the database; the pictures themselves are read from the database when the browser
/// asks for /photos/{id}. It lives as long as the app, and every change goes through it, so what it knows is right.
/// </summary>
public sealed class ProfilePhotoService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider clock)
{
    /// <summary>The biggest picture that is kept. The browser makes it much smaller (256 pixels), this only guards the database.</summary>
    public const int MaxBytes = 1_000_000;

    /// <summary>The biggest file the person may choose (the browser shrinks it before it is sent).</summary>
    public const long MaxChosenBytes = 12_000_000;

    private readonly ConcurrentDictionary<int, long> versions = new();

    /// <summary>Reads who has a picture. Called once when the app starts; calling it again is harmless.</summary>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await db.ProfilePhotos.AsNoTracking().Select(p => new { p.UserId, p.UpdatedAtUtc }).ToListAsync(ct);

        versions.Clear();
        foreach (var row in rows)
        {
            versions[row.UserId] = row.UpdatedAtUtc.Ticks;
        }
    }

    /// <summary>The version of the person's picture (it goes in the address so a new picture is fetched), or null when they have none.</summary>
    public long? VersionOf(int userId) => versions.TryGetValue(userId, out var v) ? v : null;

    /// <summary>The address of the person's picture, or null when they have none.</summary>
    public string? UrlOf(int userId) => VersionOf(userId) is { } v ? $"/photos/{userId}?v={v}" : null;

    public async Task<(string ContentType, byte[] Data)?> GetAsync(int userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var photo = await db.ProfilePhotos.AsNoTracking().Where(p => p.UserId == userId).Select(p => new { p.ContentType, p.Data }).FirstOrDefaultAsync(ct);
        return photo is null ? null : (photo.ContentType, photo.Data);
    }

    /// <summary>Which kind of picture these bytes are (by what the file starts with, not by what it says it is), or null for anything else.</summary>
    public static string? Sniff(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47 && data[4] == 0x0D && data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A)
        {
            return "image/png";
        }

        if (data.Length >= 6 && data[0] == 'G' && data[1] == 'I' && data[2] == 'F' && data[3] == '8' && (data[4] == '7' || data[4] == '9') && data[5] == 'a')
        {
            return "image/gif";
        }

        if (data.Length >= 12 && data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F' && data[8] == 'W' && data[9] == 'E' && data[10] == 'B' && data[11] == 'P')
        {
            return "image/webp";
        }

        return null;
    }

    public async Task<ServiceResult> SetAsync(int userId, byte[] data, CancellationToken ct = default)
    {
        if (data.Length == 0)
        {
            return ServiceResult.Fail("Choose a picture.");
        }

        if (data.Length > MaxBytes)
        {
            return ServiceResult.Fail("That picture is too big. Choose a smaller one.");
        }

        var contentType = Sniff(data);
        if (contentType is null)
        {
            return ServiceResult.Fail("Choose a picture: a JPG, PNG, GIF or WebP file.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await db.Users.AnyAsync(u => u.Id == userId && u.IsActive, ct))
        {
            return ServiceResult.Fail("This account no longer exists.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var existing = await db.ProfilePhotos.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (existing is null)
        {
            db.ProfilePhotos.Add(new ProfilePhoto { UserId = userId, ContentType = contentType, Data = data, UpdatedAtUtc = now });
        }
        else
        {
            existing.ContentType = contentType;
            existing.Data = data;
            existing.UpdatedAtUtc = now;
        }

        await db.SaveChangesAsync(ct);
        versions[userId] = now.Ticks;
        return ServiceResult.Ok("Your picture was changed.");
    }

    public async Task<ServiceResult> RemoveAsync(int userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.ProfilePhotos.Where(p => p.UserId == userId).ExecuteDeleteAsync(ct);
        versions.TryRemove(userId, out _);
        return ServiceResult.Ok("Your picture was removed.");
    }
}
