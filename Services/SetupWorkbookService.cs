using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Data;

namespace SEAITT_LMS.Services;

/// <summary>What the import of the academic setup did: how many rows are now in the setup, and each row that could not be saved with the reason.</summary>
public sealed record SetupImportResult(int Saved, IReadOnlyList<string> Errors);

/// <summary>
/// The Excel workbook of the academic setup (see <see cref="SetupSheets"/>): the export reads everything the setup has, the import saves the rows of a filled workbook. Every row goes through the same
/// <see cref="SchoolSetupService"/> and <see cref="CourseScheduleService"/> methods the tabs of the page use, so the same rules apply (and the same check that the person is the academic coordinator):
/// a row that breaks a rule is left out and says why, the others are saved. A row whose code, name or section already exists changes it; nothing is ever deleted.
/// </summary>
public sealed class SetupWorkbookService(IDbContextFactory<AppDbContext> dbFactory, SchoolSetupService setup, CourseScheduleService schedule)
{
    /// <summary>Everything the setup has, in the words of the workbook.</summary>
    public async Task<SetupSnapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        var years = await setup.ListSchoolYearsAsync(ct);
        var terms = await setup.ListTermsAsync(ct);
        var tracks = await setup.ListTracksAsync(ct);
        var strands = await setup.ListStrandsAsync(ct);
        var courses = await setup.ListSubjectsAsync(ct);
        var sections = await setup.ListSectionsAsync(ct);
        var dayParts = await schedule.GetDayPartsAsync(ct);

        var curriculum = new List<SetupCurriculumRow>();
        foreach (var strand in strands)
        {
            curriculum.AddRange((await setup.GetCurriculumAsync(strand.Id, ct)).Select(e => new SetupCurriculumRow(strand.Code, e.GradeLevel, e.TermNumber, e.SubjectCode)));
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var adviserIds = sections.Where(s => s.AdviserTeacherId is not null).Select(s => s.AdviserTeacherId!.Value).Distinct().ToList();
        var numbers = await db.Teachers.AsNoTracking().Where(t => adviserIds.Contains(t.Id)).Select(t => new { t.Id, t.EmployeeNumber }).ToDictionaryAsync(t => t.Id, t => t.EmployeeNumber, ct);

        var classes = new List<SetupClassRow>();
        foreach (var section in sections)
        {
            foreach (var course in await schedule.ListCoursesAsync(section.Id, ct))
            {
                if (course.Meeting is { } meeting)
                {
                    classes.Add(new SetupClassRow(section.SchoolYear, section.StrandCode, section.GradeLevel, section.Name, course.Code, meeting.Part ?? "", meeting.Days, meeting.Start, meeting.End));
                }
            }
        }

        return new SetupSnapshot(
            years, terms, tracks, strands, courses, curriculum,
            [.. sections.Select(s => new SetupSectionRow(s.SchoolYear, s.StrandCode, s.GradeLevel, s.Name, s.AdviserTeacherId is { } id ? numbers.GetValueOrDefault(id) : null))],
            dayParts, classes);
    }

    /// <summary>
    /// Saves the rows of a workbook that were read without problems, sheet by sheet in the order of the tabs. A row that is saved counts, also when it was already as the row says.
    /// <paramref name="actingUserId"/> has to be the academic coordinator (every save checks it).
    /// </summary>
    public async Task<SetupImportResult> ImportAsync(SetupBook book, int actingUserId, CancellationToken ct = default)
    {
        var saved = 0;
        var errors = new List<string>();

        // Only the academic coordinator changes the setup (a row that is already as it says would not reach the check of the save).
        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            if (!await db.Users.AsNoTracking().AnyAsync(u => u.Id == actingUserId && u.IsActive && u.RoleId == Data.Entities.AppRoles.AcademicCoordinatorId, ct))
            {
                return new SetupImportResult(0, ["Only the academic coordinator can change the academic setup."]);
            }
        }

        void Done(SetupLine line, ServiceResult result)
        {
            if (result.Succeeded)
            {
                saved++;
            }
            else
            {
                errors.Add($"{line.Place}: {string.Join(" ", result.Errors)}");
            }
        }

        // ---- the hours of AM, MID and PM (all together: they are checked together)
        var partRows = book.DayParts.Where(p => p.Line.IsReady).ToList();
        if (partRows.Count > 0)
        {
            var parts = await schedule.GetDayPartsAsync(ct);
            var inputs = new List<DayPartInput>();
            foreach (var row in partRows)
            {
                if (parts.FirstOrDefault(p => string.Equals(p.Code, row.Part, StringComparison.OrdinalIgnoreCase)) is { } part)
                {
                    inputs.Add(new DayPartInput(part.ScheduleId, row.Start, row.End));
                }
                else
                {
                    errors.Add($"{row.Line.Place}: there is no part of the day called {row.Part}.");
                }
            }

            if (inputs.Count > 0)
            {
                var result = await schedule.SaveDayPartsAsync(actingUserId, inputs, ct);
                if (result.Succeeded)
                {
                    saved += inputs.Count;
                }
                else
                {
                    errors.Add($"{SetupSheets.DayPartsSheet}: {string.Join(" ", result.Errors)}");
                }
            }
        }

