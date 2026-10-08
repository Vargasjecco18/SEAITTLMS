using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>When messages can be sent: not from 7:00 PM to 7:00 AM, Philippine time. Reading is always possible.</summary>
public static class MessagingHours
{
    public const int ClosesAtHour = 19;
    public const int OpensAtHour = 7;

    /// <summary>The Philippines are 8 hours ahead of UTC all year round (no daylight saving time).</summary>
    private static readonly TimeSpan PhilippineOffset = TimeSpan.FromHours(8);

    public static bool IsClosed(DateTimeOffset now)
    {
        var hour = now.ToOffset(PhilippineOffset).Hour;
        return hour >= ClosesAtHour || hour < OpensAtHour;
    }

    public const string ClosedText = "Messages can't be sent from 7:00 PM to 7:00 AM (Philippine time). You can still read them.";
}

public sealed record Contact(int UserId, string Name, string Role, string? Detail);

public sealed record Conversation(Contact With, string LastBody, DateTime LastAtUtc, bool LastFromMe, int Unread);

public sealed record ChatMessage(int Id, bool Mine, string Body, DateTime SentAtUtc);

public sealed record SendResult(bool Succeeded, string? Error = null);

/// <summary>
/// One-to-one messages between people of the school. Who may write to whom depends on the role (a student writes to
/// the staff of the school, a teacher also to students, ...), and nobody can send a message at night.
/// </summary>
public sealed class MessageService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider clock, NotificationService notifications)
{
    public const int MaxLength = 2000;

    public bool IsClosed => MessagingHours.IsClosed(clock.GetUtcNow());

    /// <summary>The roles a person may write to.</summary>
    private static string[] AllowedRoles(string role) => role switch
    {
        AppRoles.Student => [AppRoles.Teacher, AppRoles.GuidanceCounselor, AppRoles.Registrar],
        AppRoles.Teacher => [AppRoles.Student, AppRoles.Teacher, AppRoles.GuidanceCounselor, AppRoles.Registrar, AppRoles.Admin, AppRoles.AcademicCoordinator],
        AppRoles.Registrar => [AppRoles.Student, AppRoles.Teacher, AppRoles.GuidanceCounselor, AppRoles.Admin, AppRoles.AcademicCoordinator],
        AppRoles.AcademicCoordinator => [AppRoles.Teacher, AppRoles.GuidanceCounselor, AppRoles.Registrar, AppRoles.Admin, AppRoles.AcademicCoordinator],
        _ => [AppRoles.Student, AppRoles.Teacher, AppRoles.GuidanceCounselor, AppRoles.Registrar, AppRoles.Admin, AppRoles.AcademicCoordinator],
    };

