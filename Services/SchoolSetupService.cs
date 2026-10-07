using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Components.Shared;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;
using SEAITT_LMS.Data.Queries;

namespace SEAITT_LMS.Services;

// ---- what the lists show
public sealed record SchoolYearRow(int Id, string Name, DateOnly StartDate, DateOnly EndDate, int Terms, int Sections);

public sealed record TermRow(int Id, int SchoolYearId, string SchoolYear, int TermNumber, string Name, DateOnly StartDate, DateOnly EndDate, bool IsCurrent, int Enrollments);

public sealed record TrackRow(int Id, string Code, string Name, bool IsActive, int Strands);

public sealed record StrandRow(
    int Id, string Code, string Name, int TrackId, string Track, bool IsActive, IReadOnlyList<int> ScheduleIds, IReadOnlyList<string> Schedules,
    int Subjects, int Sections);

public sealed record SubjectRow(int Id, string Code, string Name, string? Description, bool IsActive, int Strands);

public sealed record SectionRow(
    int Id, int SchoolYearId, string SchoolYear, int StrandId, string StrandCode, int GradeLevel, int? ScheduleId, string? Schedule, string Name,
    int? AdviserTeacherId, string? Adviser, int Students, int Teachers)
{
    public string Label => TeachingService.SectionLabel((byte)GradeLevel, StrandCode, Name);
}

/// <summary>One subject of a strand's curriculum: the strand takes it in that term of that grade level.</summary>
public sealed record CurriculumEntry(int GradeLevel, int TermNumber, int SubjectId, string SubjectName, string SubjectCode);

// ---- the choices the forms offer
public sealed record YearOption(int Id, string Name, DateOnly StartDate, DateOnly EndDate);
public sealed record StrandChoice(int Id, string Code, string Name, string Track, IReadOnlyList<int> ScheduleIds);
public sealed record ScheduleChoice(int Id, string Code, string Name);
public sealed record TeacherChoice(int Id, string Name);
public sealed record SubjectPick(int Id, string Code, string Name, bool IsActive);

public sealed record SetupLookups(
    IReadOnlyList<YearOption> Years,
    IReadOnlyList<TrackRow> Tracks,
    IReadOnlyList<StrandChoice> Strands,
    IReadOnlyList<ScheduleChoice> Schedules,
    IReadOnlyList<TeacherChoice> Teachers,
    IReadOnlyList<SubjectPick> Subjects,
    IReadOnlyList<int> TermNumbers);

/// <summary>How many rows each tab of the school setup has (the numbers on the tabs).</summary>
public sealed record SetupCounts(int SchoolYears, int Terms, int Tracks, int Strands, int Subjects, int Sections, int Enrollments, int NotEnrolled);

// ---- what the forms send. An Id of null means "add".
public sealed class SchoolYearInput
{
    public int? Id { get; set; }
    public string Name { get; set; } = "";
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
}

public sealed class TermInput
{
    public int? Id { get; set; }
    public int SchoolYearId { get; set; }
    public int TermNumber { get; set; } = 1;
    public string Name { get; set; } = "";
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsCurrent { get; set; }
}

public sealed class TrackInput
{
    public int? Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public sealed class StrandInput
{
    public int? Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int TrackId { get; set; }
    public bool IsActive { get; set; } = true;
    public HashSet<int> ScheduleIds { get; set; } = [];
}

public sealed class SubjectInput
{
    public int? Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class SectionInput
{
    public int? Id { get; set; }
    public int SchoolYearId { get; set; }
    public int StrandId { get; set; }
    public int GradeLevel { get; set; } = 11;
    public int ScheduleId { get; set; }
    public string Name { get; set; } = "";
    public int? AdviserTeacherId { get; set; }
}

/// <summary>
/// What an administrator edits in the school setup: school years, terms, tracks, strands (with their schedules and their
/// curriculum), subjects and sections. Enrollments and the list of students who are not enrolled are the registrar's work: the
/// administrator can look at them (see <see cref="EnrollmentList"/> and <see cref="NotEnrolledList"/>) but not change them here.
/// Every change checks again, in the database, that the person doing it is an active administrator, and refuses what would leave
/// the records inconsistent (a strand with students can't move to another track, a section with students can't move to another strand, ...).
/// </summary>
public sealed partial class SchoolSetupService(IDbContextFactory<AppDbContext> dbFactory)
{
    [GeneratedRegex(@"^[\p{L}\p{N}][\p{L}\p{N} \-]*$")]
    private static partial Regex SectionNamePattern();

    private const string AdminOnly = "Only an administrator can change the school setup.";

    // ======================================================================= counts and choices

    public async Task<SetupCounts> GetCountsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var (_, notEnrolled) = await NotEnrolledList.CountAsync(db, ct);
        return new SetupCounts(
            await db.SchoolYears.CountAsync(ct), await db.Terms.CountAsync(ct), await db.Tracks.CountAsync(ct), await db.Strands.CountAsync(ct),
            await db.Subjects.CountAsync(ct), await db.Sections.CountAsync(ct), await db.Enrollments.CountAsync(ct), notEnrolled);
    }

