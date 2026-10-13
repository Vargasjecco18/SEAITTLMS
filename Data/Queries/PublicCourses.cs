using Microsoft.EntityFrameworkCore;

namespace SEAITT_LMS.Data.Queries;

public sealed record PublicCourse(string Code, string Name);

/// <summary>The courses a strand has in one term of one grade level.</summary>
public sealed record PublicCourseGroup(byte GradeLevel, byte TermNumber, string TermName, IReadOnlyList<PublicCourse> Courses);

/// <param name="Name">The strand's full name. Same as the code when the school only gave a code.</param>
/// <param name="CourseCount">How many different courses the strand has (a course that is taught in two terms counts once).</param>
public sealed record PublicStrand(string Code, string Name, int CourseCount, IReadOnlyList<PublicCourseGroup> Groups);

public sealed record PublicTrack(string Code, string Name, IReadOnlyList<PublicStrand> Strands);

/// <summary>
/// The courses shown on the public first page: first the tracks, each with its strands (full names), and under a strand the
/// courses it has, by grade level and term.
/// </summary>
public static class PublicCourses
{
    public static async Task<IReadOnlyList<PublicTrack>> LoadAsync(AppDbContext db, CancellationToken ct = default)
    {
        var strands = await db.Strands.AsNoTracking()
            .Where(s => s.IsActive && s.Track.IsActive)
            .OrderBy(s => s.TrackId).ThenBy(s => s.Id)
            .Select(s => new { s.Id, s.Code, s.Name, TrackId = s.TrackId, TrackCode = s.Track.Code, TrackName = s.Track.Name })
            .ToListAsync(ct);

        if (strands.Count == 0)
        {
            return [];
        }

        var curriculum = await db.StrandSubjects.AsNoTracking()
            .Where(x => x.Strand.IsActive && x.Subject.IsActive)
            .Select(x => new { x.StrandId, x.GradeLevel, x.TermNumber, x.Subject.Code, x.Subject.Name })
            .ToListAsync(ct);

        // "1st Term", "2nd Term": the names the newest school year uses for its terms.
        var termNames = await db.Terms.AsNoTracking()
            .OrderByDescending(t => t.SchoolYear.StartDate)
            .Select(t => new { t.TermNumber, t.Name })
            .ToListAsync(ct);

        string TermName(byte number) =>
            termNames.FirstOrDefault(t => t.TermNumber == number)?.Name ?? $"Term {number}";

        PublicStrand Build(int strandId, string code, string name)
        {
            var own = curriculum.Where(c => c.StrandId == strandId).ToList();
            var groups = own
                .GroupBy(c => new { c.GradeLevel, c.TermNumber })
                .OrderBy(g => g.Key.GradeLevel).ThenBy(g => g.Key.TermNumber)
                .Select(g => new PublicCourseGroup(
                    g.Key.GradeLevel, g.Key.TermNumber, TermName(g.Key.TermNumber),
                    g.OrderBy(c => c.Name).Select(c => new PublicCourse(c.Code, c.Name)).ToList()))
                .ToList();
            return new PublicStrand(
                code, name,
                own.Select(c => c.Code).Distinct().Count(),
                groups);
        }

        return strands
            .GroupBy(s => new { s.TrackId, s.TrackCode, s.TrackName })
            .Select(track => new PublicTrack(
                track.Key.TrackCode, track.Key.TrackName,
                track.Select(s => Build(s.Id, s.Code, s.Name)).ToList()))
            .ToList();
    }
}
