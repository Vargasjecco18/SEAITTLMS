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

public sealed record GradedWork(int ItemId, string Title, string Kind, decimal Score, decimal Max, DateTime? GradedAtUtc, bool IsLate, bool Summative = false)
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
            var exams = Graded.Where(g => g.Kind == ItemKinds.Exam && g.Summative && g.Max > 0).ToList();
            return exams.Count == 0 ? null : Math.Round(exams.Sum(e => e.Score) / exams.Sum(e => e.Max) * 100, 1);
        }
    }

    /// <summary>True when the course has a summative exam (graded or not).</summary>
    public bool HasExam { get; init; }

    /// <summary>True while the teacher hides the grades of this course from the students (the button Hide from students in the gradebook): there is no percent, no final grade and no graded work to show.</summary>
    public bool GradesHidden { get; init; }
}

public sealed record StudentGradeSummary(IReadOnlyList<SubjectGrade> Subjects, decimal? Overall, int FailingSubjects, int GradedCount)
{
    public IEnumerable<GradedWork> AllGraded => Subjects.SelectMany(s => s.Graded);
}

/// <summary>One course of a term of a student's history: the percent of the graded work and the final grade the teacher sent, when there is one.</summary>
public sealed record HistoryCourse(string Subject, string Teacher, decimal? Percent, decimal? Final)
{
    /// <summary>The number the course counts as: the final grade the teacher sent, else the percent of the graded work.</summary>
    public decimal? Grade => Final ?? Percent;
}

