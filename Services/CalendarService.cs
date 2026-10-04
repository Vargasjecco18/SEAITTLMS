using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Components.Shared;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>What kind of thing is on the calendar (decides its color and icon).</summary>
public static class CalendarKinds
{
    public const string Module = "Module";
    public const string Todo = "Todo";

    // The three kinds of work use the names of ItemKinds: Activity, Quiz, TaskPerformance.

    public static string Label(string kind) => kind switch
    {
        ItemKinds.Quiz => "Quiz",
        ItemKinds.TaskPerformance => "Task performance",
        ItemKinds.Activity => "Activity",
        Module => "Module",
        Todo => "To-do",
        _ => kind,
    };

    public static string Icon(string kind) => kind switch
    {
        Module => "layers",
        Todo => "list-checks",
        _ => SubjectStyle.KindIcon(kind),
    };
}

/// <param name="Day">The day in the Philippines it belongs to.</param>
/// <param name="AtUtc">The time of day it happens (a deadline's time), or empty for something that takes the whole day.</param>
public sealed record CalendarEvent(string Key, DateOnly Day, DateTime? AtUtc, string Title, string? Subtitle, string Kind, string? Href, bool Done, bool Overdue);

/// <summary>
/// What goes on a person's calendar: for a student the deadlines of their work, the modules that appeared and their to-do list; for a
/// teacher the deadlines of their work, the modules they posted or scheduled and their to-do list.
/// </summary>
public sealed class CalendarService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider clock)
{
    /// <summary>The events of the days from <paramref name="from"/> to <paramref name="to"/>, both included, oldest first.</summary>
    public async Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(CurrentUserInfo user, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        if (to < from)
        {
            return [];
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var fromUtc = PhilippineTime.ToUtc(from.ToDateTime(TimeOnly.MinValue));
        var toUtc = PhilippineTime.ToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue));

        var events = new List<CalendarEvent>();

        if (user.Role == AppRoles.Student)
        {
            var studentId = await db.Students.AsNoTracking().Where(s => s.UserId == user.UserId).Select(s => (int?)s.Id).FirstOrDefaultAsync(ct);
            if (studentId is { } sid)
            {
                var assignments = Roster.AssignmentsOf(db, sid);

                var due = await db.ModuleItems.AsNoTracking()
                    .Where(i => i.DueAtUtc != null && i.DueAtUtc >= fromUtc && i.DueAtUtc < toUtc
                                && (i.VisibleFromUtc == null || i.VisibleFromUtc <= now) && i.Module.VisibleFromUtc != null && i.Module.VisibleFromUtc <= now
                                && assignments.Any(a => a.Id == i.Module.AssignmentId))
                    .Select(i => new
                    {
                        i.Id, i.Title, i.Kind, Due = i.DueAtUtc!.Value, Subject = i.Module.Assignment.Subject.Name,
                        Status = i.Submissions.Where(s => s.StudentId == sid).Select(s => s.Status).FirstOrDefault(),
                    })
                    .ToListAsync(ct);
                foreach (var i in due)
                {
                    var done = i.Status is SubmissionStatus.Submitted or SubmissionStatus.Graded;
                    events.Add(new CalendarEvent($"item-{i.Id}", PhilippineTime.DayOf(i.Due), i.Due, i.Title, $"{i.Subject} · due {PhilippineTime.FormatTime(i.Due)}", i.Kind, $"student/items/{i.Id}", done, !done && i.Due < now));
                }

                var modules = await db.Modules.AsNoTracking()
                    .Where(m => m.VisibleFromUtc != null && m.VisibleFromUtc >= fromUtc && m.VisibleFromUtc < toUtc && m.VisibleFromUtc <= now
                                && assignments.Any(a => a.Id == m.AssignmentId))
                    .Select(m => new { m.Id, m.Title, At = m.VisibleFromUtc!.Value, Subject = m.Assignment.Subject.Name })
                    .ToListAsync(ct);
                events.AddRange(modules.Select(m => new CalendarEvent($"module-{m.Id}", PhilippineTime.DayOf(m.At), m.At, "New module: " + m.Title, m.Subject, CalendarKinds.Module, $"student/modules/{m.Id}", false, false)));
            }
        }
        else if (user.Role == AppRoles.Teacher)
        {
            var teacherId = await db.Teachers.AsNoTracking().Where(t => t.UserId == user.UserId).Select(t => (int?)t.Id).FirstOrDefaultAsync(ct);
            if (teacherId is { } tid)
            {
                var due = await db.ModuleItems.AsNoTracking()
                    .Where(i => i.DueAtUtc != null && i.DueAtUtc >= fromUtc && i.DueAtUtc < toUtc && i.Module.Assignment.TeacherId == tid)
                    .Select(i => new
                    {
                        i.Id, i.Title, i.Kind, Due = i.DueAtUtc!.Value, Subject = i.Module.Assignment.Subject.Name,
                        i.Module.Assignment.Section.GradeLevel, Strand = i.Module.Assignment.Section.Strand.Code, Section = i.Module.Assignment.Section.Name,
                        Handed = i.Submissions.Count(s => s.Status != SubmissionStatus.InProgress),
                    })
                    .ToListAsync(ct);
                events.AddRange(due.Select(i => new CalendarEvent($"item-{i.Id}", PhilippineTime.DayOf(i.Due), i.Due, i.Title,
                    $"{i.Subject} · {TeachingService.SectionLabel(i.GradeLevel, i.Strand, i.Section)} · {i.Handed} handed in", i.Kind, $"teacher/items/{i.Id}", false, false)));

                var modules = await db.Modules.AsNoTracking()
                    .Where(m => m.VisibleFromUtc != null && m.VisibleFromUtc >= fromUtc && m.VisibleFromUtc < toUtc && m.Assignment.TeacherId == tid)
                    .Select(m => new
                    {
                        m.Id, m.Title, At = m.VisibleFromUtc!.Value, Subject = m.Assignment.Subject.Name, m.AssignmentId,
                    })
                    .ToListAsync(ct);
                events.AddRange(modules.Select(m => new CalendarEvent($"module-{m.Id}", PhilippineTime.DayOf(m.At), m.At,
                    (m.At > now ? "Module posts: " : "Module posted: ") + m.Title, m.Subject, CalendarKinds.Module, $"teacher/subjects/{m.AssignmentId}", m.At <= now, false)));
            }
        }

        // The person's own to-do list (students and teachers).
        var todos = await db.TodoItems.AsNoTracking()
            .Where(t => t.UserId == user.UserId && t.DueOn != null && t.DueOn >= from && t.DueOn <= to)
            .Select(t => new { t.Id, t.Title, DueOn = t.DueOn!.Value, t.IsDone })
            .ToListAsync(ct);
        var today = PhilippineTime.Today(clock);
        events.AddRange(todos.Select(t => new CalendarEvent($"todo-{t.Id}", t.DueOn, null, t.Title, "To-do", CalendarKinds.Todo, null, t.IsDone, !t.IsDone && t.DueOn < today)));

        return events.OrderBy(e => e.Day).ThenBy(e => e.AtUtc ?? DateTime.MinValue).ThenBy(e => e.Title).ToList();
    }
}
