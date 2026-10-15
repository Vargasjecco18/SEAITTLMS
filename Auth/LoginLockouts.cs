using System.Collections.Concurrent;

namespace SEAITT_LMS.Auth;

/// <summary>
/// Counts the wrong sign-ins of an email that has NO account and locks it like a real account is locked. That way "this account is locked" (and the time that is
/// left) is said for every email after the same number of tries, and nobody can find out which emails have an account by trying them. One for the whole app.
/// </summary>
public sealed class LoginLockouts
{
    private sealed record Entry(int Failures, DateTime LastUtc, DateTime? LockedUntilUtc);

    private readonly ConcurrentDictionary<string, Entry> entries = new();

    /// <summary>When the email is locked until, or null when it is not locked now.</summary>
    public DateTime? LockedUntil(string email, DateTime now) =>
        entries.TryGetValue(email, out var entry) && entry.LockedUntilUtc is { } until && until > now ? until : null;

    /// <summary>Counts a wrong try. Gives back the moment the lock ends when this try locked the email, otherwise null.</summary>
    public DateTime? RegisterFailure(string email, int maxFailed, int lockoutMinutes, DateTime now)
    {
        if (entries.Count > 10_000)
        {
            // Forget the emails nobody tried for an hour (somebody may be filling the list with made-up emails).
            foreach (var old in entries.Where(e => e.Value.LastUtc < now.AddHours(-1) && (e.Value.LockedUntilUtc ?? DateTime.MinValue) < now).Select(e => e.Key).ToList())
            {
                entries.TryRemove(old, out _);
            }
        }

        DateTime? lockedUntil = null;
        entries.AddOrUpdate(
            email,
            _ => Next(null, maxFailed, lockoutMinutes, now, out lockedUntil),
            (_, current) => Next(current, maxFailed, lockoutMinutes, now, out lockedUntil));
        return lockedUntil;
    }

    private static Entry Next(Entry? current, int maxFailed, int lockoutMinutes, DateTime now, out DateTime? lockedUntil)
    {
        // A lock that already ran out starts counting again from zero, the way a real account does.
        var failures = current is null || (current.LockedUntilUtc is { } old && old <= now) ? 1 : current.Failures + 1;
        if (failures >= maxFailed)
        {
            lockedUntil = now.AddMinutes(lockoutMinutes);
            return new Entry(0, now, lockedUntil);
        }

        lockedUntil = null;
        return new Entry(failures, now, null);
    }
}
