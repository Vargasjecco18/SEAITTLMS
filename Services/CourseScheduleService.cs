using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>"8:00 AM" and the other ways a time of the day is written and read.</summary>
public static class ClassTime
{
    public static string Format(TimeOnly time) => time.ToString("h:mm tt", CultureInfo.InvariantCulture);

    /// <summary>"07:30" (what a time box sends) to a time, or null when it is empty or not a time.</summary>
    public static TimeOnly? Parse(string? text) =>
        TimeOnly.TryParseExact(text?.Trim(), ["HH:mm", "HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ? new TimeOnly(time.Hour, time.Minute) : null;

    /// <summary>"07:30", for a time box.</summary>
    public static string Box(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);
}

/// <summary>When a course meets: the days (MWF or TTH) and the time, for example "MWF · 8:00 AM – 9:30 AM".</summary>
public sealed record MeetingInfo(string Days, TimeOnly Start, TimeOnly End)
{
    public string Times => $"{ClassTime.Format(Start)} – {ClassTime.Format(End)}";

    public string Describe() => $"{Days} · {Times}";
}

/// <summary>AM, MID or PM with its hours.</summary>
public sealed record DayPart(int ScheduleId, string Code, string Name, TimeOnly Start, TimeOnly End)
{
    public string Hours => $"{ClassTime.Format(Start)} – {ClassTime.Format(End)}";
}

public sealed record DayPartInput(int ScheduleId, TimeOnly Start, TimeOnly End);

/// <summary>A section in the list of the class schedule, with how many of its courses have class times.</summary>
public sealed record ScheduleSection(int SectionId, string Label, string? PartCode, string Year, bool CurrentYear, int Courses, int Scheduled)
{
    public string Text => $"{Label}{(PartCode is null ? "" : " · " + PartCode)} · {Year}";
}

/// <summary>One course of a section with its class times (when set), its term and its teacher (when it has one).</summary>
public sealed record CourseTimeRow(int SubjectId, string Code, string Name, byte TermNumber, string TermName, string? TeacherName, MeetingInfo? Meeting);

/// <summary>
/// The class schedule: the hours of AM, MID and PM, and the days (MWF or TTH) and time of each course of each section. The academic coordinator sets them
/// in the academic setup. A course must meet inside the hours of its section's schedule, two courses of one section can't meet at the same time in the
/// same term, and a teacher can't be in two places at once: all of that is checked here, again in the database, before anything is saved.
/// </summary>
public sealed class CourseScheduleService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider clock)
{
    private const string CoordinatorOnly = "Only the academic coordinator can change the class schedule.";

    public static readonly TimeSpan MinLength = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan MaxLength = TimeSpan.FromHours(4);

    private static readonly TimeOnly EarliestHour = new(5, 0);
    private static readonly TimeOnly LatestHour = new(22, 0);

    private static Task<bool> IsCoordinatorAsync(AppDbContext db, int userId, CancellationToken ct) =>
        db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.IsActive && u.RoleId == AppRoles.AcademicCoordinatorId, ct);

    // ======================================================================= the hours of AM, MID and PM

