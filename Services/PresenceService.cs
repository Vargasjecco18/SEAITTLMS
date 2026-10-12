using System.Collections.Concurrent;

namespace SEAITT_LMS.Services;

/// <summary>
/// Who is online. Every page that is open tells the app (the header of the page, every few seconds) that its person is there; a person
/// is online when that was heard in the last <see cref="OnlineWindow"/>. It lives as long as the app and keeps nothing on disk: after the
/// app restarts, everybody is offline until their open pages say hello again (within a few seconds).
/// </summary>
public sealed class PresenceService(TimeProvider clock)
{
    /// <summary>The header says hello every 10 seconds, so a person who closed the last page is offline after this.</summary>
    public static readonly TimeSpan OnlineWindow = TimeSpan.FromSeconds(45);

    private readonly ConcurrentDictionary<int, DateTime> lastSeen = new();
    private DateTime lastClean = DateTime.MinValue;

    /// <summary>This person has a page open right now.</summary>
    public void Touch(int userId)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        lastSeen[userId] = now;

        // Now and then forget the people who have been gone for a long time.
        if (now - lastClean > TimeSpan.FromMinutes(10))
        {
            lastClean = now;
            foreach (var old in lastSeen.Where(kv => now - kv.Value > TimeSpan.FromHours(1)).Select(kv => kv.Key).ToList())
            {
                lastSeen.TryRemove(old, out _);
            }
        }
    }

    public bool IsOnline(int userId) =>
        lastSeen.TryGetValue(userId, out var seen) && clock.GetUtcNow().UtcDateTime - seen <= OnlineWindow;

    /// <summary>Which of these people are online.</summary>
    public HashSet<int> OnlineAmong(IEnumerable<int> userIds) => userIds.Where(IsOnline).ToHashSet();

    /// <summary>When the person was last heard of, or null when not since the app started.</summary>
    public DateTime? LastSeenUtc(int userId) => lastSeen.TryGetValue(userId, out var seen) ? seen : null;

    public int OnlineCount
    {
        get
        {
            var now = clock.GetUtcNow().UtcDateTime;
            return lastSeen.Count(kv => now - kv.Value <= OnlineWindow);
        }
    }
}
