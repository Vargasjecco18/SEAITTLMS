using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>One line of the curriculum of a strand, with the course it names (what the multilevel list of tracks, strands and courses shows).</summary>
public sealed record CurriculumLine(int StrandId, int GradeLevel, int TermNumber, int SubjectId, string SubjectCode, string SubjectName, decimal Units, bool IsActive);

public sealed partial class SchoolSetupService
{
    /// <summary>The courses of every strand, by grade level and term (one query for the whole list).</summary>
    public async Task<IReadOnlyList<CurriculumLine>> GetFullCurriculumAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.StrandSubjects.AsNoTracking()
            .OrderBy(x => x.StrandId).ThenBy(x => x.GradeLevel).ThenBy(x => x.TermNumber).ThenBy(x => x.Subject.Name)
            .Select(x => new CurriculumLine(x.StrandId, x.GradeLevel, x.TermNumber, x.SubjectId, x.Subject.Code, x.Subject.Name, x.Subject.Units, x.Subject.IsActive))
            .ToListAsync(ct);
    }

    /// <summary>
    /// The teachers who can be the adviser of a section: the ones of the same strand (the strand of the teacher's profile, or a strand the teacher gives courses in). The adviser of the section
    /// stays in the list even when that is not so any more.
    /// </summary>
    public async Task<IReadOnlyList<TeacherChoice>> ListAdviserChoicesAsync(int sectionId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var section = await db.Sections.AsNoTracking().Where(s => s.Id == sectionId).Select(s => new { s.StrandId, s.AdviserTeacherId }).FirstOrDefaultAsync(ct);
        if (section is null)
        {
            return [];
        }

        var teachers = await db.Teachers.AsNoTracking()
            .Where(t => t.StrandId == section.StrandId
                        || t.Id == section.AdviserTeacherId
                        || db.TeachingAssignments.Any(a => a.TeacherId == t.Id && a.Section.StrandId == section.StrandId))
            .OrderBy(t => t.LastName).ThenBy(t => t.FirstName)
            .Select(t => new { t.Id, t.FirstName, t.LastName, t.GradeLevel })
            .ToListAsync(ct);
        var classes = await db.TeachingAssignments.AsNoTracking().Where(a => a.Section.StrandId == section.StrandId)
            .Select(a => new { a.TeacherId, Course = a.Subject.Name }).Distinct().ToListAsync(ct);

        return [.. teachers.Select(t => new TeacherChoice(
            t.Id, $"{t.LastName}, {t.FirstName}", t.GradeLevel is { } g ? $"Grade {g}" : null, null,
            classes.Where(c => c.TeacherId == t.Id).Select(c => c.Course).Order().ToList()))];
    }

    /// <summary>Chooses the adviser of a section (null: none). Only a teacher of the same strand can be the adviser.</summary>
    public async Task<ServiceResult> SetAdviserAsync(int sectionId, int? teacherId, int actingUserId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsCoordinatorAsync(db, actingUserId, ct))
        {
            return ServiceResult.Fail(CoordinatorOnly);
        }

        var section = await db.Sections.AsNoTracking().Where(s => s.Id == sectionId).Select(s => new { s.StrandId, s.GradeLevel, Strand = s.Strand.Code, s.Name }).FirstOrDefaultAsync(ct);
        if (section is null)
        {
            return ServiceResult.Fail("This section no longer exists.");
        }

        var label = TeachingService.SectionLabel((byte)section.GradeLevel, section.Strand, section.Name);
        string? teacherName = null;
        if (teacherId is { } id)
        {
            var teacher = await db.Teachers.AsNoTracking().Where(t => t.Id == id)
                .Select(t => new { t.FirstName, t.LastName, SameStrand = t.StrandId == section.StrandId || db.TeachingAssignments.Any(a => a.TeacherId == t.Id && a.Section.StrandId == section.StrandId) })
                .FirstOrDefaultAsync(ct);
            if (teacher is null)
            {
                return ServiceResult.Fail("The adviser was not found.");
            }

            if (!teacher.SameStrand)
            {
                return ServiceResult.Fail($"The adviser of {label} has to be a teacher of the same strand ({section.Strand}).");
            }

            teacherName = $"{teacher.FirstName} {teacher.LastName}";
        }

        await db.Sections.Where(s => s.Id == sectionId).ExecuteUpdateAsync(set => set.SetProperty(s => s.AdviserTeacherId, teacherId).SetProperty(s => s.UpdatedAtUtc, DateTime.UtcNow), ct);
        return ServiceResult.Ok(teacherName is null ? $"{label} has no adviser now." : $"{teacherName} is the adviser of {label}.");
    }
}
