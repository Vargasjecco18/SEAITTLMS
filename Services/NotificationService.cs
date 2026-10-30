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
public sealed class NotificationService(IDbContextFactory<AppDbContext> dbFactory, LiveUpdates? live = null)
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
