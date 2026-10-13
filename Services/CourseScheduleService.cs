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

/// <summary>When a course meets: the part of the day (AM, MID or PM), the days (MWF or TTH) and the time, for example "AM · MWF · 8:00 AM – 9:30 AM".</summary>
public sealed record MeetingInfo(string Days, TimeOnly Start, TimeOnly End, string? Part = null)
{
    public string Times => $"{ClassTime.Format(Start)} – {ClassTime.Format(End)}";

    /// <summary>"MWF · 8:00 AM – 9:30 AM".</summary>
    public string Describe() => $"{Days} · {Times}";

    /// <summary>"AM · MWF · 8:00 AM – 9:30 AM" (the schedule first), as the enrollment form shows it.</summary>
    public string DescribeFull() => Part is null ? Describe() : $"{Part} · {Describe()}";
}

/// <summary>AM, MID or PM with its hours.</summary>
public sealed record DayPart(int ScheduleId, string Code, string Name, TimeOnly Start, TimeOnly End)
{
    public string Hours => $"{ClassTime.Format(Start)} – {ClassTime.Format(End)}";
}

public sealed record DayPartInput(int ScheduleId, TimeOnly Start, TimeOnly End);

/// <summary>A section in the list of the class schedule, with how many of its courses have class times.</summary>
public sealed record ScheduleSection(int SectionId, string Label, string Year, bool CurrentYear, int Courses, int Scheduled)
{
    public string Text => $"{Label} · {Year}";
}

/// <summary>One course of a section with its class time (when set), its term and its teacher (when it has one).</summary>
public sealed record CourseTimeRow(int SubjectId, string Code, string Name, byte TermNumber, string TermName, string? TeacherName, MeetingInfo? Meeting, int? ScheduleId = null);

/// <summary>What the coordinator typed for one course: nothing at all clears its class time.</summary>
public sealed record CourseTimeInput(int SubjectId, int? ScheduleId, string? Days, TimeOnly? Start, TimeOnly? End)
{
    public bool IsEmpty => ScheduleId is null && string.IsNullOrEmpty(Days) && Start is null && End is null;
}

