using System.Collections.Concurrent;

namespace SEAITT_LMS.Services;

/// <summary>
/// Tells the pages that are open for a person that something new arrived for them (a message, a notification), so the numbers in the
/// header and the conversation in the messages box change at once instead of at the next look. It lives as long as the app and works
/// inside this one running app; a page that is open also looks again every few seconds, in case the news came from somewhere else.
/// </summary>
public sealed class LiveUpdates
{
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<Guid, Func<Task>>> listeners = new();

    /// <summary>Asks to be told when something new arrives for this person. Dispose the result to stop.</summary>
    public IDisposable Subscribe(int userId, Func<Task> onChanged)
    {
        var id = Guid.NewGuid();
        listeners.GetOrAdd(userId, _ => new ConcurrentDictionary<Guid, Func<Task>>())[id] = onChanged;
        return new Subscription(this, userId, id);
    }

    /// <summary>Something new arrived for these people: every page they have open is told.</summary>
    public void Publish(IEnumerable<int> userIds)
    {
        foreach (var userId in userIds.Distinct())
        {
            if (!listeners.TryGetValue(userId, out var own))
            {
                continue;
            }

            foreach (var listener in own.Values)
            {
                _ = Run(listener);
            }
        }
    }

    private static async Task Run(Func<Task> listener)
    {
        try
        {
            await listener();
        }
        catch (Exception)
        {
            // A page that closed in the meantime can't be told. That is fine.
        }
    }

    private sealed class Subscription(LiveUpdates owner, int userId, Guid id) : IDisposable
    {
        public void Dispose()
        {
            if (owner.listeners.TryGetValue(userId, out var own))
            {
                own.TryRemove(id, out _);
            }
        }
    }
}
