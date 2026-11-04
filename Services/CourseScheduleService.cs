using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>"8:00 AM" and the other ways a time of the day is written and read.</summary>
public static class ClassTime
{
    public static string Format(TimeOnly time) => time.ToString("h:mm tt", CultureInfo.InvariantCulture);

    /// <summary>"7:00 AM", "12:00 MID", "1:30 PM": the part of the day follows the time (before noon AM, the hour of noon MID, after it PM).</summary>
    public static string Label(TimeOnly time)
    {
        var hour = time.Hour is 0 ? 12 : time.Hour > 12 ? time.Hour - 12 : time.Hour;
        var part = time.Hour < 12 ? "AM" : time.Hour == 12 ? "MID" : "PM";
        return $"{hour}:{time.Minute:00} {part}";
    }

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
public sealed record ScheduleSection(int SectionId, string Label, string Year, bool CurrentYear, int Courses, int Scheduled, int SchoolYearId = 0, int StrandId = 0, int GradeLevel = 0)
{
    public string Text => $"{Label} · {Year}";
}

/// <summary>One course of a section with its class time (when set), its term, its teacher (when it has one) and its room (when it has one).</summary>
public sealed record CourseTimeRow(int SubjectId, string Code, string Name, byte TermNumber, string TermName, string? TeacherName, MeetingInfo? Meeting, int? ScheduleId = null, int? RoomId = null, string? RoomName = null);

/// <summary>What the coordinator typed for one course: nothing at all clears its class time. The part of the day (AM, MID, PM) is not typed: it follows the time the class starts.</summary>
public sealed record CourseTimeInput(int SubjectId, int? ScheduleId, string? Days, TimeOnly? Start, TimeOnly? End, int? RoomId = null)
{
    /// <summary><see cref="RoomId"/> of 0: the class has no room. Null: the room it has stays (a class that is new has none).</summary>
    public const int NoRoom = 0;

    public bool IsEmpty => ScheduleId is null && string.IsNullOrEmpty(Days) && Start is null && End is null && RoomId is null;
}

/// <summary>What the list of sections shows on the head of each one: how many of its courses of the term are on the schedule (with a day, a time and a room), and the room or rooms they meet in.</summary>
public sealed record SectionTermSummary(int SectionId, int Total, int Placed, IReadOnlyList<string> Rooms)
{
    /// <summary>Every course of the term has its place.</summary>
    public bool IsDone => Total > 0 && Placed >= Total;

    /// <summary>"Room 101", "2 rooms" or "No room".</summary>
    public string RoomText => Rooms.Count switch { 0 => "No room", 1 => Rooms[0], _ => $"{Rooms.Count} rooms" };
}

/// <summary>A room with the number of classes that meet in it.</summary>
public sealed record RoomRow(int Id, string Name, int Classes);

/// <summary>A class on the schedule grid: which course of which section meets in the room, on which days and when.</summary>
public sealed record PlacedClass(int SectionId, int SubjectId, string SectionLabel, string Course, string Days, TimeOnly Start, TimeOnly End, int? RoomId, byte TermNumber, string? Teacher, string? RoomName = null)
{
    public string Key => $"{SectionId}-{SubjectId}";
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
                s.Id, s.GradeLevel, s.StrandId, Strand = s.Strand.Code, s.Name, s.SchoolYearId,
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
            .Select(x => new ScheduleSection(x.Section.Id, TeachingService.SectionLabel(x.Section.GradeLevel, x.Section.Strand, x.Section.Name), x.Section.Year, x.Running, x.Courses, x.Section.Scheduled, x.Section.SchoolYearId, x.Section.StrandId, x.Section.GradeLevel))
            .ToList();
    }

