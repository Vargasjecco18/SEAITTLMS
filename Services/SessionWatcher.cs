using System.Collections.Concurrent;

namespace SEAITT_LMS.Services;

/// <summary>
/// Knows which sign-ins still have a page open. Every page of the system is held open by a live connection to the server (a "circuit"); when the
/// browser is closed, or the person goes back out of the system, that connection ends. When a sign-in has had no connection for
/// <see cref="Grace"/> (10 seconds: long enough for a refresh or for moving from one page to the next, no countdown is shown), the sign-in is over: the person has to log in again,
/// and is offline. It lives as long as the app and keeps nothing on disk (the account's open sign-in is also cleared in the database, see
/// <see cref="Auth.AuthService.EndSessionAsync"/>).
/// </summary>
public sealed class SessionWatcher(TimeProvider clock, PresenceService presence)
{
    /// <summary>How long a sign-in may be without any page before it is ended.</summary>
    public static readonly TimeSpan Grace = TimeSpan.FromSeconds(10);

    private sealed class Entry(int userId)
    {
        public int UserId { get; } = userId;

        public HashSet<string> Circuits { get; } = [];

        /// <summary>When the last page of the sign-in went away; null while one is open.</summary>
        public DateTime? GoneSince { get; set; }
    }

    private readonly object gate = new();
    private readonly Dictionary<string, Entry> sessions = [];
    private readonly ConcurrentDictionary<string, DateTime> ended = new();

    /// <summary>A page (a circuit) of this sign-in is connected. False when the sign-in was already ended (the page has to go to the login page).</summary>
    public bool Up(int userId, string sessionId, string circuitId)
    {
        if (ended.ContainsKey(sessionId))
        {
            return false;
        }

        lock (gate)
        {
            if (!sessions.TryGetValue(sessionId, out var entry))
            {
                entry = new Entry(userId);
                sessions[sessionId] = entry;
            }

            entry.Circuits.Add(circuitId);
            entry.GoneSince = null;
        }

        presence.Touch(userId);
        return true;
    }

    /// <summary>A page (a circuit) of this sign-in went away (the browser or the tab was closed, the person left the system). The person is offline from now on, unless another page of theirs is open.</summary>
    public void Down(int userId, string sessionId, string circuitId)
    {
        var othersOpen = false;
        lock (gate)
        {
            if (sessions.TryGetValue(sessionId, out var entry))
            {
                entry.Circuits.Remove(circuitId);
                if (entry.Circuits.Count == 0)
                {
                    entry.GoneSince = clock.GetUtcNow().UtcDateTime;
                }
            }

            othersOpen = sessions.Values.Any(e => e.UserId == userId && e.Circuits.Count > 0);
        }

        if (!othersOpen)
        {
            presence.Forget(userId);
        }
    }

    /// <summary>The person pressed Log out: this sign-in is over at once and the person is offline.</summary>
    public void End(int userId, string? sessionId)
    {
        var othersOpen = false;
        if (!string.IsNullOrEmpty(sessionId))
        {
            ended[sessionId] = clock.GetUtcNow().UtcDateTime;
            lock (gate)
            {
                sessions.Remove(sessionId);
                othersOpen = sessions.Values.Any(e => e.UserId == userId && e.Circuits.Count > 0);
            }
        }

        // The same person may be signed in somewhere else with a page open: they are still online there.
        if (!othersOpen)
        {
            presence.Forget(userId);
        }
    }

    /// <summary>True when this sign-in was ended (logged out, or without a page for too long): its cookie is not good any more.</summary>
    public bool IsEnded(string? sessionId) => !string.IsNullOrEmpty(sessionId) && ended.ContainsKey(sessionId);

    /// <summary>The sign-ins that have had no page for the whole <see cref="Grace"/>. They are ended from now on (<see cref="IsEnded"/>); the caller clears them in the database too.</summary>
    public IReadOnlyList<(int UserId, string SessionId)> TakeExpired()
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var expired = new List<(int, string)>();
        HashSet<int> stillOpen;
        lock (gate)
        {
            foreach (var (sessionId, entry) in sessions.ToList())
            {
                if (entry.Circuits.Count == 0 && entry.GoneSince is { } gone && now - gone >= Grace)
                {
                    sessions.Remove(sessionId);
                    expired.Add((entry.UserId, sessionId));
                }
            }

            stillOpen = sessions.Values.Where(e => e.Circuits.Count > 0).Select(e => e.UserId).ToHashSet();
        }

        foreach (var (userId, sessionId) in expired)
        {
            ended[sessionId] = now;

            // A person who signed in again (or in another browser) and has a page open there is still online.
            if (!stillOpen.Contains(userId))
            {
                presence.Forget(userId);
            }
        }

        // An ended sign-in is remembered for longer than a cookie lasts, then forgotten.
        foreach (var old in ended.Where(kv => now - kv.Value > TimeSpan.FromHours(24)).Select(kv => kv.Key).ToList())
        {
            ended.TryRemove(old, out _);
        }

        return expired;
    }
}