/// <summary>A term of a student's schooling with its courses and their grades (for the registrar's overall grade box and the archive).</summary>
public sealed record HistoryTerm(int EnrollmentId, string SchoolYear, string Term, byte GradeLevel, string Track, string Strand, string Section, string Status, IReadOnlyList<HistoryCourse> Courses)
{
    public decimal? Average => Courses.Where(c => c.Grade is not null).Select(c => c.Grade!.Value).DefaultIfEmpty().Average() is var avg && Courses.Any(c => c.Grade is not null) ? Math.Round(avg, 1) : null;
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
                a.Id, SubjectName = a.Subject.Name, Teacher = a.Teacher.FirstName + " " + a.Teacher.LastName, a.GradesHidden,
                a.Section.GradeLevel, Strand = a.Section.Strand.Code, Section = a.Section.Name,
            })
            .ToListAsync(ct);
        var ids = assignments.Select(a => a.Id).ToList();

        var items = await db.ModuleItems.AsNoTracking()
            .Where(i => ids.Contains(i.Module.AssignmentId) && i.Module.VisibleFromUtc != null && i.Module.VisibleFromUtc <= now
                        && (i.VisibleFromUtc == null || i.VisibleFromUtc <= now))
            .Select(i => new
            {
                i.Id, i.Title, i.Kind, i.MaxScore, i.DueAtUtc, i.ShowResult, AssignmentId = i.Module.AssignmentId, Summative = i.MaterialItemId != null,
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
                .Select(i => new GradedWork(i.Id, i.Title, i.Kind, i.Mine!.Score!.Value, i.MaxScore, i.Mine.GradedAtUtc, i.Mine.IsLate, i.Summative))
                .OrderByDescending(g => g.GradedAtUtc)
                .ToList();
            // the teacher hides the grades of this course: nothing of the scores is told (the number of graded works is left at 0 too)
            if (a.GradesHidden)
            {
                graded = [];
            }

            var possible = graded.Sum(g => g.Max);
            decimal? percent = possible <= 0 ? null : Math.Round(graded.Sum(g => g.Score) / possible * 100, 1);
            var open = mine.Count(i => i.Mine is null or { Status: SubmissionStatus.InProgress });
            var missing = mine.Count(i => (i.Mine is null or { Status: SubmissionStatus.InProgress }) && i.DueAtUtc is { } due && due < now);

            subjects.Add(new SubjectGrade(a.Id, a.SubjectName, a.Teacher, TeachingService.SectionLabel(a.GradeLevel, a.Strand, a.Section), percent, graded.Count, mine.Count, open, missing, graded, a.GradesHidden ? null : finals.TryGetValue(a.Id, out var final) ? final : null)
            {
                HasExam = mine.Any(i => i.Kind == ItemKinds.Exam && i.Summative),
                GradesHidden = a.GradesHidden,
            });
        }

        var withGrade = subjects.Where(s => s.Percent is not null).ToList();
        decimal? overall = withGrade.Count == 0 ? null : Math.Round(withGrade.Average(s => s.Percent!.Value), 1);
        return new StudentGradeSummary(subjects, overall, withGrade.Count(s => s.Percent < GradeRules.PassingPercent), subjects.Sum(s => s.GradedCount));
    }

    /// <summary>
    /// Every term of a student's schooling, the oldest first (from the first enrollment, with the strand the student was in then, so a student who shifted shows both), with the courses and
    /// their grades. Staff look at it: nothing is hidden.
    /// </summary>
    public async Task<IReadOnlyList<HistoryTerm>> GetHistoryAsync(int studentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var enrollments = await db.Enrollments.AsNoTracking().Where(e => e.StudentId == studentId)
            .OrderBy(e => e.Term.SchoolYear.StartDate).ThenBy(e => e.Term.TermNumber).ThenBy(e => e.Id)
            .Select(e => new { e.Id, e.SectionId, e.GradeLevel, Track = e.Track.Code, Strand = e.Strand.Code, Section = e.Section.Name, Term = e.Term.Name, SchoolYear = e.Term.SchoolYear.Name, Status = e.Status.ToString() })
            .ToListAsync(ct);
        if (enrollments.Count == 0)
        {
            return [];
        }

        var ids = enrollments.Select(e => e.Id).ToList();
        var subjects = await db.EnrollmentSubjects.AsNoTracking().Where(x => ids.Contains(x.EnrollmentId))
            .Select(x => new { x.EnrollmentId, x.SubjectId, Name = x.Subject.Name }).ToListAsync(ct);
        var sectionIds = enrollments.Select(e => e.SectionId).Distinct().ToList();
        var assignments = await db.TeachingAssignments.AsNoTracking().Where(a => sectionIds.Contains(a.SectionId))
            .Select(a => new { a.Id, a.SectionId, a.SubjectId, Teacher = a.Teacher.FirstName + " " + a.Teacher.LastName }).ToListAsync(ct);
        var assignmentIds = assignments.Select(a => a.Id).ToList();
        var scores = await db.Submissions.AsNoTracking()
            .Where(s => s.StudentId == studentId && s.Status == SubmissionStatus.Graded && s.Score != null && assignmentIds.Contains(s.Item.Module.AssignmentId))
            .Select(s => new { AssignmentId = s.Item.Module.AssignmentId, Score = s.Score!.Value, Max = s.Item.MaxScore }).ToListAsync(ct);
        var byAssignment = scores.GroupBy(s => s.AssignmentId).ToDictionary(g => g.Key, g => (Earned: g.Sum(x => x.Score), Possible: g.Sum(x => x.Max)));
        var finals = await db.FinalGrades.AsNoTracking().Where(g => g.StudentId == studentId && assignmentIds.Contains(g.AssignmentId)).ToDictionaryAsync(g => g.AssignmentId, g => g.Grade, ct);

        return [.. enrollments.Select(e => new HistoryTerm(e.Id, e.SchoolYear, e.Term, e.GradeLevel, e.Track, e.Strand, e.Section, e.Status,
            [.. subjects.Where(x => x.EnrollmentId == e.Id).OrderBy(x => x.Name).Select(x =>
            {
                var assignment = assignments.FirstOrDefault(a => a.SectionId == e.SectionId && a.SubjectId == x.SubjectId);
                decimal? percent = null;
                decimal? final = null;
                if (assignment is not null)
                {
                    if (byAssignment.TryGetValue(assignment.Id, out var total) && total.Possible > 0)
                    {
                        percent = Math.Round(total.Earned / total.Possible * 100, 1);
                    }

                    final = finals.TryGetValue(assignment.Id, out var grade) ? grade : null;
                }

                return new HistoryCourse(x.Name, assignment?.Teacher ?? "—", percent, final);
            })]))];
    }
}