    /// <summary>
    /// For every section of a school year, how many of its courses of a term are on the schedule (a class with a day, a time and a room) out of how many it has, and its rooms.
    /// Three small queries for the whole list, so the head of every section can say "6 of 6" without opening it.
    /// </summary>
    public async Task<IReadOnlyDictionary<int, SectionTermSummary>> SummarizeTermAsync(int schoolYearId, int termNumber, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var sections = await db.Sections.AsNoTracking().Where(s => s.SchoolYearId == schoolYearId).Select(s => new { s.Id, s.StrandId, s.GradeLevel }).ToListAsync(ct);
        var curriculum = await db.StrandSubjects.AsNoTracking().Where(x => x.IsOffered).Select(x => new { x.StrandId, x.GradeLevel, x.SubjectId, x.TermNumber }).ToListAsync(ct);
        var classes = await db.CourseSchedules.AsNoTracking().Where(c => c.Section.SchoolYearId == schoolYearId)
            .Select(c => new { c.SectionId, c.SubjectId, c.RoomId, Room = c.Room != null ? c.Room.Name : null }).ToListAsync(ct);

        var result = new Dictionary<int, SectionTermSummary>();
        foreach (var s in sections)
        {
            // a course is in the term of its first appearance in the curriculum of the strand (as the list of the courses of a section says)
            var subjects = curriculum.Where(c => c.StrandId == s.StrandId && c.GradeLevel == s.GradeLevel)
                .GroupBy(c => c.SubjectId).Where(g => g.Min(x => x.TermNumber) == termNumber).Select(g => g.Key).ToHashSet();
            var placed = classes.Where(c => c.SectionId == s.Id && c.RoomId is not null && subjects.Contains(c.SubjectId)).ToList();
            result[s.Id] = new SectionTermSummary(s.Id, subjects.Count, placed.Select(c => c.SubjectId).Distinct().Count(),
                [.. placed.Select(c => c.Room!).Distinct().Order()]);
        }

        return result;
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

        var courses = await db.StrandSubjects.AsNoTracking().Where(x => x.StrandId == section.StrandId && x.GradeLevel == section.GradeLevel && x.IsOffered)
            .Select(x => new { x.SubjectId, x.Subject.Code, x.Subject.Name, x.TermNumber }).ToListAsync(ct);
        var termNames = await db.Terms.AsNoTracking().Where(t => t.SchoolYearId == section.SchoolYearId).ToDictionaryAsync(t => t.TermNumber, t => t.Name, ct);
        var teachers = await db.TeachingAssignments.AsNoTracking().Where(a => a.SectionId == sectionId)
            .Select(a => new { a.SubjectId, Name = a.Teacher.LastName + ", " + a.Teacher.FirstName }).ToListAsync(ct);
        var times = await db.CourseSchedules.AsNoTracking().Include(c => c.Schedule).Include(c => c.Room).Where(c => c.SectionId == sectionId).ToListAsync(ct);

        return courses
            .GroupBy(c => c.SubjectId).Select(g => g.OrderBy(x => x.TermNumber).First())
            .OrderBy(c => c.TermNumber).ThenBy(c => c.Name)
            .Select(c => new CourseTimeRow(
                c.SubjectId, c.Code, c.Name, c.TermNumber, termNames.GetValueOrDefault(c.TermNumber, $"Term {c.TermNumber}"),
                teachers.FirstOrDefault(t => t.SubjectId == c.SubjectId)?.Name,
                times.FirstOrDefault(t => t.SubjectId == c.SubjectId) is { } t ? new MeetingInfo(t.Days, t.StartTime, t.EndTime, t.Schedule.Code) : null,
                times.FirstOrDefault(t => t.SubjectId == c.SubjectId)?.ScheduleId,
                times.FirstOrDefault(t => t.SubjectId == c.SubjectId)?.RoomId, times.FirstOrDefault(t => t.SubjectId == c.SubjectId)?.Room?.Name))
            .ToList();
    }

    // ======================================================================= the rooms and the schedule grid

