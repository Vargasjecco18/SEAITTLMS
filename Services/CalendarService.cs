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
        ItemKinds.Activity => "Assignment",
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

/// <summary>
/// An event of a person's own calendar, for the box where it is changed (or, for an event somebody shared, only looked at: <paramref name="IsOwner"/> is false).
/// </summary>
/// <param name="SharedWith">The accounts the owner shared the event with. Only the owner gets the list.</param>
/// <param name="OwnerName">Who wrote the event, for the people it was shared with.</param>
public sealed record CalendarEntryView(
    int Id, string Title, DateOnly Day, TimeOnly? StartTime, string? Notes, IReadOnlyList<int>? SharedWith = null, bool IsOwner = true, string? OwnerName = null);

/// <summary>An activity, quiz or task performance of a teacher that has no due date yet.</summary>
public sealed record UnassignedItem(int ItemId, string Title, string Kind, string SubjectName, string SectionLabel, string ModuleTitle);

/// <summary>
/// What goes on a person's calendar: for a student the deadlines of their work and the modules that appeared; for a
/// teacher the deadlines of their work and the modules they posted or scheduled.
/// </summary>
public sealed class CalendarService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider clock, NotificationService? notifications = null)
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

        // The events other people shared with the person (a group, friends, classmates): on the calendar with who wrote them.
        var shared = await db.CalendarEntryInvites.AsNoTracking()
            .Where(i => i.UserId == user.UserId && i.Entry.Day >= from && i.Entry.Day <= to)
            .Select(i => new { i.Entry.Id, i.Entry.Title, i.Entry.Day, i.Entry.StartTime, i.Entry.UserId })
            .ToListAsync(ct);
        if (shared.Count > 0)
        {
            var owners = await UserDirectory.LoadAsync(db, shared.Select(x => x.UserId).Distinct().ToList(), ct);
            events.AddRange(shared.Select(e => new CalendarEvent($"entry-{e.Id}", e.Day, e.StartTime is { } t ? PhilippineTime.ToUtc(e.Day.ToDateTime(t)) : null, e.Title,
                $"From {(owners.TryGetValue(e.UserId, out var owner) ? owner.Name : "a friend")} · " + (e.StartTime is { } at ? at.ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture) : "All day"),
                CalendarKinds.Personal, $"calendar?event={e.Id}", false, false)));
        }

        return events.OrderBy(e => e.Day).ThenBy(e => e.AtUtc ?? DateTime.MinValue).ThenBy(e => e.Title).ToList();
    }

    // ======================================================================= the person's own events ("Add event")

    public const int MaxEntries = 1000;

    /// <summary>An event of the person (to change it) or one that was shared with them (to look at it). Null when it is neither.</summary>
    public async Task<CalendarEntryView?> GetEntryAsync(int userId, int entryId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var entry = await db.CalendarEntries.AsNoTracking().Where(e => e.Id == entryId)
            .Select(e => new { e.Id, e.Title, e.Day, e.StartTime, e.Notes, e.UserId, Invited = e.Invites.Select(i => i.UserId).ToList() })
            .FirstOrDefaultAsync(ct);
        if (entry is null)
        {
            return null;
        }

        if (entry.UserId == userId)
        {
            return new CalendarEntryView(entry.Id, entry.Title, entry.Day, entry.StartTime, entry.Notes, entry.Invited);
        }

        if (!entry.Invited.Contains(userId))
        {
            return null;
        }

        var owner = (await UserDirectory.LoadAsync(db, [entry.UserId], ct)).GetValueOrDefault(entry.UserId);
        return new CalendarEntryView(entry.Id, entry.Title, entry.Day, entry.StartTime, entry.Notes, null, false, owner?.Name);
    }

    public const int MaxInvited = 100;

    /// <summary>
    /// The accounts the person may share an event with: their friends, the members of their groups, and their classmates (for a teacher: the students of
    /// their classes).
    /// </summary>
    private static async Task<HashSet<int>> CanShareWithAsync(AppDbContext db, int userId, CancellationToken ct)
    {
        var allowed = new HashSet<int>();

        allowed.UnionWith(await db.Friendships.AsNoTracking()
            .Where(f => f.Status == FriendshipStates.Accepted && (f.RequesterUserId == userId || f.AddresseeUserId == userId))
            .Select(f => f.RequesterUserId == userId ? f.AddresseeUserId : f.RequesterUserId)
            .ToListAsync(ct));

        var myGroups = db.StudyGroupMembers.Where(m => m.UserId == userId).Select(m => m.GroupId);
        allowed.UnionWith(await db.StudyGroupMembers.AsNoTracking().Where(m => myGroups.Contains(m.GroupId)).Select(m => m.UserId).Distinct().ToListAsync(ct));

        var studentId = await db.Students.AsNoTracking().Where(s => s.UserId == userId).Select(s => (int?)s.Id).FirstOrDefaultAsync(ct);
        if (studentId is { } sid)
        {
            var mySections = db.Enrollments.Where(e => e.StudentId == sid && e.Status == EnrollmentStatus.Enrolled).Select(e => e.SectionId);
            allowed.UnionWith(await db.Enrollments.AsNoTracking()
                .Where(e => mySections.Contains(e.SectionId) && e.Status == EnrollmentStatus.Enrolled).Select(e => e.Student.UserId).Distinct().ToListAsync(ct));
        }

        var teacherId = await db.Teachers.AsNoTracking().Where(t => t.UserId == userId).Select(t => (int?)t.Id).FirstOrDefaultAsync(ct);
        if (teacherId is { } tid)
        {
            var taught = db.TeachingAssignments.Where(a => a.TeacherId == tid).Select(a => a.SectionId);
            allowed.UnionWith(await db.Enrollments.AsNoTracking()
                .Where(e => taught.Contains(e.SectionId) && e.Status == EnrollmentStatus.Enrolled).Select(e => e.Student.UserId).Distinct().ToListAsync(ct));
        }

        allowed.Remove(userId);
        return allowed;
    }

    /// <summary>
    /// Adds an event to the person's calendar (<paramref name="entryId"/> empty) or changes one of theirs. <paramref name="shareWith"/> are the accounts the event is
    /// shared with (a group's members, friends, classmates: only people the person can reach); with <paramref name="notify"/> the people newly added are told
    /// with a notification. Empty: the event is only the person's own.
    /// </summary>
    public async Task<ServiceResult> SaveEntryAsync(
        int userId, int? entryId, string? title, DateOnly? day, TimeOnly? startTime, string? notes,
        IReadOnlyCollection<int>? shareWith = null, bool notify = false, CancellationToken ct = default)
    {
        title = title?.Trim();
        notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        var invitees = (shareWith ?? []).Where(id => id != userId).Distinct().ToList();

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

        if (invitees.Count > MaxInvited)
        {
            errors.Add($"An event can be shared with at most {MaxInvited} people.");
        }

        if (errors.Count > 0)
        {
            return ServiceResult.Fail(errors);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (invitees.Count > 0)
        {
            var allowed = await CanShareWithAsync(db, userId, ct);
            if (invitees.Any(id => !allowed.Contains(id)))
            {
                return ServiceResult.Fail("An event can be shared only with your friends, the members of your groups and your classmates.");
            }
        }

        CalendarEntry? entry = null;
        if (entryId is { } id)
        {
            entry = await db.CalendarEntries.Include(e => e.Invites).FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct);
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

        // Who it is shared with: the ones who are not in the list any more are taken off, the new ones added.
        var before = entry.Invites.Select(i => i.UserId).ToHashSet();
        foreach (var gone in entry.Invites.Where(i => !invitees.Contains(i.UserId)).ToList())
        {
            entry.Invites.Remove(gone);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var added = invitees.Where(u => !before.Contains(u)).ToList();
        foreach (var who in added)
        {
            entry.Invites.Add(new CalendarEntryInvite { UserId = who, CreatedAtUtc = now });
        }

        await db.SaveChangesAsync(ct);

        if (notify && added.Count > 0 && notifications is not null)
        {
            var owner = (await UserDirectory.LoadAsync(db, [userId], ct)).GetValueOrDefault(userId)?.Name ?? "Somebody";
            var when = entry.Day.ToString("MMM d, yyyy", System.Globalization.CultureInfo.InvariantCulture)
                       + (entry.StartTime is { } at ? " · " + at.ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture) : "");
            await notifications.AddForManyAsync(added, "calendar", $"{owner} shared an event with you: {entry.Title}", when, $"calendar?view=week&date={entry.Day:yyyy-MM-dd}&event={entry.Id}", ct);
        }

        return ServiceResult.Ok(entryId is null ? "The event was added." : "The event was changed.", entry.Id);
    }

    /// <summary>A person takes an event somebody shared with them off their own calendar.</summary>
    public async Task<ServiceResult> LeaveEntryAsync(int userId, int entryId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var removed = await db.CalendarEntryInvites.Where(i => i.EntryId == entryId && i.UserId == userId).ExecuteDeleteAsync(ct);
        return removed == 0 ? ServiceResult.Fail("This event is not on your calendar.") : ServiceResult.Ok("The event is off your calendar.");
    }

    public async Task<ServiceResult> DeleteEntryAsync(int userId, int entryId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var deleted = await db.CalendarEntries.Where(e => e.Id == entryId && e.UserId == userId).ExecuteDeleteAsync(ct);
        return deleted == 0 ? ServiceResult.Fail("This event was not found. It may have been deleted already.") : ServiceResult.Ok("The event was deleted.");
    }
}