        // ---- school years
        var years = (await setup.ListSchoolYearsAsync(ct)).ToDictionary(y => y.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var row in book.Years.Where(y => y.Line.IsReady))
        {
            years.TryGetValue(row.Name, out var old);
            if (old is not null && old.StartDate == row.Start && old.EndDate == row.End)
            {
                saved++;
                continue;
            }

            Done(row.Line, await setup.SaveSchoolYearAsync(new SchoolYearInput { Id = old?.Id, Name = row.Name, StartDate = row.Start, EndDate = row.End }, actingUserId, ct));
        }

        years = (await setup.ListSchoolYearsAsync(ct)).ToDictionary(y => y.Name, StringComparer.OrdinalIgnoreCase);

        // ---- terms
        var terms = await setup.ListTermsAsync(ct);
        foreach (var row in book.Terms.Where(t => t.Line.IsReady))
        {
            if (!years.TryGetValue(row.Year, out var year))
            {
                errors.Add($"{row.Line.Place}: there is no school year called {row.Year}. Add it in the sheet {SetupSheets.YearsSheet}.");
                continue;
            }

            var old = terms.FirstOrDefault(t => t.SchoolYearId == year.Id && t.TermNumber == row.Number);
            if (old is not null && old.Name == row.Name && old.StartDate == row.Start && old.EndDate == row.End && old.IsCurrent == row.Current)
            {
                saved++;
                continue;
            }

            Done(row.Line, await setup.SaveTermAsync(new TermInput { Id = old?.Id, SchoolYearId = year.Id, TermNumber = row.Number, Name = row.Name, StartDate = row.Start, EndDate = row.End, IsCurrent = row.Current }, actingUserId, ct));
            terms = await setup.ListTermsAsync(ct);
        }

        // ---- tracks
        var tracks = (await setup.ListTracksAsync(ct)).ToDictionary(t => t.Code, StringComparer.OrdinalIgnoreCase);
        foreach (var row in book.Tracks.Where(t => t.Line.IsReady))
        {
            tracks.TryGetValue(row.Code, out var old);
            if (old is not null && old.Name == row.Name && old.IsActive == row.Active)
            {
                saved++;
                continue;
            }

            Done(row.Line, await setup.SaveTrackAsync(new TrackInput { Id = old?.Id, Code = row.Code, Name = row.Name, IsActive = row.Active }, actingUserId, ct));
        }

        tracks = (await setup.ListTracksAsync(ct)).ToDictionary(t => t.Code, StringComparer.OrdinalIgnoreCase);

        // ---- strands
        var strands = (await setup.ListStrandsAsync(ct)).ToDictionary(s => s.Code, StringComparer.OrdinalIgnoreCase);
        foreach (var row in book.Strands.Where(s => s.Line.IsReady))
        {
            if (!tracks.TryGetValue(row.Track, out var track))
            {
                errors.Add($"{row.Line.Place}: there is no track with the code {row.Track}. Add it in the sheet {SetupSheets.TracksSheet}.");
                continue;
            }

            strands.TryGetValue(row.Code, out var old);
            if (old is not null && old.Name == row.Name && old.TrackId == track.Id && old.IsActive == row.Active)
            {
                saved++;
                continue;
            }

            Done(row.Line, await setup.SaveStrandAsync(new StrandInput { Id = old?.Id, Code = row.Code, Name = row.Name, TrackId = track.Id, IsActive = row.Active }, actingUserId, ct));
        }

        strands = (await setup.ListStrandsAsync(ct)).ToDictionary(s => s.Code, StringComparer.OrdinalIgnoreCase);

