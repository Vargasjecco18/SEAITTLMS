using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Components.Shared;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>What kind of thing is on the calendar (decides its color and icon).</summary>
public static class CalendarKinds
{
    public const string Module = "Module";

    /// <summary>An event the person wrote on their own calendar ("Add event").</summary>
    public const string Personal = "Personal";

    // The three kinds of work use the names of ItemKinds: Activity, Quiz, TaskPerformance.

    public static string Label(string kind) => kind switch
    {
        ItemKinds.Quiz => "Quiz",
        ItemKinds.TaskPerformance => "Task performance",
        ItemKinds.Activity => "Activity",
        Module => "Module",
        Personal => "Event",
        _ => kind,
    };

    public static string Icon(string kind) => kind switch
    {
        Module => "layers",
        Personal => "calendar-clock",
        _ => SubjectStyle.KindIcon(kind),
    };
}

/// <param name="Day">The day in the Philippines it belongs to.</param>
/// <param name="AtUtc">The time of day it happens (a deadline's time), or empty for something that takes the whole day.</param>
public sealed record CalendarEvent(string Key, DateOnly Day, DateTime? AtUtc, string Title, string? Subtitle, string Kind, string? Href, bool Done, bool Overdue);

/// <summary>An event of a person's own calendar, for the box where it is changed.</summary>
public sealed record CalendarEntryView(int Id, string Title, DateOnly Day, TimeOnly? StartTime, string? Notes);

/// <summary>An activity, quiz or task performance of a teacher that has no due date yet.</summary>
public sealed record UnassignedItem(int ItemId, string Title, string Kind, string SubjectName, string SectionLabel, string ModuleTitle);

/// <summary>
/// What goes on a person's calendar: for a student the deadlines of their work and the modules that appeared; for a
/// teacher the deadlines of their work and the modules they posted or scheduled.
/// </summary>
public sealed class CalendarService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider clock)
{
    /// <summary>
    /// The teacher's activities, quizzes and task performances that have no due date, in subject order. The teacher drags them onto a day
    /// of the calendar to give them one.
    /// </summary>
    public async Task<IReadOnlyList<UnassignedItem>> GetUnassignedAsync(int teacherId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await db.ModuleItems.AsNoTracking()
            .Where(i => i.DueAtUtc == null && i.Module.Assignment.TeacherId == teacherId)
            .OrderBy(i => i.Module.Assignment.Subject.Name).ThenBy(i => i.Module.Position).ThenBy(i => i.Position).ThenBy(i => i.Id)
            .Select(i => new
            {
                i.Id, i.Title, i.Kind, Subject = i.Module.Assignment.Subject.Name, ModuleTitle = i.Module.Title,
                i.Module.Assignment.Section.GradeLevel, Strand = i.Module.Assignment.Section.Strand.Code, Section = i.Module.Assignment.Section.Name,
            })
            .ToListAsync(ct);
        return rows.Select(r => new UnassignedItem(r.Id, r.Title, r.Kind, r.Subject, TeachingService.SectionLabel(r.GradeLevel, r.Strand, r.Section), r.ModuleTitle)).ToList();
    }

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

        // The events the person wrote on their own calendar.
        var entries = await db.CalendarEntries.AsNoTracking().Where(e => e.UserId == user.UserId && e.Day >= from && e.Day <= to).ToListAsync(ct);
        events.AddRange(entries.Select(e => new CalendarEvent($"entry-{e.Id}", e.Day, e.StartTime is { } t ? PhilippineTime.ToUtc(e.Day.ToDateTime(t)) : null, e.Title,
            e.StartTime is { } at ? at.ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture) : "All day", CalendarKinds.Personal, $"calendar?event={e.Id}", false, false)));

        return events.OrderBy(e => e.Day).ThenBy(e => e.AtUtc ?? DateTime.MinValue).ThenBy(e => e.Title).ToList();
    }

    // ======================================================================= the person's own events ("Add event")

    public const int MaxEntries = 1000;

    public async Task<CalendarEntryView?> GetEntryAsync(int userId, int entryId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.CalendarEntries.AsNoTracking().Where(e => e.Id == entryId && e.UserId == userId)
            .Select(e => new CalendarEntryView(e.Id, e.Title, e.Day, e.StartTime, e.Notes)).FirstOrDefaultAsync(ct);
    }

    /// <summary>Adds an event to the person's calendar (<paramref name="entryId"/> empty) or changes one of theirs.</summary>
    public async Task<ServiceResult> SaveEntryAsync(int userId, int? entryId, string? title, DateOnly? day, TimeOnly? startTime, string? notes, CancellationToken ct = default)
    {
        title = title?.Trim();
        notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        var errors = new List<string>();
        if (string.IsNullOrEmpty(title))
        {
            errors.Add("Write what the event is.");
        }
        else if (title.Length > 120)
        {
            errors.Add("The title can have at most 120 characters.");
        }

        if (day is null || day.Value.Year is < 2000 or > 2100)
        {
            errors.Add("Choose the day of the event.");
        }

        if (notes is { Length: > 500 })
        {
            errors.Add("The note can have at most 500 characters.");
        }

        if (errors.Count > 0)
        {
            return ServiceResult.Fail(errors);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        CalendarEntry? entry = null;
        if (entryId is { } id)
        {
            entry = await db.CalendarEntries.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct);
            if (entry is null)
            {
                return ServiceResult.Fail("This event was not found. It may have been deleted.");
            }
        }
        else if (await db.CalendarEntries.CountAsync(e => e.UserId == userId, ct) >= MaxEntries)
        {
            return ServiceResult.Fail($"You can keep at most {MaxEntries:N0} events. Delete some old ones first.");
        }

        entry ??= db.CalendarEntries.Add(new CalendarEntry { UserId = userId }).Entity;
        entry.Title = title!;
        entry.Day = day!.Value;
        entry.StartTime = startTime;
        entry.Notes = notes;
        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok(entryId is null ? "The event was added." : "The event was changed.", entry.Id);
    }

    public async Task<ServiceResult> DeleteEntryAsync(int userId, int entryId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var deleted = await db.CalendarEntries.Where(e => e.Id == entryId && e.UserId == userId).ExecuteDeleteAsync(ct);
        return deleted == 0 ? ServiceResult.Fail("This event was not found. It may have been deleted already.") : ServiceResult.Ok("The event was deleted.");
    }
}