    public async Task<SetupLookups> GetLookupsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var years = await db.SchoolYears.AsNoTracking().OrderByDescending(y => y.StartDate)
            .Select(y => new YearOption(y.Id, y.Name, y.StartDate, y.EndDate)).ToListAsync(ct);
        var tracks = await db.Tracks.AsNoTracking().OrderBy(t => t.Id)
            .Select(t => new TrackRow(t.Id, t.Code, t.Name, t.IsActive, 0)).ToListAsync(ct);
        var schedules = await db.Schedules.AsNoTracking().OrderBy(s => s.Id)
            .Select(s => new ScheduleChoice(s.Id, s.Code, s.Name)).ToListAsync(ct);
        var links = await db.StrandSchedules.AsNoTracking().Select(x => new { x.StrandId, x.ScheduleId }).ToListAsync(ct);
        var strands = (await db.Strands.AsNoTracking().OrderBy(s => s.Track.Id).ThenBy(s => s.Id)
                .Select(s => new { s.Id, s.Code, s.Name, Track = s.Track.Code }).ToListAsync(ct))
            .Select(s => new StrandChoice(s.Id, s.Code, s.Name, s.Track, links.Where(l => l.StrandId == s.Id).Select(l => l.ScheduleId).Order().ToList()))
            .ToList();
        var teachers = (await db.Teachers.AsNoTracking().OrderBy(t => t.LastName).ThenBy(t => t.FirstName)
                .Select(t => new { t.Id, t.FirstName, t.MiddleName, t.LastName }).ToListAsync(ct))
            .Select(t => new TeacherChoice(t.Id, $"{t.LastName}, {t.FirstName}")).ToList();
        var subjects = await db.Subjects.AsNoTracking().OrderBy(s => s.Name)
            .Select(s => new SubjectPick(s.Id, s.Code, s.Name, s.IsActive)).ToListAsync(ct);

        // 1st term, 2nd term ... as many as the terms of any school year, and at least two.
        var maxTerm = Math.Max(2, (int)(await db.Terms.AsNoTracking().Select(t => (int?)t.TermNumber).MaxAsync(ct) ?? 2));
        return new SetupLookups(years, tracks, strands, schedules, teachers, subjects, Enumerable.Range(1, maxTerm).ToList());
    }

    // ======================================================================= school years