        // ---- courses
        var courses = (await setup.ListSubjectsAsync(ct)).ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);
        foreach (var row in book.Courses.Where(c => c.Line.IsReady))
        {
            courses.TryGetValue(row.Code, out var old);
            if (old is not null && old.Name == row.Name && old.Units == row.Units && (old.Description ?? "") == (row.Description ?? "") && old.IsActive == row.Active)
            {
                saved++;
                continue;
            }

            Done(row.Line, await setup.SaveSubjectAsync(new SubjectInput { Id = old?.Id, Code = row.Code, Name = row.Name, Description = row.Description, IsActive = row.Active, Units = row.Units }, actingUserId, ct));
        }

        courses = (await setup.ListSubjectsAsync(ct)).ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);

        // ---- the courses of each strand
        var known = new Dictionary<int, IReadOnlyList<CurriculumEntry>>();
        foreach (var row in book.Curriculum.Where(c => c.Line.IsReady))
        {
            if (!strands.TryGetValue(row.Strand, out var strand))
            {
                errors.Add($"{row.Line.Place}: there is no strand with the code {row.Strand}. Add it in the sheet {SetupSheets.StrandsSheet}.");
                continue;
            }

            if (!courses.TryGetValue(row.Course, out var course))
            {
                errors.Add($"{row.Line.Place}: there is no course with the code {row.Course}. Add it in the sheet {SetupSheets.CoursesSheet}.");
                continue;
            }

            if (!known.TryGetValue(strand.Id, out var entries))
            {
                known[strand.Id] = entries = await setup.GetCurriculumAsync(strand.Id, ct);
            }

            if (entries.Any(e => e.GradeLevel == row.Grade && e.TermNumber == row.Term && e.SubjectId == course.Id))
            {
                saved++;
                continue;
            }

            var added = await setup.AddCurriculumSubjectAsync(strand.Id, row.Grade, row.Term, course.Id, actingUserId, ct);
            Done(row.Line, added);
            known.Remove(strand.Id);
        }

        // ---- sections
        var numbers = await TeachersByEmployeeNumberAsync(ct);
        var sectionList = await setup.ListSectionsAsync(ct);
        foreach (var row in book.Sections.Where(s => s.Line.IsReady))
        {
            if (!years.TryGetValue(row.Year, out var year))
            {
                errors.Add($"{row.Line.Place}: there is no school year called {row.Year}. Add it in the sheet {SetupSheets.YearsSheet}.");
                continue;
            }

            if (!strands.TryGetValue(row.Strand, out var strand))
            {
                errors.Add($"{row.Line.Place}: there is no strand with the code {row.Strand}. Add it in the sheet {SetupSheets.StrandsSheet}.");
                continue;
            }

            int? adviser = null;
            if (row.Adviser is { } number)
            {
                if (!numbers.TryGetValue(number, out var teacherId))
                {
                    errors.Add($"{row.Line.Place}: no teacher has the employee number {number}.");
                    continue;
                }

                adviser = teacherId;
            }

            var old = sectionList.FirstOrDefault(s => s.SchoolYearId == year.Id && s.StrandId == strand.Id && s.GradeLevel == row.Grade && string.Equals(s.Name, row.Name, StringComparison.OrdinalIgnoreCase));
            if (old is not null && old.AdviserTeacherId == adviser)
            {
                saved++;
                continue;
            }

            Done(row.Line, await setup.SaveSectionAsync(new SectionInput { Id = old?.Id, SchoolYearId = year.Id, StrandId = strand.Id, GradeLevel = row.Grade, Name = row.Name, AdviserTeacherId = adviser }, actingUserId, ct));
            sectionList = await setup.ListSectionsAsync(ct);
        }

        // ---- the class times
        var partList = await schedule.GetDayPartsAsync(ct);
        foreach (var row in book.Classes.Where(c => c.Line.IsReady))
        {
            if (!years.TryGetValue(row.Year, out var year) || !strands.TryGetValue(row.Strand, out var strand))
            {
                errors.Add($"{row.Line.Place}: there is no section {row.Strand} {row.Section} in {row.Year}.");
                continue;
            }

            var section = sectionList.FirstOrDefault(s => s.SchoolYearId == year.Id && s.StrandId == strand.Id && s.GradeLevel == row.Grade && string.Equals(s.Name, row.Section, StringComparison.OrdinalIgnoreCase));
            if (section is null)
            {
                errors.Add($"{row.Line.Place}: there is no section {row.Strand} {row.Section} for grade {row.Grade} in {row.Year}. Add it in the sheet {SetupSheets.SectionsSheet}.");
                continue;
            }

            if (!courses.TryGetValue(row.Course, out var course))
            {
                errors.Add($"{row.Line.Place}: there is no course with the code {row.Course}.");
                continue;
            }

            var part = partList.FirstOrDefault(p => string.Equals(p.Code, row.Part, StringComparison.OrdinalIgnoreCase));
            if (part is null)
            {
                errors.Add($"{row.Line.Place}: there is no part of the day called {row.Part}.");
                continue;
            }

            Done(row.Line, await schedule.SaveAsync(actingUserId, section.Id, course.Id, part.ScheduleId, row.Days, row.Start, row.End, ct));
        }

        return new SetupImportResult(saved, errors);
    }

    private async Task<Dictionary<string, int>> TeachersByEmployeeNumberAsync(CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await db.Teachers.AsNoTracking().Where(t => t.EmployeeNumber != null).Select(t => new { t.Id, Number = t.EmployeeNumber! }).ToListAsync(ct);
        return rows.GroupBy(r => r.Number, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
    }
}
