using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>
/// Friends between the students and the teachers of the school: one asks, the other accepts. Saying no, taking back a request and ending a
/// friendship all delete the row, so people can ask again later. Friends can write to each other (see <see cref="MessageService"/>), add
/// each other to groups and share files.
/// </summary>
public sealed class FriendService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider clock, NotificationService notifications)
{
    /// <summary>Asks the other person to be friends. If they had already asked, the two are friends now.</summary>
    public async Task<ServiceResult> RequestAsync(int userId, int otherUserId, CancellationToken ct = default)
    {
        if (userId == otherUserId)
        {
            return ServiceResult.Fail("You can't be your own friend.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var people = await db.Users.AsNoTracking()
            .Where(u => (u.Id == userId || u.Id == otherUserId) && u.IsActive && (u.RoleId == AppRoles.StudentId || u.RoleId == AppRoles.TeacherId))
            .Select(u => u.Id).ToListAsync(ct);
        if (people.Count != 2)
        {
            return ServiceResult.Fail("Friends can only be students and teachers.");
        }

        var existing = await db.Friendships.FirstOrDefaultAsync(f =>
            (f.RequesterUserId == userId && f.AddresseeUserId == otherUserId) || (f.RequesterUserId == otherUserId && f.AddresseeUserId == userId), ct);

        var now = clock.GetUtcNow().UtcDateTime;
        if (existing is not null)
        {
            if (existing.Status == FriendshipStates.Accepted)
            {
                return ServiceResult.Fail("You are already friends.");
            }

            if (existing.RequesterUserId == userId)
            {
                return ServiceResult.Fail("You already asked. Wait for the answer.");
            }

            // The other person had asked first: both want it.
            existing.Status = FriendshipStates.Accepted;
            existing.RespondedAtUtc = now;
            await db.SaveChangesAsync(ct);
            await TellAsync(db, otherUserId, userId, "accepted your friend request", ct);
            return ServiceResult.Ok("You are friends now.");
        }

        db.Friendships.Add(new Friendship { RequesterUserId = userId, AddresseeUserId = otherUserId, Status = FriendshipStates.Pending });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Fail("You already asked. Wait for the answer.");
        }

        await TellAsync(db, otherUserId, userId, "sent you a friend request", ct);
        return ServiceResult.Ok("The request was sent.");
    }

    public async Task<ServiceResult> AcceptAsync(int userId, int requesterUserId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var request = await db.Friendships.FirstOrDefaultAsync(f =>
            f.RequesterUserId == requesterUserId && f.AddresseeUserId == userId && f.Status == FriendshipStates.Pending, ct);
        if (request is null)
        {
            return ServiceResult.Fail("This request is no longer there.");
        }

        request.Status = FriendshipStates.Accepted;
        request.RespondedAtUtc = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        await TellAsync(db, requesterUserId, userId, "accepted your friend request", ct);
        return ServiceResult.Ok("You are friends now.");
    }

    /// <summary>Says no to a request somebody sent to this person.</summary>
    public async Task<ServiceResult> DeclineAsync(int userId, int requesterUserId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var deleted = await db.Friendships.Where(f =>
            f.RequesterUserId == requesterUserId && f.AddresseeUserId == userId && f.Status == FriendshipStates.Pending).ExecuteDeleteAsync(ct);
        return deleted > 0 ? ServiceResult.Ok("The request was declined.") : ServiceResult.Fail("This request is no longer there.");
    }

    /// <summary>Takes back a request this person sent.</summary>
    public async Task<ServiceResult> CancelAsync(int userId, int otherUserId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var deleted = await db.Friendships.Where(f =>
            f.RequesterUserId == userId && f.AddresseeUserId == otherUserId && f.Status == FriendshipStates.Pending).ExecuteDeleteAsync(ct);
        return deleted > 0 ? ServiceResult.Ok("The request was taken back.") : ServiceResult.Fail("This request is no longer there.");
    }

    public async Task<ServiceResult> UnfriendAsync(int userId, int otherUserId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var deleted = await db.Friendships.Where(f => f.Status == FriendshipStates.Accepted
            && ((f.RequesterUserId == userId && f.AddresseeUserId == otherUserId) || (f.RequesterUserId == otherUserId && f.AddresseeUserId == userId))).ExecuteDeleteAsync(ct);
        return deleted > 0 ? ServiceResult.Ok("You are not friends any more.") : ServiceResult.Fail("You are not friends.");
    }

    public async Task<bool> AreFriendsAsync(int userId, int otherUserId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await AreFriendsAsync(db, userId, otherUserId, ct);
    }

    internal static Task<bool> AreFriendsAsync(AppDbContext db, int userId, int otherUserId, CancellationToken ct) =>
        db.Friendships.AsNoTracking().AnyAsync(f => f.Status == FriendshipStates.Accepted
            && ((f.RequesterUserId == userId && f.AddresseeUserId == otherUserId) || (f.RequesterUserId == otherUserId && f.AddresseeUserId == userId)), ct);

    /// <summary>The accounts of the person's friends.</summary>
    public async Task<IReadOnlyList<int>> FriendIdsAsync(int userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Friendships.AsNoTracking()
            .Where(f => f.Status == FriendshipStates.Accepted && (f.RequesterUserId == userId || f.AddresseeUserId == userId))
            .Select(f => f.RequesterUserId == userId ? f.AddresseeUserId : f.RequesterUserId)
            .ToListAsync(ct);
    }

    /// <summary>How many requests wait for the person's answer.</summary>
    public async Task<int> PendingCountAsync(int userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Friendships.CountAsync(f => f.AddresseeUserId == userId && f.Status == FriendshipStates.Pending, ct);
    }

    private async Task TellAsync(AppDbContext db, int toUserId, int fromUserId, string what, CancellationToken ct)
    {
        var people = await UserDirectory.LoadAsync(db, [fromUserId], ct);
        var name = people.TryGetValue(fromUserId, out var person) ? person.Name : "Somebody";
        await notifications.AddAsync(toUserId, "friend", $"{name} {what}", null, "users?tab=friends", ct);
    }
}