    public async Task<IReadOnlyList<DayPart>> GetDayPartsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Schedules.AsNoTracking().OrderBy(s => s.Id).Select(s => new DayPart(s.Id, s.Code, s.Name, s.StartTime, s.EndTime)).ToListAsync(ct);
    }

    public async Task<ServiceResult> SaveDayPartsAsync(int actingUserId, IReadOnlyList<DayPartInput> inputs, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsCoordinatorAsync(db, actingUserId, ct))
        {
            return ServiceResult.Fail(CoordinatorOnly);
        }

        var parts = await db.Schedules.ToListAsync(ct);
        var errors = new List<string>();
        foreach (var input in inputs)
        {
            var part = parts.FirstOrDefault(p => p.Id == input.ScheduleId);
            if (part is null)
            {
                errors.Add("One of the schedules no longer exists. Reload the page.");
                continue;
            }

            if (input.End <= input.Start)
            {
                errors.Add($"{part.Code}: it must end after it starts.");
            }
            else if (input.Start < EarliestHour || input.End > LatestHour)
            {
                errors.Add($"{part.Code}: the hours must be between {ClassTime.Format(EarliestHour)} and {ClassTime.Format(LatestHour)}.");
            }
            else if (input.End - input.Start < TimeSpan.FromHours(1))
            {
                errors.Add($"{part.Code}: it must be at least one hour long.");
            }
        }

        if (errors.Count > 0)
        {
            return ServiceResult.Fail(errors);
        }

        // A shorter day must still hold the courses that already have times in it.
        foreach (var input in inputs)
        {
            var part = parts.First(p => p.Id == input.ScheduleId);
            var outside = (await db.CourseSchedules.AsNoTracking()
                    .Where(c => c.Section.ScheduleId == part.Id && (c.StartTime < input.Start || c.EndTime > input.End))
                    .Select(c => new { Subject = c.Subject.Name, c.Section.GradeLevel, Strand = c.Section.Strand.Code, c.Section.Name, c.StartTime, c.EndTime })
                    .Take(4).ToListAsync(ct))
                .Select(c => $"{c.Subject} of {TeachingService.SectionLabel(c.GradeLevel, c.Strand, c.Name)} meets {ClassTime.Format(c.StartTime)} – {ClassTime.Format(c.EndTime)}")
                .ToList();
            if (outside.Count > 0)
            {
                errors.Add($"{part.Code} can't be {ClassTime.Format(input.Start)} – {ClassTime.Format(input.End)}: {string.Join("; ", outside)}. Change those class times first.");
            }
        }

        if (errors.Count > 0)
        {
            return ServiceResult.Fail(errors);
        }

        foreach (var input in inputs)
        {
            var part = parts.First(p => p.Id == input.ScheduleId);
            part.StartTime = input.Start;
            part.EndTime = input.End;
        }

        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok("The times of the day were saved.");
    }

    // ======================================================================= the sections and their courses

    /// <summary>Every section, the ones of the school year that is running first.</summary>
    public async Task<IReadOnlyList<ScheduleSection>> ListSectionsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddHours(8));

        var sections = await db.Sections.AsNoTracking()
            .Select(s => new
            {
                s.Id, s.GradeLevel, s.StrandId, Strand = s.Strand.Code, s.Name, Part = s.Schedule != null ? s.Schedule.Code : null,
                Year = s.SchoolYear.Name, s.SchoolYear.StartDate, s.SchoolYear.EndDate,
                Scheduled = db.CourseSchedules.Count(c => c.SectionId == s.Id),
            })
            .ToListAsync(ct);
        var curriculum = await db.StrandSubjects.AsNoTracking().Select(x => new { x.StrandId, x.GradeLevel, x.SubjectId }).ToListAsync(ct);

        return sections
            .Select(s => new
            {
                Section = s,
                Running = s.StartDate <= today && today <= s.EndDate,
                Courses = curriculum.Where(c => c.StrandId == s.StrandId && c.GradeLevel == s.GradeLevel).Select(c => c.SubjectId).Distinct().Count(),
            })
            .OrderByDescending(x => x.Running).ThenByDescending(x => x.Section.StartDate).ThenBy(x => x.Section.GradeLevel).ThenBy(x => x.Section.Strand).ThenBy(x => x.Section.Part).ThenBy(x => x.Section.Name)
            .Select(x => new ScheduleSection(x.Section.Id, TeachingService.SectionLabel(x.Section.GradeLevel, x.Section.Strand, x.Section.Name), x.Section.Part, x.Section.Year, x.Running, x.Courses, x.Section.Scheduled))
            .ToList();
    }

    /// <summary>The courses of the section (its strand's courses at its grade level), by term, with their class times and teachers.</summary>
    public async Task<IReadOnlyList<CourseTimeRow>> ListCoursesAsync(int sectionId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var section = await db.Sections.AsNoTracking().Where(s => s.Id == sectionId).Select(s => new { s.StrandId, s.GradeLevel, s.SchoolYearId }).FirstOrDefaultAsync(ct);
        if (section is null)
        {
            return [];
        }

        var courses = await db.StrandSubjects.AsNoTracking().Where(x => x.StrandId == section.StrandId && x.GradeLevel == section.GradeLevel)
            .Select(x => new { x.SubjectId, x.Subject.Code, x.Subject.Name, x.TermNumber }).ToListAsync(ct);
        var termNames = await db.Terms.AsNoTracking().Where(t => t.SchoolYearId == section.SchoolYearId).ToDictionaryAsync(t => t.TermNumber, t => t.Name, ct);
        var teachers = await db.TeachingAssignments.AsNoTracking().Where(a => a.SectionId == sectionId)
            .Select(a => new { a.SubjectId, Name = a.Teacher.LastName + ", " + a.Teacher.FirstName }).ToListAsync(ct);
        var times = await db.CourseSchedules.AsNoTracking().Where(c => c.SectionId == sectionId).ToListAsync(ct);

        return courses
            .GroupBy(c => c.SubjectId).Select(g => g.OrderBy(x => x.TermNumber).First())
            .OrderBy(c => c.TermNumber).ThenBy(c => c.Name)
            .Select(c => new CourseTimeRow(
                c.SubjectId, c.Code, c.Name, c.TermNumber, termNames.GetValueOrDefault(c.TermNumber, $"Term {c.TermNumber}"),
                teachers.FirstOrDefault(t => t.SubjectId == c.SubjectId)?.Name,
                times.FirstOrDefault(t => t.SubjectId == c.SubjectId) is { } t ? new MeetingInfo(t.Days, t.StartTime, t.EndTime) : null))
            .ToList();
    }

    // ======================================================================= setting and clearing the class time of a course

    public async Task<ServiceResult> SaveAsync(int actingUserId, int sectionId, int subjectId, string? days, TimeOnly? start, TimeOnly? end, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsCoordinatorAsync(db, actingUserId, ct))
        {
            return ServiceResult.Fail(CoordinatorOnly);
        }

        if (!MeetingDays.IsValid(days))
        {
            return ServiceResult.Fail("Choose the days: MWF (Monday, Wednesday, Friday) or TTH (Tuesday, Thursday).");
        }

        if (start is null || end is null)
        {
            return ServiceResult.Fail("Choose when the class starts and when it ends.");
        }

        if (end <= start)
        {
            return ServiceResult.Fail("The class must end after it starts.");
        }

        var length = end.Value - start.Value;
        if (length < MinLength || length > MaxLength)
        {
            return ServiceResult.Fail($"A class is from {(int)MinLength.TotalMinutes} minutes to {(int)MaxLength.TotalHours} hours long.");
        }

        var section = await db.Sections.AsNoTracking().Where(s => s.Id == sectionId)
            .Select(s => new
            {
                s.Id, s.StrandId, s.GradeLevel, s.SchoolYearId, Strand = s.Strand.Code, s.Name,
                Part = s.Schedule != null ? s.Schedule.Code : null, PartStart = s.Schedule != null ? (TimeOnly?)s.Schedule.StartTime : null, PartEnd = s.Schedule != null ? (TimeOnly?)s.Schedule.EndTime : null,
            }).FirstOrDefaultAsync(ct);
        var subject = await db.Subjects.AsNoTracking().Where(s => s.Id == subjectId).Select(s => new { s.Id, s.Name }).FirstOrDefaultAsync(ct);
        if (section is null || subject is null)
        {
            return ServiceResult.Fail("Choose a section and a course.");
        }

        var label = TeachingService.SectionLabel(section.GradeLevel, section.Strand, section.Name);
        var term = await TermOfAsync(db, section.StrandId, section.GradeLevel, subjectId, ct);
        if (term is null)
        {
            return ServiceResult.Fail($"{subject.Name} is not one of the courses of {section.Strand} in grade {section.GradeLevel}.");
        }

        // Inside the hours of the schedule of the section.
        if (section.PartStart is { } from && section.PartEnd is { } to && (start < from || end > to))
        {
            return ServiceResult.Fail($"{label} is in the {section.Part} schedule, {ClassTime.Format(from)} – {ClassTime.Format(to)}: the class must start and end inside it.");
        }

        // Not at the same time as another course of the section in the same term.
        var courseTerms = await db.StrandSubjects.AsNoTracking().Where(x => x.StrandId == section.StrandId && x.GradeLevel == section.GradeLevel)
            .Select(x => new { x.SubjectId, x.TermNumber }).ToListAsync(ct);
        var others = await db.CourseSchedules.AsNoTracking().Where(c => c.SectionId == sectionId && c.SubjectId != subjectId)
            .Select(c => new { c.SubjectId, SubjectName = c.Subject.Name, c.Days, c.StartTime, c.EndTime }).ToListAsync(ct);
        var clash = others.FirstOrDefault(o => o.Days == days && Overlaps(start.Value, end.Value, o.StartTime, o.EndTime)
                                               && courseTerms.Any(t => t.SubjectId == o.SubjectId && t.TermNumber == term));
        if (clash is not null)
        {
            return ServiceResult.Fail($"{clash.SubjectName} of {label} already meets {days} {ClassTime.Format(clash.StartTime)} – {ClassTime.Format(clash.EndTime)}. A section can't have two classes at the same time.");
        }

        // The teacher of the course (if it has one) can't be teaching somewhere else at that time.
        var teacherId = await db.TeachingAssignments.AsNoTracking().Where(a => a.SectionId == sectionId && a.SubjectId == subjectId).Select(a => (int?)a.TeacherId).FirstOrDefaultAsync(ct);
        if (teacherId is { } tid && await TeacherConflictAsync(db, tid, sectionId, subjectId, days!, start.Value, end.Value, ct) is { } conflict)
        {
            return ServiceResult.Fail(conflict);
        }

        var row = await db.CourseSchedules.FirstOrDefaultAsync(c => c.SectionId == sectionId && c.SubjectId == subjectId, ct);
        if (row is null)
        {
            db.CourseSchedules.Add(new CourseSchedule { SectionId = sectionId, SubjectId = subjectId, Days = days!, StartTime = start.Value, EndTime = end.Value });
        }
        else
        {
            row.Days = days!;
            row.StartTime = start.Value;
            row.EndTime = end.Value;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Fail("Somebody else just set the time of that course. Reload and look again.");
        }

        return ServiceResult.Ok($"{subject.Name} of {label} meets {new MeetingInfo(days!, start.Value, end.Value).Describe()}.");
    }

    public async Task<ServiceResult> ClearAsync(int actingUserId, int sectionId, int subjectId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsCoordinatorAsync(db, actingUserId, ct))
        {
            return ServiceResult.Fail(CoordinatorOnly);
        }

        var name = await db.Subjects.AsNoTracking().Where(s => s.Id == subjectId).Select(s => s.Name).FirstOrDefaultAsync(ct);
        var removed = await db.CourseSchedules.Where(c => c.SectionId == sectionId && c.SubjectId == subjectId).ExecuteDeleteAsync(ct);
        return removed == 0 ? ServiceResult.Fail("That course had no class time.") : ServiceResult.Ok($"The class time of {name} was removed.");
    }

    // ======================================================================= what the other pages show

    /// <summary>The class times of the courses of the sections, by section and course.</summary>
    public async Task<IReadOnlyDictionary<(int SectionId, int SubjectId), MeetingInfo>> GetMeetingsAsync(IEnumerable<int> sectionIds, CancellationToken ct = default)
    {
        var ids = sectionIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<(int, int), MeetingInfo>();
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return (await db.CourseSchedules.AsNoTracking().Where(c => ids.Contains(c.SectionId)).ToListAsync(ct))
            .ToDictionary(c => (c.SectionId, c.SubjectId), c => new MeetingInfo(c.Days, c.StartTime, c.EndTime));
    }

    // ======================================================================= the checks other services use too

    /// <summary>
    /// A sentence that says why the teacher can't have this course at this time (another course of theirs, in the same school year and the same term, meets
    /// at the same time), or null when the teacher is free.
    /// </summary>
    internal static async Task<string?> TeacherConflictAsync(AppDbContext db, int teacherId, int sectionId, int subjectId, string days, TimeOnly start, TimeOnly end, CancellationToken ct)
    {
        var mine = await db.Sections.AsNoTracking().Where(s => s.Id == sectionId).Select(s => new { s.StrandId, s.GradeLevel, s.SchoolYearId }).FirstOrDefaultAsync(ct);
        if (mine is null)
        {
            return null;
        }

        var myTerm = await TermOfAsync(db, mine.StrandId, mine.GradeLevel, subjectId, ct);
        var theirs = await db.CourseSchedules.AsNoTracking()
            .Where(c => c.Days == days && !(c.SectionId == sectionId && c.SubjectId == subjectId) && c.Section.SchoolYearId == mine.SchoolYearId
                        && db.TeachingAssignments.Any(a => a.TeacherId == teacherId && a.SectionId == c.SectionId && a.SubjectId == c.SubjectId))
            .Select(c => new
            {
                c.SubjectId, Subject = c.Subject.Name, c.StartTime, c.EndTime, c.Section.StrandId, c.Section.GradeLevel, Strand = c.Section.Strand.Code, SectionName = c.Section.Name,
                Teacher = db.Teachers.Where(t => t.Id == teacherId).Select(t => t.FirstName + " " + t.LastName).FirstOrDefault(),
            }).ToListAsync(ct);

        foreach (var other in theirs.Where(o => Overlaps(start, end, o.StartTime, o.EndTime)))
        {
            if (myTerm is not null && await TermOfAsync(db, other.StrandId, other.GradeLevel, other.SubjectId, ct) != myTerm)
            {
                continue;
            }

            return $"{other.Teacher} already teaches {other.Subject} to {TeachingService.SectionLabel(other.GradeLevel, other.Strand, other.SectionName)} on {days} {ClassTime.Format(other.StartTime)} – {ClassTime.Format(other.EndTime)}. A teacher can't be in two classes at the same time.";
        }

        return null;
    }

    private static bool Overlaps(TimeOnly start, TimeOnly end, TimeOnly otherStart, TimeOnly otherEnd) => start < otherEnd && otherStart < end;

    /// <summary>The term (1, 2, ...) in which the strand takes the course at the grade level, or null when it is not one of its courses.</summary>
    private static async Task<byte?> TermOfAsync(AppDbContext db, int strandId, byte gradeLevel, int subjectId, CancellationToken ct) =>
        await db.StrandSubjects.AsNoTracking().Where(x => x.StrandId == strandId && x.GradeLevel == gradeLevel && x.SubjectId == subjectId)
            .OrderBy(x => x.TermNumber).Select(x => (byte?)x.TermNumber).FirstOrDefaultAsync(ct);
}
