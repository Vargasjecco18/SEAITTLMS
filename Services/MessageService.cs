using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

public sealed record Contact(int UserId, string Name, string Role, string? Detail);

public sealed record Conversation(Contact With, string LastBody, DateTime LastAtUtc, bool LastFromMe, int Unread);

public sealed record ChatMessage(int Id, bool Mine, string Body, DateTime SentAtUtc, IReadOnlyList<AttachmentRow>? Files = null);

public sealed record SendResult(bool Succeeded, string? Error = null, int MessageId = 0);

/// <summary>
/// One-to-one messages between people of the school. Who may write to whom depends on the role (a student writes to
/// the staff of the school, a teacher also to students, ...). Messages can be sent at any hour.
/// </summary>
public sealed class MessageService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider clock, NotificationService notifications, LiveUpdates? live = null, AttachmentService? attachments = null)
{
    public const int MaxLength = 2000;

    /// <summary>The roles a person may write to.</summary>
    private static string[] AllowedRoles(string role) => role switch
    {
        AppRoles.Student => [AppRoles.Teacher, AppRoles.GuidanceCounselor, AppRoles.Registrar],
        AppRoles.Teacher => [AppRoles.Student, AppRoles.Teacher, AppRoles.GuidanceCounselor, AppRoles.Registrar, AppRoles.Admin, AppRoles.AcademicCoordinator, AppRoles.AcademicProvider],
        AppRoles.Registrar => [AppRoles.Student, AppRoles.Teacher, AppRoles.GuidanceCounselor, AppRoles.Admin, AppRoles.AcademicCoordinator, AppRoles.AcademicProvider],
        AppRoles.AcademicCoordinator => [AppRoles.Teacher, AppRoles.GuidanceCounselor, AppRoles.Registrar, AppRoles.Admin, AppRoles.AcademicCoordinator, AppRoles.AcademicProvider],
        AppRoles.AcademicProvider => [AppRoles.Teacher, AppRoles.GuidanceCounselor, AppRoles.Registrar, AppRoles.Admin, AppRoles.AcademicCoordinator, AppRoles.AcademicProvider],
        _ => [AppRoles.Student, AppRoles.Teacher, AppRoles.GuidanceCounselor, AppRoles.Registrar, AppRoles.Admin, AppRoles.AcademicCoordinator, AppRoles.AcademicProvider],
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

        // The people of the roles this person may write to, and their friends (a student can write to a friend who is a student).
        var friendIds = await db.Friendships.AsNoTracking()
            .Where(f => f.Status == FriendshipStates.Accepted && (f.RequesterUserId == userId || f.AddresseeUserId == userId))
            .Select(f => f.RequesterUserId == userId ? f.AddresseeUserId : f.RequesterUserId)
            .ToListAsync(ct);
        var ids = await db.Users.AsNoTracking()
            .Where(u => u.IsActive && u.Id != userId && (roleIds.Contains(u.RoleId) || friendIds.Contains(u.Id)))
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

    /// <summary>The person to write to as a contact, when the first person may write to them. Null otherwise.</summary>
    public async Task<Contact?> GetContactAsync(int userId, int otherUserId, CancellationToken ct = default)
    {
        if (!await CanMessageAsync(userId, otherUserId, ct))
        {
            return null;
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var people = await UserDirectory.LoadAsync(db, [otherUserId], ct);
        return people.TryGetValue(otherUserId, out var p) ? new Contact(p.UserId, p.Name, p.Role, AppRoles.DisplayName(p.Role)) : null;
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
        if (AllowedRoles(fromRole).Any(r => AppRoles.Ids[r] == to.RoleId))
        {
            return true;
        }

        // Friends can write to each other (a student to another student).
        return await FriendService.AreFriendsAsync(db, fromUserId, toUserId, ct);
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
                    string.IsNullOrEmpty(last.Body) ? "Sent a file" : last.Body, last.CreatedAtUtc, last.SenderUserId == userId,
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
            .Select(m => new ChatMessage(m.Id, m.SenderUserId == userId, m.Body, m.CreatedAtUtc, null))
            .ToListAsync(ct);

        // The files that were sent with the messages.
        if (attachments is not null && rows.Count > 0)
        {
            var files = await attachments.ListForAsync(AttachmentOwnerKind.Message, rows.Select(r => r.Id).ToList(), ct);
            rows = rows.Select(r => files.TryGetValue(r.Id, out var list) ? r with { Files = list } : r).ToList();
        }

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

    public async Task<SendResult> SendAsync(int fromUserId, int toUserId, string? body, IReadOnlyList<NewAttachment>? files = null, CancellationToken ct = default)
    {
        var text = (body ?? "").Trim();
        var hasFiles = files is { Count: > 0 } && attachments is not null;
        if (text.Length == 0 && !hasFiles)
        {
            return new SendResult(false, "Write a message or choose a file first.");
        }

        if (hasFiles && attachments!.Check(files!) is { Count: > 0 } fileProblems)
        {
            return new SendResult(false, string.Join(" ", fileProblems));
        }

        if (text.Length > MaxLength)
        {
            return new SendResult(false, $"A message can have at most {MaxLength:N0} characters.");
        }

        if (!await CanMessageAsync(fromUserId, toUserId, ct))
        {
            return new SendResult(false, "You can't send a message to this person.");
        }

        int messageId;
        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            var message = new Message { SenderUserId = fromUserId, RecipientUserId = toUserId, Body = text };
            db.Messages.Add(message);
            await db.SaveChangesAsync(ct);
            messageId = message.Id;
        }

        if (hasFiles)
        {
            var saved = await attachments!.AddAsync(fromUserId, new AttachmentOwner(AttachmentOwnerKind.Message, messageId), files!, ct);
            if (!saved.Succeeded)
            {
                await using var db = await dbFactory.CreateDbContextAsync(ct);
                await db.Messages.Where(m => m.Id == messageId).ExecuteDeleteAsync(ct);
                return new SendResult(false, string.Join(" ", saved.Errors));
            }
        }

        // The recipient sees it on the messages icon (the number on it): a message is not a notification, so the bell does not ring for it.
        live?.Publish([toUserId]);
        return new SendResult(true, null, messageId);
    }
}

/// <summary>
/// Lets a page open the messages box of the header on a conversation ("Message" on the Users page). It lives as long as the open browser
/// tab (one per circuit), like the header and the page, so they can talk through it.
/// </summary>
public sealed class MessageLauncher
{
    public event Action<int>? Requested;

    public void Open(int userId) => Requested?.Invoke(userId);
}
