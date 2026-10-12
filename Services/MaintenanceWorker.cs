using SEAITT_LMS.Components.Shared;

namespace SEAITT_LMS.Services;

/// <summary>
/// Looks every few minutes at what the administrator asked for in System Settings: makes the automatic backup once a day (from the chosen hour,
/// Philippine time) and keeps only the newest ones, and deletes the lines of the security log that are older than the days it is kept.
/// </summary>
public sealed class MaintenanceWorker(BackupService backups, SystemSettingsService settings, SecurityLogService securityLog, TimeProvider clock, ILogger<MaintenanceWorker> log) : BackgroundService
{
    private DateOnly lastPurge;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the app finish starting before the first look.
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // A failed run must not stop the next one.
                log.LogWarning(ex, "The automatic backup or the clean-up of the log could not run.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>One look (the worker does this every 5 minutes). Public so that a test can run it.</summary>
    public async Task RunOnceAsync(CancellationToken ct = default)
    {
        var current = settings.Current;
        var now = PhilippineTime.Now(clock);
        var today = DateOnly.FromDateTime(now.DateTime);

        if (current.AutoBackupOn && now.Hour >= current.AutoBackupHour
            && !backups.List().Any(b => b.Kind == "auto" && PhilippineTime.DayOf(b.CreatedAtUtc) == today))
        {
            var result = await backups.CreateAsync("auto", null, ct);
            if (result.Succeeded)
            {
                backups.PruneAutomatic(current.BackupsToKeep);
            }
            else
            {
                log.LogWarning("The automatic backup failed: {Error}", result.Error);
            }
        }

        if (lastPurge != today)
        {
            await securityLog.PurgeAsync(current.LogKeepDays, ct);
            lastPurge = today;
        }
    }
}
