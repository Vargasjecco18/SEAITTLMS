using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

public sealed record NotificationItem(int Id, string Kind, string Title, string? Body, string? Link, bool IsRead, DateTime CreatedAtUtc);

public sealed record NotificationList(IReadOnlyList<NotificationItem> Items, int Unread);

/// <summary>
/// The bell in the header. Other services call <see cref="AddAsync"/> when something happens that a person should know about
/// (a module was posted, an activity was graded, a message came in, a student was referred).
/// </summary>
public sealed class NotificationService(IDbContextFactory<AppDbContext> dbFactory, LiveUpdates? live = null, EmailQueue? email = null)
{
    public async Task AddAsync(int userId, string kind, string title, string? body = null, string? link = null, CancellationToken ct = default) =>
        await AddForManyAsync([userId], kind, title, body, link, ct);

    public async Task AddForManyAsync(IEnumerable<int> userIds, string kind, string title, string? body = null, string? link = null, CancellationToken ct = default)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        foreach (var id in ids)
        {
            db.Notifications.Add(new Notification
            {
                UserId = id,
                Kind = Limit(kind, 30),
                Title = Limit(title, 200),
                Body = body is null ? null : Limit(body, 500),
                Link = link is null ? null : Limit(link, 300),
            });
        }

        await db.SaveChangesAsync(ct);

        // The people who have a page open see it now.
        live?.Publish(ids);

        // The people who added an email address in Settings get it there too (it only waits in the queue: the mail server is never waited for).
        if (email is { Enabled: true })
        {
            var targets = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id) && u.IsActive && u.NotifyEmail != null).Select(u => u.NotifyEmail!).ToListAsync(ct);
            foreach (var address in targets.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                email.TryEnqueue(email.ForNotification(address, title, body, link));
            }
        }
    }

    // ======================================================================= the email address of a person

    /// <summary>Whether the system can send email at all (the "Email" section of appsettings is filled in).</summary>
    public bool EmailIsSetUp => email?.Enabled ?? false;

    public async Task<string?> GetNotifyEmailAsync(int userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.NotifyEmail).FirstOrDefaultAsync(ct);
    }

    /// <summary>The person adds the email address (a Gmail, for example) where their notifications are also sent, changes it, or takes it off (empty).</summary>
    public async Task<ServiceResult> SetNotifyEmailAsync(int userId, string? address, CancellationToken ct = default)
    {
        string? normalized = null;
        if (!string.IsNullOrWhiteSpace(address))
        {
            normalized = Auth.EmailAddresses.Normalize(address);
            if (normalized is null)
            {
                return ServiceResult.Fail("Write a real email address, like name@gmail.com.");
            }
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var changed = await db.Users.Where(u => u.Id == userId && u.IsActive).ExecuteUpdateAsync(s => s.SetProperty(u => u.NotifyEmail, normalized), ct);
        if (changed == 0)
        {
            return ServiceResult.Fail("Your account was not found.");
        }

        return ServiceResult.Ok(normalized is null ? "The email address was taken off. Notifications stay in the system only." : $"Saved. Your notifications are also sent to {normalized}.");
    }

    /// <summary>Sends one email to the address the person added, so they can see that it arrives.</summary>
    public async Task<ServiceResult> SendTestEmailAsync(int userId, CancellationToken ct = default)
    {
        if (email is not { Enabled: true })
        {
            return ServiceResult.Fail("The school has not set up the sending of email yet, so nothing can be sent now. Your address is kept: the emails start when the administrator sets it up.");
        }

        var address = await GetNotifyEmailAsync(userId, ct);
        if (address is null)
        {
            return ServiceResult.Fail("Save an email address first.");
        }

        email.TryEnqueue(email.ForNotification(address, "Test message from SEAITT Legazpi LMS", "If you read this, the notifications of the system arrive at this address.", null));
        return ServiceResult.Ok($"A test message was sent to {address}. It can take a minute to arrive; look in the spam folder too.");
    }

    public async Task<NotificationList> GetAsync(int userId, int take = 15, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var items = await db.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAtUtc).ThenByDescending(n => n.Id)
            .Take(Math.Clamp(take, 1, 100))
            .Select(n => new NotificationItem(n.Id, n.Kind, n.Title, n.Body, n.Link, n.IsRead, n.CreatedAtUtc))
            .ToListAsync(ct);

        var unread = await db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);
        return new NotificationList(items, unread);
    }

    /// <summary>The notifications of one kind that the person has not read yet, oldest first (for the box in the middle of the screen, like a guidance message).</summary>
    public async Task<IReadOnlyList<NotificationItem>> GetUnreadOfKindAsync(int userId, string kind, int take = 5, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId && n.Kind == kind && !n.IsRead)
            .OrderBy(n => n.CreatedAtUtc).ThenBy(n => n.Id)
            .Take(Math.Clamp(take, 1, 20))
            .Select(n => new NotificationItem(n.Id, n.Kind, n.Title, n.Body, n.Link, n.IsRead, n.CreatedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<int> UnreadCountAsync(int userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);
    }

    /// <summary>Marks one notification as read. Only the person it was sent to can do that.</summary>
    public async Task MarkReadAsync(int userId, int notificationId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.Notifications.Where(n => n.Id == notificationId && n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(set => set.SetProperty(n => n.IsRead, true), ct);
    }

    public async Task MarkAllReadAsync(int userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.Notifications.Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(set => set.SetProperty(n => n.IsRead, true), ct);
    }

    private static string Limit(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";
}