    public async Task<IReadOnlyList<SchoolYearRow>> ListSchoolYearsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.SchoolYears.AsNoTracking().OrderByDescending(y => y.StartDate)
            .Select(y => new SchoolYearRow(y.Id, y.Name, y.StartDate, y.EndDate, y.Terms.Count, db.Sections.Count(s => s.SchoolYearId == y.Id)))
            .ToListAsync(ct);
    }

    public async Task<ServiceResult> SaveSchoolYearAsync(SchoolYearInput input, int actingUserId, CancellationToken ct = default)
    {
        var errors = new List<string>();
        var name = Clean(input.Name);
        if (name.Length is 0 or > 20)
        {
            errors.Add("Enter the name of the school year, like 2027-2028 (up to 20 characters).");
        }

        if (input.StartDate is null || input.EndDate is null)
        {
            errors.Add("Pick the first and the last day of the school year.");
        }
        else if (input.EndDate <= input.StartDate)
        {
            errors.Add("The school year must end after it starts.");
        }

        if (errors.Count > 0)
        {
            return ServiceResult.Fail(errors);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsAdminAsync(db, actingUserId, ct))
        {
            return ServiceResult.Fail(AdminOnly);
        }

        if (await db.SchoolYears.AnyAsync(y => y.Name == name && y.Id != input.Id, ct))
        {
            return ServiceResult.Fail($"There is already a school year called {name}.");
        }

        SchoolYear year;
        if (input.Id is { } id)
        {
            var found = await db.SchoolYears.FirstOrDefaultAsync(y => y.Id == id, ct);
            if (found is null)
            {
                return ServiceResult.Fail("This school year no longer exists.");
            }

            year = found;

            // The terms of the year have to stay inside it.
            var outside = await db.Terms.AnyAsync(t => t.SchoolYearId == id && (t.StartDate < input.StartDate!.Value || t.EndDate > input.EndDate!.Value), ct);
            if (outside)
            {
                return ServiceResult.Fail("A term of this school year would be outside it. Change the term first, or pick wider dates.");
            }
        }
        else
        {
            year = new SchoolYear();
            db.SchoolYears.Add(year);
        }

        year.Name = name;
        year.StartDate = input.StartDate!.Value;
        year.EndDate = input.EndDate!.Value;
        return await SaveAsync(db, input.Id is null ? $"The school year {name} was added." : $"The school year {name} was saved.", ct);
    }

    public async Task<ServiceResult> DeleteSchoolYearAsync(int id, int actingUserId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsAdminAsync(db, actingUserId, ct))
        {
            return ServiceResult.Fail(AdminOnly);
        }

        var year = await db.SchoolYears.Where(y => y.Id == id).Select(y => new { y.Name, Terms = y.Terms.Count, Sections = db.Sections.Count(s => s.SchoolYearId == id) }).FirstOrDefaultAsync(ct);
        if (year is null)
        {
            return ServiceResult.Fail("This school year was already deleted.");
        }

        if (year.Terms > 0 || year.Sections > 0)
        {
            return ServiceResult.Fail($"{year.Name} has {Plural(year.Terms, "term")} and {Plural(year.Sections, "section")}. Delete those first.");
        }

        await db.SchoolYears.Where(y => y.Id == id).ExecuteDeleteAsync(ct);
        return ServiceResult.Ok($"The school year {year.Name} was deleted.");
    }

    // ======================================================================= terms

    public async Task<IReadOnlyList<TermRow>> ListTermsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Terms.AsNoTracking().OrderByDescending(t => t.SchoolYear.StartDate).ThenBy(t => t.TermNumber)
            .Select(t => new TermRow(t.Id, t.SchoolYearId, t.SchoolYear.Name, t.TermNumber, t.Name, t.StartDate, t.EndDate, t.IsCurrent,
                db.Enrollments.Count(e => e.TermId == t.Id)))
            .ToListAsync(ct);
    }

    public async Task<ServiceResult> SaveTermAsync(TermInput input, int actingUserId, CancellationToken ct = default)
    {
        var errors = new List<string>();
        var name = Clean(input.Name);
        if (name.Length is 0 or > 50)
        {
            errors.Add("Enter the name of the term, like 3rd Term (up to 50 characters).");
        }

        if (input.TermNumber is < 1 or > 9)
        {
            errors.Add("The term number must be from 1 to 9.");
        }

        if (input.StartDate is null || input.EndDate is null)
        {
            errors.Add("Pick the first and the last day of the term.");
        }
        else if (input.EndDate < input.StartDate)
        {
            errors.Add("The term can't end before it starts.");
        }

        if (errors.Count > 0)
        {
            return ServiceResult.Fail(errors);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsAdminAsync(db, actingUserId, ct))
        {
            return ServiceResult.Fail(AdminOnly);
        }

        var year = await db.SchoolYears.AsNoTracking().FirstOrDefaultAsync(y => y.Id == input.SchoolYearId, ct);
        if (year is null)
        {
            return ServiceResult.Fail("Pick the school year of the term.");
        }

        if (input.StartDate < year.StartDate || input.EndDate > year.EndDate)
        {
            return ServiceResult.Fail($"The term has to be inside the school year ({PhilippineTime.FormatDate(year.StartDate)} to {PhilippineTime.FormatDate(year.EndDate)}).");
        }

        if (await db.Terms.AnyAsync(t => t.SchoolYearId == year.Id && t.TermNumber == input.TermNumber && t.Id != input.Id, ct))
        {
            return ServiceResult.Fail($"{year.Name} already has term number {input.TermNumber}.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        int termId;
        if (input.Id is { } id)
        {
            var found = await db.Terms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
            if (found is null)
            {
                return ServiceResult.Fail("This term no longer exists.");
            }

            // Enrollments are tied to the school year and the number of the term (the subjects came from it).
            if ((found.SchoolYearId != year.Id || found.TermNumber != input.TermNumber) && await db.Enrollments.AnyAsync(e => e.TermId == id, ct))
            {
                return ServiceResult.Fail("Students are enrolled in this term, so its school year and its number can't change.");
            }

            // The school year is part of a key that enrollments use, so the row is updated directly (not through a tracked entity).
            var updated = await TryAsync(() => db.Terms.Where(t => t.Id == id).ExecuteUpdateAsync(set => set
                .SetProperty(t => t.SchoolYearId, year.Id)
                .SetProperty(t => t.TermNumber, (byte)input.TermNumber)
                .SetProperty(t => t.Name, name)
                .SetProperty(t => t.StartDate, input.StartDate!.Value)
                .SetProperty(t => t.EndDate, input.EndDate!.Value)
                .SetProperty(t => t.IsCurrent, input.IsCurrent)
                .SetProperty(t => t.UpdatedAtUtc, DateTime.UtcNow), ct));
            if (updated is null)
            {
                return ServiceResult.Fail(SaveFailed);
            }

            termId = id;
        }
        else
        {
            var term = new Term
            {
                SchoolYearId = year.Id, TermNumber = (byte)input.TermNumber, Name = name, StartDate = input.StartDate!.Value, EndDate = input.EndDate!.Value, IsCurrent = input.IsCurrent,
            };
            db.Terms.Add(term);
            var saved = await SaveAsync(db, "", ct);
            if (!saved.Succeeded)
            {
                return saved;
            }

            termId = term.Id;
        }

        // Only one term is the current one.
        if (input.IsCurrent)
        {
            await db.Terms.Where(t => t.Id != termId && t.IsCurrent).ExecuteUpdateAsync(set => set.SetProperty(t => t.IsCurrent, false), ct);
        }

        await transaction.CommitAsync(ct);
        return ServiceResult.Ok(input.Id is null ? $"The term {name} of {year.Name} was added." : $"The term {name} of {year.Name} was saved.");
    }

    public async Task<ServiceResult> DeleteTermAsync(int id, int actingUserId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsAdminAsync(db, actingUserId, ct))
        {
            return ServiceResult.Fail(AdminOnly);
        }

        var term = await db.Terms.Where(t => t.Id == id).Select(t => new { t.Name, Year = t.SchoolYear.Name, t.IsCurrent, Enrollments = db.Enrollments.Count(e => e.TermId == id) }).FirstOrDefaultAsync(ct);
        if (term is null)
        {
            return ServiceResult.Fail("This term was already deleted.");
        }

        if (term.Enrollments > 0)
        {
            return ServiceResult.Fail($"{Plural(term.Enrollments, "student")} {(term.Enrollments == 1 ? "is" : "are")} enrolled in {term.Name} of {term.Year}. A term with enrollments can't be deleted.");
        }

        await db.Terms.Where(t => t.Id == id).ExecuteDeleteAsync(ct);
        return ServiceResult.Ok($"The term {term.Name} of {term.Year} was deleted.");
    }

    // ======================================================================= tracks

    public async Task<IReadOnlyList<TrackRow>> ListTracksAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Tracks.AsNoTracking().OrderBy(t => t.Id)
            .Select(t => new TrackRow(t.Id, t.Code, t.Name, t.IsActive, db.Strands.Count(s => s.TrackId == t.Id)))
            .ToListAsync(ct);
    }

    public async Task<ServiceResult> SaveTrackAsync(TrackInput input, int actingUserId, CancellationToken ct = default)
    {
        var errors = new List<string>();
        var code = Clean(input.Code).ToUpperInvariant();
        var name = Clean(input.Name);
        if (code.Length is 0 or > 20)
        {
            errors.Add("Enter the code of the track, like ACAD (up to 20 characters).");
        }

        if (name.Length is 0 or > 100)
        {
            errors.Add("Enter the name of the track (up to 100 characters).");
        }

        if (errors.Count > 0)
        {
            return ServiceResult.Fail(errors);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsAdminAsync(db, actingUserId, ct))
        {
            return ServiceResult.Fail(AdminOnly);
        }

        if (await db.Tracks.AnyAsync(t => t.Code == code && t.Id != input.Id, ct))
        {
            return ServiceResult.Fail($"There is already a track with the code {code}.");
        }

        if (await db.Tracks.AnyAsync(t => t.Name == name && t.Id != input.Id, ct))
        {
            return ServiceResult.Fail($"There is already a track called {name}.");
        }

        Track track;
        if (input.Id is { } id)
        {
            var found = await db.Tracks.FirstOrDefaultAsync(t => t.Id == id, ct);
            if (found is null)
            {
                return ServiceResult.Fail("This track no longer exists.");
            }

            track = found;
        }
        else
        {
            track = new Track();
            db.Tracks.Add(track);
        }

        track.Code = code;
        track.Name = name;
        track.IsActive = input.IsActive;
        return await SaveAsync(db, input.Id is null ? $"The track {code} was added." : $"The track {code} was saved.", ct);
    }

    public async Task<ServiceResult> DeleteTrackAsync(int id, int actingUserId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsAdminAsync(db, actingUserId, ct))
        {
            return ServiceResult.Fail(AdminOnly);
        }

        var track = await db.Tracks.Where(t => t.Id == id).Select(t => new { t.Code, Strands = db.Strands.Count(s => s.TrackId == id) }).FirstOrDefaultAsync(ct);
        if (track is null)
        {
            return ServiceResult.Fail("This track was already deleted.");
        }

        if (track.Strands > 0)
        {
            return ServiceResult.Fail($"{track.Code} has {Plural(track.Strands, "strand")}. Delete or move those first, or switch the track off instead.");
        }

        await db.Tracks.Where(t => t.Id == id).ExecuteDeleteAsync(ct);
        return ServiceResult.Ok($"The track {track.Code} was deleted.");
    }

    // ======================================================================= strands

    public async Task<IReadOnlyList<StrandRow>> ListStrandsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var links = await db.StrandSchedules.AsNoTracking().Select(x => new { x.StrandId, x.ScheduleId, x.Schedule.Code }).ToListAsync(ct);
        var rows = await db.Strands.AsNoTracking().OrderBy(s => s.TrackId).ThenBy(s => s.Id)
            .Select(s => new
            {
                s.Id, s.Code, s.Name, s.TrackId, Track = s.Track.Code, s.IsActive,
                Subjects = db.StrandSubjects.Count(x => x.StrandId == s.Id),
                Sections = db.Sections.Count(x => x.StrandId == s.Id),
            })
            .ToListAsync(ct);
        return rows.Select(s => new StrandRow(
            s.Id, s.Code, s.Name, s.TrackId, s.Track, s.IsActive,
            links.Where(l => l.StrandId == s.Id).Select(l => l.ScheduleId).Order().ToList(),
            links.Where(l => l.StrandId == s.Id).OrderBy(l => l.ScheduleId).Select(l => l.Code).ToList(),
            s.Subjects, s.Sections)).ToList();
    }

    public async Task<ServiceResult> SaveStrandAsync(StrandInput input, int actingUserId, CancellationToken ct = default)
    {
        var errors = new List<string>();
        var code = Clean(input.Code).ToUpperInvariant();
        var name = Clean(input.Name);
        if (code.Length is 0 or > 20)
        {
            errors.Add("Enter the code of the strand, like BUSSENT (up to 20 characters).");
        }

        if (name.Length is 0 or > 150)
        {
            errors.Add("Enter the name of the strand (up to 150 characters).");
        }

        if (input.ScheduleIds.Count == 0)
        {
            errors.Add("Pick at least one schedule the strand has classes in.");
        }

        if (errors.Count > 0)
        {
            return ServiceResult.Fail(errors);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsAdminAsync(db, actingUserId, ct))
        {
            return ServiceResult.Fail(AdminOnly);
        }

        if (!await db.Tracks.AnyAsync(t => t.Id == input.TrackId, ct))
        {
            return ServiceResult.Fail("Pick the track of the strand.");
        }

        var wanted = input.ScheduleIds.ToList();
        if (await db.Schedules.CountAsync(s => wanted.Contains(s.Id), ct) != wanted.Count)
        {
            return ServiceResult.Fail("One of the schedules does not exist.");
        }

        if (await db.Strands.AnyAsync(s => s.Code == code && s.Id != input.Id, ct))
        {
            return ServiceResult.Fail($"There is already a strand with the code {code}.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        int strandId;
        if (input.Id is { } id)
        {
            var found = await db.Strands.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
            if (found is null)
            {
                return ServiceResult.Fail("This strand no longer exists.");
            }

            // Students, teachers and enrollments keep the strand together with its track.
            if (found.TrackId != input.TrackId
                && (await db.Enrollments.AnyAsync(e => e.StrandId == id, ct) || await db.Students.AnyAsync(s => s.StrandId == id, ct) || await db.Teachers.AnyAsync(t => t.StrandId == id, ct)))
            {
                return ServiceResult.Fail("Students or teachers belong to this strand, so it can't move to another track.");
            }

            // A section runs in one of the strand's schedules, so a schedule in use can't be taken away.
            var dropped = await db.StrandSchedules.Where(x => x.StrandId == id && !wanted.Contains(x.ScheduleId)).Select(x => x.ScheduleId).ToListAsync(ct);
            if (dropped.Count > 0)
            {
                var inUse = await db.Sections.Where(s => s.StrandId == id && s.ScheduleId != null && dropped.Contains(s.ScheduleId.Value))
                    .Select(s => new { s.GradeLevel, s.Name, Schedule = s.Schedule!.Code }).FirstOrDefaultAsync(ct);
                if (inUse is not null)
                {
                    return ServiceResult.Fail($"Section {inUse.GradeLevel}-{code}-{inUse.Name} meets in the {inUse.Schedule} schedule, so {code} must keep it.");
                }

                await db.StrandSchedules.Where(x => x.StrandId == id && dropped.Contains(x.ScheduleId)).ExecuteDeleteAsync(ct);
            }

            // The track is part of a key that enrollments use, so the row is updated directly (not through a tracked entity).
            var updated = await TryAsync(() => db.Strands.Where(s => s.Id == id).ExecuteUpdateAsync(set => set
                .SetProperty(s => s.Code, code)
                .SetProperty(s => s.Name, name)
                .SetProperty(s => s.TrackId, input.TrackId)
                .SetProperty(s => s.IsActive, input.IsActive)
                .SetProperty(s => s.UpdatedAtUtc, DateTime.UtcNow), ct));
            if (updated is null)
            {
                return ServiceResult.Fail(SaveFailed);
            }

            strandId = id;
        }
        else
        {
            var strand = new Strand { Code = code, Name = name, TrackId = input.TrackId, IsActive = input.IsActive };
            db.Strands.Add(strand);
            var saved = await SaveAsync(db, "", ct);
            if (!saved.Succeeded)
            {
                return saved;
            }

            strandId = strand.Id;
        }

        var have = await db.StrandSchedules.Where(x => x.StrandId == strandId).Select(x => x.ScheduleId).ToListAsync(ct);
        foreach (var scheduleId in wanted.Except(have))
        {
            db.StrandSchedules.Add(new StrandSchedule { StrandId = strandId, ScheduleId = scheduleId });
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ServiceResult.Ok(input.Id is null ? $"The strand {code} was added. Give it its courses with the Courses button." : $"The strand {code} was saved.", strandId);
    }

    public async Task<ServiceResult> DeleteStrandAsync(int id, int actingUserId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsAdminAsync(db, actingUserId, ct))
        {
            return ServiceResult.Fail(AdminOnly);
        }

        var strand = await db.Strands.Where(s => s.Id == id).Select(s => new
        {
            s.Code,
            Sections = db.Sections.Count(x => x.StrandId == id),
            Enrollments = db.Enrollments.Count(x => x.StrandId == id),
            People = db.Students.Count(x => x.StrandId == id) + db.Teachers.Count(x => x.StrandId == id),
        }).FirstOrDefaultAsync(ct);
        if (strand is null)
        {
            return ServiceResult.Fail("This strand was already deleted.");
        }

        if (strand.Sections > 0 || strand.Enrollments > 0 || strand.People > 0)
        {
            return ServiceResult.Fail($"{strand.Code} has sections or students. A strand that is or was in use can't be deleted: switch it off instead.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.StrandSubjects.Where(x => x.StrandId == id).ExecuteDeleteAsync(ct);
        await db.StrandSchedules.Where(x => x.StrandId == id).ExecuteDeleteAsync(ct);
        await db.Strands.Where(s => s.Id == id).ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct);
        return ServiceResult.Ok($"The strand {strand.Code} was deleted.");
    }

    // ----------------------------------------------------------------------- the curriculum of a strand

    public async Task<IReadOnlyList<CurriculumEntry>> GetCurriculumAsync(int strandId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.StrandSubjects.AsNoTracking().Where(x => x.StrandId == strandId)
            .OrderBy(x => x.GradeLevel).ThenBy(x => x.TermNumber).ThenBy(x => x.Subject.Name)
            .Select(x => new CurriculumEntry(x.GradeLevel, x.TermNumber, x.SubjectId, x.Subject.Name, x.Subject.Code))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Puts a subject in the strand's curriculum for a term of a grade level. It is what students enrolled from now on get; students who
    /// are already enrolled keep the subjects they have.
    /// </summary>
    public async Task<ServiceResult> AddCurriculumSubjectAsync(int strandId, int gradeLevel, int termNumber, int subjectId, int actingUserId, CancellationToken ct = default)
    {
        if (gradeLevel is not (11 or 12))
        {
            return ServiceResult.Fail("Pick grade level 11 or 12.");
        }

        if (termNumber is < 1 or > 9)
        {
            return ServiceResult.Fail("Pick the term.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsAdminAsync(db, actingUserId, ct))
        {
            return ServiceResult.Fail(AdminOnly);
        }

        var strand = await db.Strands.AsNoTracking().Where(s => s.Id == strandId).Select(s => s.Code).FirstOrDefaultAsync(ct);
        var subject = await db.Subjects.AsNoTracking().Where(s => s.Id == subjectId).Select(s => new { s.Name, s.IsActive }).FirstOrDefaultAsync(ct);
        if (strand is null || subject is null)
        {
            return ServiceResult.Fail("Pick a course.");
        }

        if (!subject.IsActive)
        {
            return ServiceResult.Fail($"{subject.Name} is switched off. Switch it on first.");
        }

        if (await db.StrandSubjects.AnyAsync(x => x.StrandId == strandId && x.GradeLevel == gradeLevel && x.TermNumber == termNumber && x.SubjectId == subjectId, ct))
        {
            return ServiceResult.Fail($"{subject.Name} is already one of the courses of {strand} in that term.");
        }

        db.StrandSubjects.Add(new StrandSubject { StrandId = strandId, GradeLevel = (byte)gradeLevel, TermNumber = (byte)termNumber, SubjectId = subjectId });
        return await SaveAsync(db, $"{subject.Name} was added to {strand}, grade {gradeLevel}, term {termNumber}.", ct);
    }

    public async Task<ServiceResult> RemoveCurriculumSubjectAsync(int strandId, int gradeLevel, int termNumber, int subjectId, int actingUserId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsAdminAsync(db, actingUserId, ct))
        {
            return ServiceResult.Fail(AdminOnly);
        }

        var removed = await db.StrandSubjects.Where(x => x.StrandId == strandId && x.GradeLevel == gradeLevel && x.TermNumber == termNumber && x.SubjectId == subjectId).ExecuteDeleteAsync(ct);
        return removed == 0 ? ServiceResult.Fail("This course was already removed.") : ServiceResult.Ok("The course was removed from the strand.");
    }

    // ======================================================================= subjects

    public async Task<IReadOnlyList<SubjectRow>> ListSubjectsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Subjects.AsNoTracking().OrderBy(s => s.Name)
            .Select(s => new SubjectRow(s.Id, s.Code, s.Name, s.Description, s.IsActive, db.StrandSubjects.Where(x => x.SubjectId == s.Id).Select(x => x.StrandId).Distinct().Count()))
            .ToListAsync(ct);
    }

    public async Task<ServiceResult> SaveSubjectAsync(SubjectInput input, int actingUserId, CancellationToken ct = default)
    {
        var errors = new List<string>();
        var code = Clean(input.Code).ToUpperInvariant();
        var name = Clean(input.Name);
        var description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        if (code.Length is 0 or > 30)
        {
            errors.Add("Enter the code of the course (up to 30 characters).");
        }

        if (name.Length is 0 or > 150)
        {
            errors.Add("Enter the name of the course (up to 150 characters).");
        }

        if (description is { Length: > 500 })
        {
            errors.Add("The description can have at most 500 characters.");
        }

        if (errors.Count > 0)
        {
            return ServiceResult.Fail(errors);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsAdminAsync(db, actingUserId, ct))
        {
            return ServiceResult.Fail(AdminOnly);
        }

        if (await db.Subjects.AnyAsync(s => s.Code == code && s.Id != input.Id, ct))
        {
            return ServiceResult.Fail($"There is already a course with the code {code}.");
        }

        Subject subject;
        if (input.Id is { } id)
        {
            var found = await db.Subjects.FirstOrDefaultAsync(s => s.Id == id, ct);
            if (found is null)
            {
                return ServiceResult.Fail("This course no longer exists.");
            }

            subject = found;
        }
        else
        {
            subject = new Subject();
            db.Subjects.Add(subject);
        }

        subject.Code = code;
        subject.Name = name;
        subject.Description = description;
        subject.IsActive = input.IsActive;
        return await SaveAsync(db, input.Id is null ? $"The course {name} was added. Put it in a strand's courses to use it." : $"The course {name} was saved.", ct);
    }

    public async Task<ServiceResult> DeleteSubjectAsync(int id, int actingUserId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsAdminAsync(db, actingUserId, ct))
        {
            return ServiceResult.Fail(AdminOnly);
        }

        var subject = await db.Subjects.Where(s => s.Id == id).Select(s => new
        {
            s.Name,
            Curriculum = db.StrandSubjects.Count(x => x.SubjectId == id),
            Students = db.EnrollmentSubjects.Count(x => x.SubjectId == id),
            Teaching = db.TeachingAssignments.Count(x => x.SubjectId == id),
            People = db.Students.Count(x => x.SubjectId == id) + db.Teachers.Count(x => x.SubjectId == id),
        }).FirstOrDefaultAsync(ct);
        if (subject is null)
        {
            return ServiceResult.Fail("This course was already deleted.");
        }

        if (subject.Curriculum > 0)
        {
            return ServiceResult.Fail($"{subject.Name} is part of {Plural(subject.Curriculum, "strand curriculum", "strand curricula")}. Remove it from the strands first, or switch it off instead.");
        }

        if (subject.Students > 0 || subject.Teaching > 0 || subject.People > 0)
        {
            return ServiceResult.Fail($"{subject.Name} is in use by students or teachers, so it can't be deleted. Switch it off instead.");
        }

        await db.Subjects.Where(s => s.Id == id).ExecuteDeleteAsync(ct);
        return ServiceResult.Ok($"The course {subject.Name} was deleted.");
    }

    // ======================================================================= sections

    public async Task<IReadOnlyList<SectionRow>> ListSectionsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await db.Sections.AsNoTracking()
            .OrderByDescending(s => s.SchoolYear.StartDate).ThenBy(s => s.GradeLevel).ThenBy(s => s.Strand.Code).ThenBy(s => s.ScheduleId).ThenBy(s => s.Name)
            .Select(s => new
            {
                s.Id, s.SchoolYearId, Year = s.SchoolYear.Name, s.StrandId, StrandCode = s.Strand.Code, s.GradeLevel, s.ScheduleId,
                Schedule = s.Schedule != null ? s.Schedule.Code : null, s.Name, s.AdviserTeacherId,
                Adviser = s.Adviser != null ? s.Adviser.LastName + ", " + s.Adviser.FirstName : null,
                Students = db.Enrollments.Where(e => e.SectionId == s.Id).Select(e => e.StudentId).Distinct().Count(),
                Teachers = db.TeachingAssignments.Count(a => a.SectionId == s.Id),
            })
            .ToListAsync(ct);
        return rows.Select(r => new SectionRow(r.Id, r.SchoolYearId, r.Year, r.StrandId, r.StrandCode, r.GradeLevel, r.ScheduleId, r.Schedule, r.Name, r.AdviserTeacherId, r.Adviser, r.Students, r.Teachers)).ToList();
    }

    public async Task<ServiceResult> SaveSectionAsync(SectionInput input, int actingUserId, CancellationToken ct = default)
    {
        var errors = new List<string>();
        var name = Clean(input.Name);
        if (name.Length is 0 or > 50 || !SectionNamePattern().IsMatch(name))
        {
            errors.Add("Enter the section name, for example A. Use letters, numbers, spaces and dashes (up to 50 characters).");
        }

        if (input.GradeLevel is not (11 or 12))
        {
            errors.Add("Pick grade level 11 or 12.");
        }

        if (errors.Count > 0)
        {
            return ServiceResult.Fail(errors);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsAdminAsync(db, actingUserId, ct))
        {
            return ServiceResult.Fail(AdminOnly);
        }

        var strand = await db.Strands.AsNoTracking().Where(s => s.Id == input.StrandId).Select(s => new { s.Id, s.Code }).FirstOrDefaultAsync(ct);
        if (strand is null)
        {
            return ServiceResult.Fail("Pick the strand of the section.");
        }

        if (!await db.SchoolYears.AnyAsync(y => y.Id == input.SchoolYearId, ct))
        {
            return ServiceResult.Fail("Pick the school year of the section.");
        }

        var schedule = await db.Schedules.AsNoTracking().Where(s => s.Id == input.ScheduleId).Select(s => new { s.Id, s.Code }).FirstOrDefaultAsync(ct);
        if (schedule is null)
        {
            return ServiceResult.Fail("Pick when the section meets.");
        }

        if (!await db.StrandSchedules.AnyAsync(x => x.StrandId == strand.Id && x.ScheduleId == schedule.Id, ct))
        {
            return ServiceResult.Fail($"{strand.Code} has no {schedule.Code} classes. Pick one of its schedules.");
        }

        if (input.AdviserTeacherId is { } adviserId && !await db.Teachers.AnyAsync(t => t.Id == adviserId, ct))
        {
            return ServiceResult.Fail("The adviser was not found.");
        }

        if (await db.Sections.AnyAsync(s => s.SchoolYearId == input.SchoolYearId && s.StrandId == strand.Id && s.GradeLevel == input.GradeLevel && s.ScheduleId == schedule.Id && s.Name == name && s.Id != input.Id, ct))
        {
            return ServiceResult.Fail($"There is already a section {input.GradeLevel}-{strand.Code}-{name} in the {schedule.Code} schedule of that school year.");
        }

        var label = TeachingService.SectionLabel((byte)input.GradeLevel, strand.Code, name);
        if (input.Id is { } id)
        {
            var found = await db.Sections.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
            if (found is null)
            {
                return ServiceResult.Fail("This section no longer exists.");
            }

            // Students and teachers are tied to the school year, strand, grade level and schedule of the section.
            var moved = found.SchoolYearId != input.SchoolYearId || found.StrandId != strand.Id || found.GradeLevel != input.GradeLevel || found.ScheduleId != schedule.Id;
            if (moved && (await db.Enrollments.AnyAsync(e => e.SectionId == id, ct) || await db.TeachingAssignments.AnyAsync(a => a.SectionId == id, ct)
                          || await db.Students.AnyAsync(s => s.SectionId == id, ct) || await db.Teachers.AnyAsync(t => t.SectionId == id, ct)))
            {
                return ServiceResult.Fail("Students or teachers are in this section, so only its name and its adviser can change.");
            }

            // The school year, strand and grade level are part of keys that enrollments use, so the row is updated directly.
            var adviser = input.AdviserTeacherId;
            var updated = await TryAsync(() => db.Sections.Where(s => s.Id == id).ExecuteUpdateAsync(set => set
                .SetProperty(s => s.SchoolYearId, input.SchoolYearId)
                .SetProperty(s => s.StrandId, strand.Id)
                .SetProperty(s => s.GradeLevel, (byte)input.GradeLevel)
                .SetProperty(s => s.ScheduleId, schedule.Id)
                .SetProperty(s => s.Name, name)
                .SetProperty(s => s.AdviserTeacherId, adviser)
                .SetProperty(s => s.UpdatedAtUtc, DateTime.UtcNow), ct));
            return updated is null ? ServiceResult.Fail(SaveFailed) : ServiceResult.Ok($"The section {label} was saved.");
        }

        db.Sections.Add(new Section
        {
            SchoolYearId = input.SchoolYearId, StrandId = strand.Id, GradeLevel = (byte)input.GradeLevel, ScheduleId = schedule.Id, Name = name, AdviserTeacherId = input.AdviserTeacherId,
        });
        return await SaveAsync(db, $"The section {label} was added.", ct);
    }

    public async Task<ServiceResult> DeleteSectionAsync(int id, int actingUserId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsAdminAsync(db, actingUserId, ct))
        {
            return ServiceResult.Fail(AdminOnly);
        }

        var section = await db.Sections.Where(s => s.Id == id).Select(s => new
        {
            s.GradeLevel, Strand = s.Strand.Code, s.Name,
            Students = db.Enrollments.Count(e => e.SectionId == id),
            Teachers = db.TeachingAssignments.Count(a => a.SectionId == id),
            People = db.Students.Count(x => x.SectionId == id) + db.Teachers.Count(x => x.SectionId == id),
        }).FirstOrDefaultAsync(ct);
        if (section is null)
        {
            return ServiceResult.Fail("This section was already deleted.");
        }

        var label = TeachingService.SectionLabel((byte)section.GradeLevel, section.Strand, section.Name);
        if (section.Students > 0 || section.Teachers > 0 || section.People > 0)
        {
            return ServiceResult.Fail($"{label} has students or teachers, so it can't be deleted.");
        }

        await db.Sections.Where(s => s.Id == id).ExecuteDeleteAsync(ct);
        return ServiceResult.Ok($"The section {label} was deleted.");
    }

    // ======================================================================= helpers

    private static Task<bool> IsAdminAsync(AppDbContext db, int userId, CancellationToken ct) =>
        db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.IsActive && u.RoleId == AppRoles.AdminId, ct);

    private const string SaveFailed = "It could not be saved: someone else saved the same thing, or something depends on it. Reload and try again.";

    /// <summary>Saves the changes. A duplicate or a record in use that the checks missed (somebody else was faster) becomes a plain sentence.</summary>
    private static async Task<ServiceResult> SaveAsync(AppDbContext db, string successMessage, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return ServiceResult.Ok(successMessage);
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Fail(SaveFailed);
        }
    }

    /// <summary>Runs a direct update. A database refusal (a duplicate or a key in use) gives null instead of an exception.</summary>
    private static async Task<int?> TryAsync(Func<Task<int>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (ex is DbException or DbUpdateException)
        {
            return null;
        }
    }

    /// <summary>Trimmed, with every run of spaces made into one space. Null becomes "".</summary>
    private static string Clean(string? text) =>
        string.Join(' ', (text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static string Plural(int count, string one, string? many = null) => count == 1 ? $"1 {one}" : $"{count} {many ?? one + "s"}";
}
