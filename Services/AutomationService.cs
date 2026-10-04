using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Components.Shared;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>
/// The things the system does by itself: tell students when a module or an activity appears (also when the teacher scheduled it for
/// later) and remind them a day before something is due. Each thing is told once: a row is "claimed" with an update that only
/// succeeds for the first caller, so two runs at the same moment can't send the same notice twice.
/// </summary>
public static class AutomationService
{
    /// <summary>How long before the deadline the reminder goes out.</summary>
    public static readonly TimeSpan ReminderBefore = TimeSpan.FromHours(24);

    /// <summary>Tells the students about every module and item that is visible now and was not told yet. Returns how many notices were sent.</summary>
    public static async Task<int> NotifyNewContentAsync(IDbContextFactory<AppDbContext> dbFactory, NotificationService notifications, DateTime nowUtc, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var sent = 0;

        // ---- modules that appeared
        var modules = await db.Modules.AsNoTracking()
            .Where(m => m.VisibleFromUtc != null && m.VisibleFromUtc <= nowUtc && m.NotifiedAtUtc == null)
            .Select(m => new { m.Id, m.Title, m.AssignmentId, m.Assignment.SectionId, m.Assignment.SubjectId, SubjectName = m.Assignment.Subject.Name })
            .ToListAsync(ct);

        foreach (var m in modules)
        {
            var claimed = await db.Modules.Where(x => x.Id == m.Id && x.NotifiedAtUtc == null)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.NotifiedAtUtc, nowUtc), ct);
            if (claimed == 0)
            {
                continue;
            }

            // The activities that come with the module are covered by this one notice.
            await db.ModuleItems.Where(i => i.ModuleId == m.Id && i.NotifiedAtUtc == null && (i.VisibleFromUtc == null || i.VisibleFromUtc <= nowUtc))
                .ExecuteUpdateAsync(set => set.SetProperty(i => i.NotifiedAtUtc, nowUtc), ct);

            var users = await StudentUserIdsAsync(db, m.SectionId, m.SubjectId, ct);
            await notifications.AddForManyAsync(users, "module", $"New module in {m.SubjectName}", m.Title, $"student/modules/{m.Id}", ct);
            sent += users.Count;
        }

        // ---- items that appeared later, in a module that was already shown
        var items = await db.ModuleItems.AsNoTracking()
            .Where(i => i.NotifiedAtUtc == null && (i.VisibleFromUtc == null || i.VisibleFromUtc <= nowUtc)
                        && i.Module.VisibleFromUtc != null && i.Module.VisibleFromUtc <= nowUtc && i.Module.NotifiedAtUtc != null)
            .Select(i => new
            {
                i.Id, i.Kind, i.Title, i.DueAtUtc, i.Module.Assignment.SectionId, i.Module.Assignment.SubjectId, SubjectName = i.Module.Assignment.Subject.Name,
            })
            .ToListAsync(ct);

        foreach (var i in items)
        {
            var claimed = await db.ModuleItems.Where(x => x.Id == i.Id && x.NotifiedAtUtc == null)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.NotifiedAtUtc, nowUtc), ct);
            if (claimed == 0)
            {
                continue;
            }

            var users = await StudentUserIdsAsync(db, i.SectionId, i.SubjectId, ct);
            var body = i.DueAtUtc is { } due ? $"{i.Title} · due {PhilippineTime.FormatDateTime(due)}" : i.Title;
            await notifications.AddForManyAsync(users, "activity", $"New {ItemKinds.Display(i.Kind).ToLowerInvariant()} in {i.SubjectName}", body, $"student/items/{i.Id}", ct);
            sent += users.Count;
        }

        return sent;
    }

    /// <summary>Reminds the students who have not handed in yet that something is due within a day. Returns how many reminders were sent.</summary>
    public static async Task<int> SendDueRemindersAsync(IDbContextFactory<AppDbContext> dbFactory, NotificationService notifications, DateTime nowUtc, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var until = nowUtc + ReminderBefore;
        var sent = 0;

        var due = await db.ModuleItems.AsNoTracking()
            .Where(i => i.DueAtUtc != null && i.DueAtUtc > nowUtc && i.DueAtUtc <= until && i.DueReminderSentAtUtc == null
                        && (i.VisibleFromUtc == null || i.VisibleFromUtc <= nowUtc)
                        && i.Module.VisibleFromUtc != null && i.Module.VisibleFromUtc <= nowUtc)
            .Select(i => new
            {
                i.Id, i.Title, i.Kind, i.DueAtUtc, i.Module.Assignment.SectionId, i.Module.Assignment.SubjectId, SubjectName = i.Module.Assignment.Subject.Name,
            })
            .ToListAsync(ct);

        foreach (var i in due)
        {
            var claimed = await db.ModuleItems.Where(x => x.Id == i.Id && x.DueReminderSentAtUtc == null)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.DueReminderSentAtUtc, nowUtc), ct);
            if (claimed == 0)
            {
                continue;
            }

            var done = await db.Submissions.Where(s => s.ItemId == i.Id && s.Status != SubmissionStatus.InProgress).Select(s => s.StudentId).ToListAsync(ct);
            var studentUsers = await Roster.Enrollments(db, i.SectionId, i.SubjectId)
                .Where(e => !done.Contains(e.StudentId)).Select(e => e.Student.UserId).Distinct().ToListAsync(ct);

            await notifications.AddForManyAsync(studentUsers, "due", $"Due soon: {i.Title}", $"{i.SubjectName} · {PhilippineTime.DueText(i.DueAtUtc!.Value, PhilippineTime.FromUtc(nowUtc))}", $"student/items/{i.Id}", ct);
            sent += studentUsers.Count;
        }

        return sent;
    }

    private static async Task<List<int>> StudentUserIdsAsync(AppDbContext db, int sectionId, int subjectId, CancellationToken ct) =>
        await Roster.Enrollments(db, sectionId, subjectId).Select(e => e.Student.UserId).Distinct().ToListAsync(ct);
}

/// <summary>Runs <see cref="AutomationService"/> once a minute while the app is running.</summary>
public sealed class AutomationWorker(IServiceScopeFactory scopes, TimeProvider clock, ILogger<AutomationWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the app finish starting (and the database be prepared) before the first run.
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
                var notifications = scope.ServiceProvider.GetRequiredService<NotificationService>();
                var now = clock.GetUtcNow().UtcDateTime;

                await AutomationService.NotifyNewContentAsync(dbFactory, notifications, now, stoppingToken);
                await AutomationService.SendDueRemindersAsync(dbFactory, notifications, now, stoppingToken);
                await QuizService.FinalizeExpiredAsync(dbFactory, now, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // A failed run must not stop the next one.
                log.LogWarning(ex, "The automatic notifications could not run.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
