using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Data.Queries;

/// <summary>
/// The final grade of an enrollment: the average of the grades of its subjects. The grade of a subject is the points the student earned
/// divided by the points possible, of the work that was graded (the rule of the student's "Average grade"). A subject nobody graded yet has
/// no grade and is left out; when no subject has one the final grade is empty.
/// </summary>
public static class FinalGrades
{
    public static async Task<IReadOnlyDictionary<int, decimal>> LoadAsync(AppDbContext db, IReadOnlyCollection<int> enrollmentIds, CancellationToken ct = default)
    {
        if (enrollmentIds.Count == 0)
        {
            return new Dictionary<int, decimal>();
        }

        var pairs = await db.EnrollmentSubjects.AsNoTracking()
            .Where(x => enrollmentIds.Contains(x.EnrollmentId))
            .Select(x => new { x.EnrollmentId, x.Enrollment.StudentId, x.Enrollment.SectionId, x.SubjectId })
            .ToListAsync(ct);
        if (pairs.Count == 0)
        {
            return new Dictionary<int, decimal>();
        }

        var sectionIds = pairs.Select(p => p.SectionId).Distinct().ToList();
        var assignments = await db.TeachingAssignments.AsNoTracking()
            .Where(a => sectionIds.Contains(a.SectionId))
            .Select(a => new { a.Id, a.SectionId, a.SubjectId })
            .ToListAsync(ct);
        var assignmentOf = assignments.ToDictionary(a => (a.SectionId, a.SubjectId), a => a.Id);

        var studentIds = pairs.Select(p => p.StudentId).Distinct().ToList();
        var assignmentIds = assignments.Select(a => a.Id).ToList();
        var scores = await db.Submissions.AsNoTracking()
            .Where(s => s.Status == SubmissionStatus.Graded && s.Score != null && studentIds.Contains(s.StudentId) && assignmentIds.Contains(s.Item.Module.AssignmentId))
            .Select(s => new { s.StudentId, AssignmentId = s.Item.Module.AssignmentId, Score = s.Score!.Value, Max = s.Item.MaxScore })
            .ToListAsync(ct);
        var byPair = scores
            .GroupBy(s => (s.StudentId, s.AssignmentId))
            .ToDictionary(g => g.Key, g => (Earned: g.Sum(x => x.Score), Possible: g.Sum(x => x.Max)));

        var result = new Dictionary<int, decimal>();
        foreach (var enrollment in pairs.GroupBy(p => p.EnrollmentId))
        {
            var percents = new List<decimal>();
            foreach (var pair in enrollment)
            {
                if (assignmentOf.TryGetValue((pair.SectionId, pair.SubjectId), out var assignmentId)
                    && byPair.TryGetValue((pair.StudentId, assignmentId), out var total) && total.Possible > 0)
                {
                    percents.Add(Math.Round(total.Earned / total.Possible * 100, 1));
                }
            }

            if (percents.Count > 0)
            {
                result[enrollment.Key] = Math.Round(percents.Average(), 1);
            }
        }

        return result;
    }
}
