using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>The school's rules for grades.</summary>
public static class GradeRules
{
    /// <summary>A grade under this percent is failing. The guidance counselor is told.</summary>
    public const decimal PassingPercent = 70;

    /// <summary>A grade between the passing mark and this one is "at risk" (close to failing).</summary>
    public const decimal AtRiskUntil = 75;

    public static string Descriptor(decimal percent) => percent switch
    {
        >= 90 => "Outstanding",
        >= 85 => "Very satisfactory",
        >= 80 => "Satisfactory",
        >= PassingPercent => "Fairly satisfactory",
        _ => "Did not meet expectations",
    };

    public static Components.Shared.Tone ToneOf(decimal? percent) => percent switch
    {
        null => Components.Shared.Tone.Neutral,
        >= 85 => Components.Shared.Tone.Success,
        >= AtRiskUntil => Components.Shared.Tone.Info,
        >= PassingPercent => Components.Shared.Tone.Warning,
        _ => Components.Shared.Tone.Danger,
    };
}

public sealed record GradedWork(int ItemId, string Title, string Kind, decimal Score, decimal Max, DateTime? GradedAtUtc, bool IsLate)
{
    public decimal Percent => Max <= 0 ? 0 : Score / Max * 100;
}

public sealed record SubjectGrade(
    int AssignmentId, string SubjectName, string TeacherName, string SectionLabel, decimal? Percent,
    int GradedCount, int TotalItems, int OpenItems, int Missing, IReadOnlyList<GradedWork> Graded, decimal? FinalGrade = null)
{
    /// <summary>The grade of the summative exam (the percent of its points; the average when there is more than one), or nothing until an exam is graded.</summary>
    public decimal? ExamPercent
    {
        get
        {
            var exams = Graded.Where(g => g.Kind == ItemKinds.Exam && g.Max > 0).ToList();
            return exams.Count == 0 ? null : Math.Round(exams.Sum(e => e.Score) / exams.Sum(e => e.Max) * 100, 1);
        }
    }

    /// <summary>True when the course has a summative exam (graded or not).</summary>
    public bool HasExam { get; init; }
}

public sealed record StudentGradeSummary(IReadOnlyList<SubjectGrade> Subjects, decimal? Overall, int FailingSubjects, int GradedCount)
{
    public IEnumerable<GradedWork> AllGraded => Subjects.SelectMany(s => s.Graded);
}

/// <summary>
/// Grades from the scores of the work. A subject's grade is the points earned divided by the points possible, of the work that has
/// been graded (so one graded activity already gives a grade). A quiz whose result the teacher hides is left out for the student.
/// </summary>
public sealed class GradeService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider clock)
{
    public async Task<StudentGradeSummary> GetStudentSummaryAsync(int studentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;

        var assignments = await Roster.AssignmentsOf(db, studentId).AsNoTracking()
            .OrderBy(a => a.Subject.Name)
            .Select(a => new
            {
                a.Id, SubjectName = a.Subject.Name, Teacher = a.Teacher.FirstName + " " + a.Teacher.LastName,
                a.Section.GradeLevel, Strand = a.Section.Strand.Code, Section = a.Section.Name,
            })
            .ToListAsync(ct);
        var ids = assignments.Select(a => a.Id).ToList();

        var items = await db.ModuleItems.AsNoTracking()
            .Where(i => ids.Contains(i.Module.AssignmentId) && i.Module.VisibleFromUtc != null && i.Module.VisibleFromUtc <= now
                        && (i.VisibleFromUtc == null || i.VisibleFromUtc <= now))
            .Select(i => new
            {
                i.Id, i.Title, i.Kind, i.MaxScore, i.DueAtUtc, i.ShowResult, AssignmentId = i.Module.AssignmentId,
                Mine = i.Submissions.Where(s => s.StudentId == studentId).Select(s => new { s.Status, s.Score, s.GradedAtUtc, s.IsLate }).FirstOrDefault(),
            })
            .ToListAsync(ct);

        // The final grade the teacher gave in each subject, when there is one.
        var finals = await db.FinalGrades.AsNoTracking().Where(g => g.StudentId == studentId && ids.Contains(g.AssignmentId)).ToDictionaryAsync(g => g.AssignmentId, g => g.Grade, ct);

        var subjects = new List<SubjectGrade>();
        foreach (var a in assignments)
        {
            var mine = items.Where(i => i.AssignmentId == a.Id).ToList();
            var graded = mine
                .Where(i => i.Mine is { Status: SubmissionStatus.Graded, Score: not null } && !(ItemKinds.IsQuizLike(i.Kind) && !i.ShowResult))
                .Select(i => new GradedWork(i.Id, i.Title, i.Kind, i.Mine!.Score!.Value, i.MaxScore, i.Mine.GradedAtUtc, i.Mine.IsLate))
                .OrderByDescending(g => g.GradedAtUtc)
                .ToList();
            var possible = graded.Sum(g => g.Max);
            decimal? percent = possible <= 0 ? null : Math.Round(graded.Sum(g => g.Score) / possible * 100, 1);
            var open = mine.Count(i => i.Mine is null or { Status: SubmissionStatus.InProgress });
            var missing = mine.Count(i => (i.Mine is null or { Status: SubmissionStatus.InProgress }) && i.DueAtUtc is { } due && due < now);

            subjects.Add(new SubjectGrade(a.Id, a.SubjectName, a.Teacher, TeachingService.SectionLabel(a.GradeLevel, a.Strand, a.Section), percent, graded.Count, mine.Count, open, missing, graded, finals.TryGetValue(a.Id, out var final) ? final : null) { HasExam = mine.Any(i => i.Kind == ItemKinds.Exam) });
        }

        var withGrade = subjects.Where(s => s.Percent is not null).ToList();
        decimal? overall = withGrade.Count == 0 ? null : Math.Round(withGrade.Average(s => s.Percent!.Value), 1);
        return new StudentGradeSummary(subjects, overall, withGrade.Count(s => s.Percent < GradeRules.PassingPercent), subjects.Sum(s => s.GradedCount));
    }
}