/// <summary>
/// The class schedule: the hours of AM, MID and PM, and for each course of each section the part of the day, the days (MWF or TTH) and the time.
/// The academic coordinator sets them in the academic setup, all the courses of a section with one Save. A class must be inside the hours of its
/// part of the day, two courses of one section can't meet at the same time in the same term, and a teacher can't be in two places at once: all of
/// that is checked here, again in the database, before anything is saved.
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

        // A shorter day must still hold the classes that already have times in it.
        foreach (var input in inputs)
        {
            var part = parts.First(p => p.Id == input.ScheduleId);
            var outside = (await db.CourseSchedules.AsNoTracking()
                    .Where(c => c.ScheduleId == part.Id && (c.StartTime < input.Start || c.EndTime > input.End))
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
                s.Id, s.GradeLevel, s.StrandId, Strand = s.Strand.Code, s.Name,
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
            .OrderByDescending(x => x.Running).ThenByDescending(x => x.Section.StartDate).ThenBy(x => x.Section.GradeLevel).ThenBy(x => x.Section.Strand).ThenBy(x => x.Section.Name)
            .Select(x => new ScheduleSection(x.Section.Id, TeachingService.SectionLabel(x.Section.GradeLevel, x.Section.Strand, x.Section.Name), x.Section.Year, x.Running, x.Courses, x.Section.Scheduled))
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
        var times = await db.CourseSchedules.AsNoTracking().Include(c => c.Schedule).Where(c => c.SectionId == sectionId).ToListAsync(ct);

        return courses
            .GroupBy(c => c.SubjectId).Select(g => g.OrderBy(x => x.TermNumber).First())
            .OrderBy(c => c.TermNumber).ThenBy(c => c.Name)
            .Select(c => new CourseTimeRow(
                c.SubjectId, c.Code, c.Name, c.TermNumber, termNames.GetValueOrDefault(c.TermNumber, $"Term {c.TermNumber}"),
                teachers.FirstOrDefault(t => t.SubjectId == c.SubjectId)?.Name,
                times.FirstOrDefault(t => t.SubjectId == c.SubjectId) is { } t ? new MeetingInfo(t.Days, t.StartTime, t.EndTime, t.Schedule.Code) : null,
                times.FirstOrDefault(t => t.SubjectId == c.SubjectId)?.ScheduleId))
            .ToList();
    }

    // ======================================================================= setting and clearing the class times

    /// <summary>The class time of one course (the other courses of the section are left as they are).</summary>
    public Task<ServiceResult> SaveAsync(int actingUserId, int sectionId, int subjectId, int? scheduleId, string? days, TimeOnly? start, TimeOnly? end, CancellationToken ct = default) =>
        SaveSectionAsync(actingUserId, sectionId, [new CourseTimeInput(subjectId, scheduleId, days, start, end)], ct);

    /// <summary>
    /// The class times of the courses of a section, all with one Save. A course with nothing chosen has its class time cleared. Everything is checked
    /// first (the courses together too): if anything is wrong nothing is saved and every problem is told, with the name of its course.
    /// </summary>
    public async Task<ServiceResult> SaveSectionAsync(int actingUserId, int sectionId, IReadOnlyList<CourseTimeInput> rows, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsCoordinatorAsync(db, actingUserId, ct))
        {
            return ServiceResult.Fail(CoordinatorOnly);
        }

        var section = await db.Sections.AsNoTracking().Where(s => s.Id == sectionId)
            .Select(s => new { s.Id, s.StrandId, s.GradeLevel, s.SchoolYearId, Strand = s.Strand.Code, s.Name }).FirstOrDefaultAsync(ct);
        if (section is null)
        {
            return ServiceResult.Fail("Choose a section.");
        }

        var label = TeachingService.SectionLabel(section.GradeLevel, section.Strand, section.Name);
        var curriculum = await db.StrandSubjects.AsNoTracking().Where(x => x.StrandId == section.StrandId && x.GradeLevel == section.GradeLevel)
            .Select(x => new { x.SubjectId, x.TermNumber, x.Subject.Name }).ToListAsync(ct);
        var parts = await db.Schedules.AsNoTracking().ToDictionaryAsync(p => p.Id, ct);
        var existing = await db.CourseSchedules.Where(c => c.SectionId == sectionId).ToListAsync(ct);
        var errors = new List<string>();

        // ---- what each row asks for, checked by itself
        var plan = new List<(string Name, int SubjectId, byte Term, CourseTimeInput? Input, CourseSchedule? Old)>();
        foreach (var row in rows)
        {
            var entry = curriculum.Where(c => c.SubjectId == row.SubjectId).OrderBy(c => c.TermNumber).FirstOrDefault();
            if (entry is null)
            {
                var name = await db.Subjects.AsNoTracking().Where(s => s.Id == row.SubjectId).Select(s => s.Name).FirstOrDefaultAsync(ct);
                errors.Add($"{name ?? "That course"} is not one of the courses of {section.Strand} in grade {section.GradeLevel}.");
                continue;
            }

            var old = existing.FirstOrDefault(e => e.SubjectId == row.SubjectId);
            if (row.IsEmpty)
            {
                plan.Add((entry.Name, row.SubjectId, entry.TermNumber, null, old));
                continue;
            }

            var problem = Check(row, parts);
            if (problem is not null)
            {
                errors.Add($"{entry.Name}: {problem}");
                continue;
            }

            plan.Add((entry.Name, row.SubjectId, entry.TermNumber, row, old));
        }

        if (errors.Count > 0)
        {
            return ServiceResult.Fail(errors);
        }

        // ---- the section as it will be: two courses of the same term never at the same time on the same days
        var future = new List<(string Name, int SubjectId, byte Term, string Days, TimeOnly Start, TimeOnly End)>();
        foreach (var e in existing.Where(e => plan.All(p => p.SubjectId != e.SubjectId)))
        {
            var c = curriculum.Where(x => x.SubjectId == e.SubjectId).OrderBy(x => x.TermNumber).FirstOrDefault();
            if (c is not null)
            {
                future.Add((c.Name, e.SubjectId, c.TermNumber, e.Days, e.StartTime, e.EndTime));
            }
        }

        future.AddRange(plan.Where(p => p.Input is not null).Select(p => (p.Name, p.SubjectId, p.Term, p.Input!.Days!, p.Input.Start!.Value, p.Input.End!.Value)));
        for (var i = 0; i < future.Count; i++)
        {
            for (var j = i + 1; j < future.Count; j++)
            {
                var a = future[i];
                var b = future[j];
                if (a.Term == b.Term && a.Days == b.Days && Overlaps(a.Start, a.End, b.Start, b.End)
                    && (plan.Any(p => p.SubjectId == a.SubjectId && p.Input is not null) || plan.Any(p => p.SubjectId == b.SubjectId && p.Input is not null)))
                {
                    errors.Add($"{a.Name} ({a.Days} {ClassTime.Format(a.Start)} – {ClassTime.Format(a.End)}) and {b.Name} ({b.Days} {ClassTime.Format(b.Start)} – {ClassTime.Format(b.End)}) of {label} would meet at the same time. A section can't have two classes at the same time.");
                }
            }
        }

        // ---- a teacher can't be teaching somewhere else at that time
        foreach (var p in plan.Where(p => p.Input is not null))
        {
            var teacherId = await db.TeachingAssignments.AsNoTracking().Where(a => a.SectionId == sectionId && a.SubjectId == p.SubjectId).Select(a => (int?)a.TeacherId).FirstOrDefaultAsync(ct);
            if (teacherId is { } tid && await TeacherConflictAsync(db, tid, sectionId, p.SubjectId, p.Input!.Days!, p.Input.Start!.Value, p.Input.End!.Value, ct) is { } conflict)
            {
                errors.Add($"{p.Name}: {conflict}");
            }
        }

        if (errors.Count > 0)
        {
            return ServiceResult.Fail(errors);
        }

        // ---- save
        var set = 0;
        var cleared = 0;
        foreach (var p in plan)
        {
            if (p.Input is null)
            {
                if (p.Old is not null)
                {
                    db.CourseSchedules.Remove(p.Old);
                    cleared++;
                }

                continue;
            }

            if (p.Old is null)
            {
                db.CourseSchedules.Add(new CourseSchedule { SectionId = sectionId, SubjectId = p.SubjectId, ScheduleId = p.Input.ScheduleId!.Value, Days = p.Input.Days!, StartTime = p.Input.Start!.Value, EndTime = p.Input.End!.Value });
                set++;
            }
            else if (p.Old.ScheduleId != p.Input.ScheduleId || p.Old.Days != p.Input.Days || p.Old.StartTime != p.Input.Start || p.Old.EndTime != p.Input.End)
            {
                p.Old.ScheduleId = p.Input.ScheduleId!.Value;
                p.Old.Days = p.Input.Days!;
                p.Old.StartTime = p.Input.Start!.Value;
                p.Old.EndTime = p.Input.End!.Value;
                set++;
            }
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Fail("Somebody else just changed the class times of that section. Reload and look again.");
        }

        if (set == 0 && cleared == 0)
        {
            return ServiceResult.Ok($"Nothing changed in the class schedule of {label}.");
        }

        var onlyOne = plan.Count == 1 && plan[0].Input is { } one ? $"{plan[0].Name} of {label} meets {new MeetingInfo(one.Days!, one.Start!.Value, one.End!.Value, parts[one.ScheduleId!.Value].Code).DescribeFull()}." : null;
        return ServiceResult.Ok(onlyOne ?? $"The class schedule of {label} was saved ({set} set, {cleared} cleared).");
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

    /// <summary>What is wrong with one course's class time by itself (the part of the day, the days, the hours), or null.</summary>
    private static string? Check(CourseTimeInput row, IReadOnlyDictionary<int, Schedule> parts)
    {
        if (row.ScheduleId is not { } scheduleId || !parts.TryGetValue(scheduleId, out var part))
        {
            return "choose the schedule: AM, MID or PM.";
        }

        if (!MeetingDays.IsValid(row.Days))
        {
            return "choose the days: MWF (Monday, Wednesday, Friday) or TTH (Tuesday, Thursday).";
        }

        if (row.Start is not { } start || row.End is not { } end)
        {
            return "choose when the class starts and when it ends.";
        }

        if (end <= start)
        {
            return "the class must end after it starts.";
        }

        var length = end - start;
        if (length < MinLength || length > MaxLength)
        {
            return $"a class is from {(int)MinLength.TotalMinutes} minutes to {(int)MaxLength.TotalHours} hours long.";
        }

        if (start < part.StartTime || end > part.EndTime)
        {
            return $"the {part.Code} schedule is {ClassTime.Format(part.StartTime)} – {ClassTime.Format(part.EndTime)}: the class must start and end inside it.";
        }

        return null;
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
        return (await db.CourseSchedules.AsNoTracking().Include(c => c.Schedule).Where(c => ids.Contains(c.SectionId)).ToListAsync(ct))
            .ToDictionary(c => (c.SectionId, c.SubjectId), c => new MeetingInfo(c.Days, c.StartTime, c.EndTime, c.Schedule.Code));
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
