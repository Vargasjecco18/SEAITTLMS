using Microsoft.AspNetCore.Components.Server.Circuits;
using SEAITT_LMS.Auth;

namespace SEAITT_LMS.Services;

/// <summary>Whether the pages of one browser tab are connected to the server right now. A page that was closed stays in memory for a few minutes, but it must not say that its person is online.</summary>
public sealed class CircuitLink
{
    public bool IsUp { get; set; } = true;
}

/// <summary>Tells the <see cref="SessionWatcher"/> when a page (a circuit) of a sign-in is connected and when it is gone.</summary>
public sealed class SessionCircuitHandler(CurrentUser me, SessionWatcher watcher, CircuitLink link) : CircuitHandler
{
    private int userId;
    private string? sessionId;

    public override async Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        link.IsUp = true;
        if (sessionId is null)
        {
            var user = await me.GetAsync();
            if (user is null || string.IsNullOrEmpty(user.SessionId))
            {
                return;
            }

            userId = user.UserId;
            sessionId = user.SessionId;
        }

        watcher.Up(userId, sessionId, circuit.Id);
    }

    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        link.IsUp = false;
        if (sessionId is not null)
        {
            watcher.Down(userId, sessionId, circuit.Id);
        }

        return Task.CompletedTask;
    }

    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken) => OnConnectionDownAsync(circuit, cancellationToken);
}

/// <summary>Every two seconds: ends the sign-ins that have had no page for 10 seconds (see <see cref="SessionWatcher"/>).</summary>
public sealed class SessionWatcherWorker(SessionWatcher watcher, IServiceScopeFactory scopes, ILogger<SessionWatcherWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await EndExpiredAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>Ends what is over; also what the tests call.</summary>
    public async Task EndExpiredAsync(CancellationToken ct = default)
    {
        foreach (var (userId, sessionId) in watcher.TakeExpired())
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<AuthService>().EndSessionAsync(userId, sessionId, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Could not free the account of user {UserId} after its sign-in ended.", userId);
            }
        }
    }
}
