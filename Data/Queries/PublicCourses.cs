using Microsoft.EntityFrameworkCore;

namespace SEAITT_LMS.Data.Queries;

public sealed record CourseSubject(string Code, string Name);

/// <param name="Name">The strand's own name. Same as the code when the school only gave a code.</param>
public sealed record CourseStrand(string Code, string Name, string Track, IReadOnlyList<string> Schedules, IReadOnlyList<CourseSubject> Subjects);

public sealed record CourseTerm(byte TermNumber, string Name, IReadOnlyList<CourseStrand> Strands);

public sealed record CourseGrade(byte GradeLevel, IReadOnlyList<CourseTerm> Terms);

/// <summary>The courses shown on the public first page: for each grade level and term, the subjects of every strand.</summary>
public static class PublicCourses
{
    public static async Task<IReadOnlyList<CourseGrade>> LoadAsync(AppDbContext db, CancellationToken ct = default)
    {
        var rows = await db.StrandSubjects.AsNoTracking()
            .Where(x => x.Strand.IsActive && x.Subject.IsActive)
            .Select(x => new
            {
                x.GradeLevel,
                x.TermNumber,
                StrandId = x.StrandId,
                StrandCode = x.Strand.Code,
                StrandName = x.Strand.Name,
                Track = x.Strand.Track.Code,
                SubjectCode = x.Subject.Code,
                SubjectName = x.Subject.Name,
            })
            .ToListAsync(ct);

        if (rows.Count == 0)
        {
            return [];
        }

        var schedules = await db.StrandSchedules.AsNoTracking()
            .OrderBy(x => x.ScheduleId)
            .Select(x => new { x.StrandId, x.Schedule.Code })
            .ToListAsync(ct);

        // "1st Term", "2nd Term": the names the newest school year uses for its terms.
        var termNames = await db.Terms.AsNoTracking()
            .OrderByDescending(t => t.SchoolYear.StartDate)
            .Select(t => new { t.TermNumber, t.Name })
            .ToListAsync(ct);

        string TermName(byte number) =>
            termNames.FirstOrDefault(t => t.TermNumber == number)?.Name ?? $"Term {number}";

        return rows
            .GroupBy(r => r.GradeLevel)
            .OrderBy(g => g.Key)
            .Select(grade => new CourseGrade(
                grade.Key,
                grade.GroupBy(r => r.TermNumber)
                    .OrderBy(t => t.Key)
                    .Select(term => new CourseTerm(
                        term.Key,
                        TermName(term.Key),
                        term.GroupBy(r => new { r.StrandId, r.StrandCode, r.StrandName, r.Track })
                            .OrderBy(s => s.Key.Track).ThenBy(s => s.Key.StrandId)
                            .Select(strand => new CourseStrand(
                                strand.Key.StrandCode,
                                strand.Key.StrandName,
                                strand.Key.Track,
                                schedules.Where(s => s.StrandId == strand.Key.StrandId).Select(s => s.Code).ToList(),
                                strand.OrderBy(r => r.SubjectName).Select(r => new CourseSubject(r.SubjectCode, r.SubjectName)).ToList()))
                            .ToList()))
                    .ToList()))
            .ToList();
    }
}