    /// <summary>The people this person may write to (active accounts only), by name, optionally filtered by a search text.</summary>
    public async Task<IReadOnlyList<Contact>> GetContactsAsync(int userId, string? search = null, int take = 40, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var me = await db.Users.AsNoTracking().Where(u => u.Id == userId && u.IsActive).Select(u => new { u.RoleId }).FirstOrDefaultAsync(ct);
        if (me is null)
        {
            return [];
        }

        var myRole = AppRoles.Ids.First(kv => kv.Value == me.RoleId).Key;
        var roleIds = AllowedRoles(myRole).Select(r => AppRoles.Ids[r]).ToList();

        var ids = await db.Users.AsNoTracking()
            .Where(u => u.IsActive && u.Id != userId && roleIds.Contains(u.RoleId))
            .Select(u => u.Id)
            .ToListAsync(ct);

        var people = await UserDirectory.LoadAsync(db, ids, ct);
        var tokens = (search ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return people.Values
            .Where(p => tokens.All(t => p.Name.Contains(t, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(p => p.Role == AppRoles.Student ? 1 : 0).ThenBy(p => p.Name)
            .Take(Math.Clamp(take, 1, 200))
            .Select(p => new Contact(p.UserId, p.Name, p.Role, AppRoles.DisplayName(p.Role)))
            .ToList();
    }

    public async Task<bool> CanMessageAsync(int fromUserId, int toUserId, CancellationToken ct = default)
    {
        if (fromUserId == toUserId)
        {
            return false;
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var users = await db.Users.AsNoTracking().Where(u => (u.Id == fromUserId || u.Id == toUserId) && u.IsActive).Select(u => new { u.Id, u.RoleId }).ToListAsync(ct);
        var from = users.FirstOrDefault(u => u.Id == fromUserId);
        var to = users.FirstOrDefault(u => u.Id == toUserId);
        if (from is null || to is null)
        {
            return false;
        }

        var fromRole = AppRoles.Ids.First(kv => kv.Value == from.RoleId).Key;
        return AllowedRoles(fromRole).Any(r => AppRoles.Ids[r] == to.RoleId);
    }

    /// <summary>Every conversation of the person, the newest first, with how many messages in it are unread.</summary>
    public async Task<IReadOnlyList<Conversation>> GetConversationsAsync(int userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var mine = await db.Messages.AsNoTracking()
            .Where(m => m.SenderUserId == userId || m.RecipientUserId == userId)
            .OrderByDescending(m => m.CreatedAtUtc).ThenByDescending(m => m.Id)
            .Select(m => new { m.SenderUserId, m.RecipientUserId, m.Body, m.CreatedAtUtc, m.ReadAtUtc })
            .Take(500)
            .ToListAsync(ct);

        var byPerson = mine.GroupBy(m => m.SenderUserId == userId ? m.RecipientUserId : m.SenderUserId).ToList();
        var people = await UserDirectory.LoadAsync(db, byPerson.Select(g => g.Key).ToList(), ct);

        return byPerson
            .Where(g => people.ContainsKey(g.Key))
            .Select(g =>
            {
                var last = g.First();
                var p = people[g.Key];
                return new Conversation(
                    new Contact(p.UserId, p.Name, p.Role, AppRoles.DisplayName(p.Role)),
                    last.Body, last.CreatedAtUtc, last.SenderUserId == userId,
                    g.Count(m => m.RecipientUserId == userId && m.ReadAtUtc == null));
            })
            .OrderByDescending(c => c.LastAtUtc)
            .ToList();
    }

    /// <summary>The latest messages between two people, oldest first. Opening them marks the ones sent to <paramref name="userId"/> as read.</summary>
    public async Task<IReadOnlyList<ChatMessage>> GetThreadAsync(int userId, int otherUserId, int take = 60, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var rows = await db.Messages.AsNoTracking()
            .Where(m => (m.SenderUserId == userId && m.RecipientUserId == otherUserId) || (m.SenderUserId == otherUserId && m.RecipientUserId == userId))
            .OrderByDescending(m => m.CreatedAtUtc).ThenByDescending(m => m.Id)
            .Take(Math.Clamp(take, 1, 200))
            .Select(m => new ChatMessage(m.Id, m.SenderUserId == userId, m.Body, m.CreatedAtUtc))
            .ToListAsync(ct);

        var now = clock.GetUtcNow().UtcDateTime;
        await db.Messages.Where(m => m.SenderUserId == otherUserId && m.RecipientUserId == userId && m.ReadAtUtc == null)
            .ExecuteUpdateAsync(set => set.SetProperty(m => m.ReadAtUtc, now), ct);

        rows.Reverse();
        return rows;
    }

    public async Task<int> UnreadCountAsync(int userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Messages.CountAsync(m => m.RecipientUserId == userId && m.ReadAtUtc == null, ct);
    }

    public async Task<SendResult> SendAsync(int fromUserId, int toUserId, string? body, CancellationToken ct = default)
    {
        if (IsClosed)
        {
            return new SendResult(false, MessagingHours.ClosedText);
        }

        var text = (body ?? "").Trim();
        if (text.Length == 0)
        {
            return new SendResult(false, "Write a message first.");
        }

        if (text.Length > MaxLength)
        {
            return new SendResult(false, $"A message can have at most {MaxLength:N0} characters.");
        }

        if (!await CanMessageAsync(fromUserId, toUserId, ct))
        {
            return new SendResult(false, "You can't send a message to this person.");
        }

        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            db.Messages.Add(new Message { SenderUserId = fromUserId, RecipientUserId = toUserId, Body = text });
            await db.SaveChangesAsync(ct);
        }

        // The recipient is told by the bell and by the number on the messages icon.
        string senderName;
        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            senderName = (await UserDirectory.LoadAsync(db, [fromUserId], ct)).GetValueOrDefault(fromUserId)?.Name ?? "Someone";
        }

        await notifications.AddAsync(toUserId, "message", $"New message from {senderName}", text.Length > 120 ? text[..119] + "…" : text, null, ct);
        return new SendResult(true);
    }
}