    public async Task<IReadOnlyList<RoomRow>> ListRoomsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Rooms.AsNoTracking().OrderBy(r => r.Name).Select(r => new RoomRow(r.Id, r.Name, db.CourseSchedules.Count(c => c.RoomId == r.Id))).ToListAsync(ct);
    }

    public async Task<ServiceResult> AddRoomAsync(int actingUserId, string? name, CancellationToken ct = default)
    {
        var clean = string.Join(' ', (name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (clean.Length is 0 or > 50)
        {
            return ServiceResult.Fail("Type the name of the room, like Room 101 (up to 50 characters).");
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsCoordinatorAsync(db, actingUserId, ct))
        {
            return ServiceResult.Fail(CoordinatorOnly);
        }

        if (await db.Rooms.AnyAsync(r => r.Name == clean, ct))
        {
            return ServiceResult.Fail($"There is already a room called {clean}.");
        }

        db.Rooms.Add(new Room { Name = clean });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Fail($"There is already a room called {clean}.");
        }

        return ServiceResult.Ok($"The room {clean} was added.");
    }

    /// <summary>A room gets another name (the classes in it stay in it).</summary>
    public async Task<ServiceResult> RenameRoomAsync(int actingUserId, int roomId, string? name, CancellationToken ct = default)
    {
        var clean = string.Join(' ', (name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (clean.Length is 0 or > 50)
        {
            return ServiceResult.Fail("Type the name of the room, like Room 101 (up to 50 characters).");
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsCoordinatorAsync(db, actingUserId, ct))
        {
            return ServiceResult.Fail(CoordinatorOnly);
        }

        var room = await db.Rooms.FirstOrDefaultAsync(r => r.Id == roomId, ct);
        if (room is null)
        {
            return ServiceResult.Fail("That room was removed.");
        }

        if (room.Name == clean)
        {
            return ServiceResult.Ok(null);
        }

        if (await db.Rooms.AnyAsync(r => r.Name == clean && r.Id != roomId, ct))
        {
            return ServiceResult.Fail($"There is already a room called {clean}.");
        }

        room.Name = clean;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Fail($"There is already a room called {clean}.");
        }

        return ServiceResult.Ok($"The room is now called {clean}.");
    }

    /// <summary>A room is removed; its classes keep their days and times and have no room (they show under "No room" and can be put in another room).</summary>
    public async Task<ServiceResult> RemoveRoomAsync(int actingUserId, int roomId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsCoordinatorAsync(db, actingUserId, ct))
        {
            return ServiceResult.Fail(CoordinatorOnly);
        }

        var room = await db.Rooms.AsNoTracking().Where(r => r.Id == roomId).Select(r => new { r.Name, Classes = db.CourseSchedules.Count(c => c.RoomId == roomId) }).FirstOrDefaultAsync(ct);
        if (room is null)
        {
            return ServiceResult.Fail("That room was already removed.");
        }

        await db.CourseSchedules.Where(c => c.RoomId == roomId).ExecuteUpdateAsync(set => set.SetProperty(c => c.RoomId, (int?)null), ct);
        await db.Rooms.Where(r => r.Id == roomId).ExecuteDeleteAsync(ct);
        return ServiceResult.Ok(room.Classes == 0 ? $"The room {room.Name} was removed." : $"The room {room.Name} was removed. Its {room.Classes} {(room.Classes == 1 ? "class has" : "classes have")} no room now.");
    }

    /// <summary>
    /// The classes of one room in one school year (room 0: the classes that have no room; a negative number: every room), for the schedule grid. A term number narrows it to the courses of that term (0: every term).
    /// </summary>
    public async Task<IReadOnlyList<PlacedClass>> ListPlacedAsync(int roomId, int schoolYearId, int termNumber = 0, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await db.CourseSchedules.AsNoTracking()
            .Where(c => c.Section.SchoolYearId == schoolYearId && (roomId < 0 || (roomId == 0 ? c.RoomId == null : c.RoomId == roomId)))
            .Select(c => new
            {
                c.SectionId, c.SubjectId, Subject = c.Subject.Name, c.Section.GradeLevel, Strand = c.Section.Strand.Code, c.Section.StrandId, SectionName = c.Section.Name,
                c.Days, c.StartTime, c.EndTime, c.RoomId, RoomName = c.Room != null ? c.Room.Name : null,
                Teacher = db.TeachingAssignments.Where(a => a.SectionId == c.SectionId && a.SubjectId == c.SubjectId).Select(a => a.Teacher.LastName + ", " + a.Teacher.FirstName).FirstOrDefault(),
            })
            .ToListAsync(ct);
        var curriculum = await db.StrandSubjects.AsNoTracking().Select(x => new { x.StrandId, x.GradeLevel, x.SubjectId, x.TermNumber }).ToListAsync(ct);

        return rows
            .Select(c => new PlacedClass(
                c.SectionId, c.SubjectId, TeachingService.SectionLabel(c.GradeLevel, c.Strand, c.SectionName), c.Subject, c.Days, c.StartTime, c.EndTime, c.RoomId,
                curriculum.Where(x => x.StrandId == c.StrandId && x.GradeLevel == c.GradeLevel && x.SubjectId == c.SubjectId).Select(x => (byte?)x.TermNumber).Min() ?? 0, c.Teacher, c.RoomName))
            .Where(c => termNumber == 0 || c.TermNumber == termNumber)
            .OrderBy(c => c.Start).ThenBy(c => c.SectionLabel)
            .ToList();
    }

    // ======================================================================= setting and clearing the class times

    /// <summary>The class time of one course (the other courses of the section are left as they are).</summary>
    public Task<ServiceResult> SaveAsync(int actingUserId, int sectionId, int subjectId, int? scheduleId, string? days, TimeOnly? start, TimeOnly? end, CancellationToken ct = default) =>
        SaveSectionAsync(actingUserId, sectionId, [new CourseTimeInput(subjectId, scheduleId, days, start, end)], ct);

    /// <summary>Puts a course on the schedule grid (or moves it): the days, the time and the room (null for no room).</summary>
    public Task<ServiceResult> PlaceAsync(int actingUserId, int sectionId, int subjectId, int? roomId, string days, TimeOnly start, TimeOnly end, CancellationToken ct = default) =>
        SaveSectionAsync(actingUserId, sectionId, [new CourseTimeInput(subjectId, null, days, start, end, roomId ?? CourseTimeInput.NoRoom)], ct);

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

            var problem = Check(row);
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

        // ---- a room can't hold two classes at the same time
        foreach (var p in plan.Where(p => p.Input is not null && EffectiveRoom(p.Input, p.Old) is not null))
        {
            var roomId = EffectiveRoom(p.Input!, p.Old)!.Value;
            if (!await db.Rooms.AnyAsync(r => r.Id == roomId, ct))
            {
                errors.Add($"{p.Name}: that room no longer exists.");
            }
            else if (await RoomConflictAsync(db, roomId, sectionId, p.SubjectId, p.Input!.Days!, p.Input.Start!.Value, p.Input.End!.Value, ct) is { } roomProblem)
            {
                errors.Add($"{p.Name}: {roomProblem}");
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

            var partId = PartFor(parts.Values, p.Input.Start!.Value)?.Id ?? p.Input.ScheduleId ?? parts.Keys.First();
            if (p.Old is null)
            {
                db.CourseSchedules.Add(new CourseSchedule { SectionId = sectionId, SubjectId = p.SubjectId, ScheduleId = partId, Days = p.Input.Days!, StartTime = p.Input.Start!.Value, EndTime = p.Input.End!.Value, RoomId = EffectiveRoom(p.Input, null) });
                set++;
            }
            else if (p.Old.ScheduleId != partId || p.Old.Days != p.Input.Days || p.Old.StartTime != p.Input.Start || p.Old.EndTime != p.Input.End || p.Old.RoomId != EffectiveRoom(p.Input, p.Old))
            {
                p.Old.ScheduleId = partId;
                p.Old.Days = p.Input.Days!;
                p.Old.StartTime = p.Input.Start!.Value;
                p.Old.EndTime = p.Input.End!.Value;
                p.Old.RoomId = EffectiveRoom(p.Input, p.Old);
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

        var onlyOne = plan.Count == 1 && plan[0].Input is { } one ? $"{plan[0].Name} of {label} meets {new MeetingInfo(one.Days!, one.Start!.Value, one.End!.Value, PartFor(parts.Values, one.Start!.Value)?.Code).DescribeFull()}." : null;
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

    /// <summary>The room a class ends up in: the one typed, none when <see cref="CourseTimeInput.NoRoom"/> was typed, and the one it has when nothing was typed.</summary>
    private static int? EffectiveRoom(CourseTimeInput input, CourseSchedule? old) =>
        input.RoomId is null ? old?.RoomId : input.RoomId == CourseTimeInput.NoRoom ? null : input.RoomId;

    /// <summary>
    /// The part of the day (AM, MID or PM) of a class: it follows the time the class starts, not a choice and not the hours kept for each part: before noon is AM, the hour of noon is MID,
    /// from 1:00 PM it is PM. (The rows of the schedules only give the part its number.)
    /// </summary>
    public static Schedule? PartFor(IEnumerable<Schedule> parts, TimeOnly start)
    {
        var list = parts.ToList();
        var code = start.Hour < 12 ? "AM" : start.Hour == 12 ? "MID" : "PM";
        return list.FirstOrDefault(p => string.Equals(p.Code, code, StringComparison.OrdinalIgnoreCase)) ?? list.OrderBy(p => p.StartTime).FirstOrDefault();
    }

    /// <summary>What is wrong with one course's class time by itself (the days, the hours), or null.</summary>
    private static string? Check(CourseTimeInput row)
    {
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

        if (start < EarliestHour || end > LatestHour)
        {
            return $"a class is between {ClassTime.Format(EarliestHour)} and {ClassTime.Format(LatestHour)}.";
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

    /// <summary>A sentence that says why the room can't hold this class at this time (another class of the same school year and term is there then), or null when it is free.</summary>
    internal static async Task<string?> RoomConflictAsync(AppDbContext db, int roomId, int sectionId, int subjectId, string days, TimeOnly start, TimeOnly end, CancellationToken ct)
    {
        var mine = await db.Sections.AsNoTracking().Where(s => s.Id == sectionId).Select(s => new { s.StrandId, s.GradeLevel, s.SchoolYearId }).FirstOrDefaultAsync(ct);
        if (mine is null)
        {
            return null;
        }

        var myTerm = await TermOfAsync(db, mine.StrandId, mine.GradeLevel, subjectId, ct);
        var others = await db.CourseSchedules.AsNoTracking()
            .Where(c => c.RoomId == roomId && c.Days == days && !(c.SectionId == sectionId && c.SubjectId == subjectId) && c.Section.SchoolYearId == mine.SchoolYearId)
            .Select(c => new { c.SubjectId, Subject = c.Subject.Name, c.StartTime, c.EndTime, c.Section.StrandId, c.Section.GradeLevel, Strand = c.Section.Strand.Code, SectionName = c.Section.Name, Room = c.Room!.Name })
            .ToListAsync(ct);

        foreach (var other in others.Where(o => Overlaps(start, end, o.StartTime, o.EndTime)))
        {
            if (myTerm is not null && await TermOfAsync(db, other.StrandId, other.GradeLevel, other.SubjectId, ct) != myTerm)
            {
                continue;
            }

            return $"{other.Room} is already used by {other.Subject} of {TeachingService.SectionLabel(other.GradeLevel, other.Strand, other.SectionName)} on {days} {ClassTime.Format(other.StartTime)} – {ClassTime.Format(other.EndTime)}. Two classes can't be in one room at the same time.";
        }

        return null;
    }

    private static bool Overlaps(TimeOnly start, TimeOnly end, TimeOnly otherStart, TimeOnly otherEnd) => start < otherEnd && otherStart < end;

    /// <summary>The term (1, 2, ...) in which the strand takes the course at the grade level, or null when it is not one of its courses.</summary>
    private static async Task<byte?> TermOfAsync(AppDbContext db, int strandId, byte gradeLevel, int subjectId, CancellationToken ct) =>
        await db.StrandSubjects.AsNoTracking().Where(x => x.StrandId == strandId && x.GradeLevel == gradeLevel && x.SubjectId == subjectId)
            .OrderBy(x => x.TermNumber).Select(x => (byte?)x.TermNumber).FirstOrDefaultAsync(ct);
}
