using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

public sealed record GradebookColumn(int ItemId, string Title, string Kind, decimal Max, string ModuleTitle);

/// <param name="Status">NotStarted, InProgress, Submitted (waiting for a score) or Graded.</param>
public sealed record GradebookCell(int ItemId, string Status, decimal? Score, bool IsLate, bool IsFlagged);

public sealed record GradebookRow(int StudentId, string Name, string? Lrn, IReadOnlyList<GradebookCell> Cells, decimal? Percent, int ToCheck, decimal? FinalGrade = null);

public sealed record SubjectGradebook(
    int AssignmentId, string SubjectName, string SectionLabel, IReadOnlyList<GradebookColumn> Columns, IReadOnlyList<GradebookRow> Rows, decimal? ClassAverage, int Failing);

/// <summary>
/// The teacher's gradebook of one subject: a row per student, a column per activity, quiz and task performance of the modules that
/// are posted, and the average (points earned divided by points possible of the graded work). Scores are changed here too, and
/// the teacher writes the final grade of each student next to the average.
/// </summary>
public sealed class GradebookService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider clock, WorkService work)
{
    public async Task<SubjectGradebook?> GetAsync(int teacherId, int assignmentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;

        var a = await db.TeachingAssignments.AsNoTracking().Where(x => x.Id == assignmentId && x.TeacherId == teacherId)
            .Select(x => new { x.Id, x.SectionId, x.SubjectId, Subject = x.Subject.Name, x.Section.GradeLevel, Strand = x.Section.Strand.Code, Section = x.Section.Name })
            .FirstOrDefaultAsync(ct);
        if (a is null)
        {
            return null;
        }

        var columns = await db.ModuleItems.AsNoTracking()
            .Where(i => i.Module.AssignmentId == assignmentId && i.Module.VisibleFromUtc != null && i.Module.VisibleFromUtc <= now)
            .OrderBy(i => i.Module.Position).ThenBy(i => i.Module.Id).ThenBy(i => i.Position).ThenBy(i => i.Id)
            .Select(i => new GradebookColumn(i.Id, i.Title, i.Kind, i.MaxScore, i.Module.Title))
            .ToListAsync(ct);
        var itemIds = columns.Select(c => c.ItemId).ToList();

        var students = await Roster.Enrollments(db, a.SectionId, a.SubjectId)
            .Select(e => new { e.StudentId, e.Student.FirstName, e.Student.LastName, e.Student.Lrn }).Distinct()
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName).ToListAsync(ct);
        var subs = await db.Submissions.AsNoTracking().Where(s => itemIds.Contains(s.ItemId)).Select(s => new { s.ItemId, s.StudentId, s.Status, s.Score, s.IsLate, s.IsFlagged }).ToListAsync(ct);
        var bySlot = subs.ToDictionary(s => (s.StudentId, s.ItemId));
        var finals = await db.FinalGrades.AsNoTracking().Where(g => g.AssignmentId == assignmentId).ToDictionaryAsync(g => g.StudentId, g => g.Grade, ct);

        var rows = new List<GradebookRow>();
        foreach (var st in students)
        {
            var cells = new List<GradebookCell>();
            decimal earned = 0, possible = 0;
            var toCheck = 0;
            foreach (var c in columns)
            {
                if (bySlot.TryGetValue((st.StudentId, c.ItemId), out var s))
                {
                    cells.Add(new GradebookCell(c.ItemId, s.Status, s.Score, s.IsLate, s.IsFlagged));
                    if (s.Score is { } score && s.Status == SubmissionStatus.Graded)
                    {
                        earned += score;
                        possible += c.Max;
                    }

                    if (s.Status == SubmissionStatus.Submitted)
                    {
                        toCheck++;
                    }
                }
                else
                {
                    cells.Add(new GradebookCell(c.ItemId, "NotStarted", null, false, false));
                }
            }

            rows.Add(new GradebookRow(st.StudentId, $"{st.LastName}, {st.FirstName}", st.Lrn, cells, possible <= 0 ? null : Math.Round(earned / possible * 100, 1), toCheck, finals.TryGetValue(st.StudentId, out var final) ? final : null));
        }

        var graded = rows.Where(r => r.Percent is not null).ToList();
        return new SubjectGradebook(a.Id, a.Subject, TeachingService.SectionLabel(a.GradeLevel, a.Strand, a.Section), columns, rows,
            graded.Count == 0 ? null : Math.Round(graded.Average(r => r.Percent!.Value), 1), graded.Count(r => r.Percent < GradeRules.PassingPercent));
    }

    /// <summary>
    /// Saves the final grades of the students of the subject (a number from 0 to 100, two decimals at most). A blank (null) removes the final grade of that
    /// student. Everything is checked first: a wrong box saves nothing at all.
    /// </summary>
    public async Task<ServiceResult> SetFinalGradesAsync(int teacherId, int assignmentId, IReadOnlyDictionary<int, decimal?> grades, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var a = await db.TeachingAssignments.AsNoTracking().Where(x => x.Id == assignmentId && x.TeacherId == teacherId)
            .Select(x => new { x.Id, x.SectionId, x.SubjectId }).FirstOrDefaultAsync(ct);
        if (a is null)
        {
            return ServiceResult.Fail("This is not your course.");
        }

        var roster = await Roster.Enrollments(db, a.SectionId, a.SubjectId).Select(e => new { e.StudentId, e.Student.FirstName, e.Student.LastName }).Distinct().ToListAsync(ct);
        var names = roster.ToDictionary(r => r.StudentId, r => $"{r.LastName}, {r.FirstName}");

        var errors = new List<string>();
        foreach (var (studentId, grade) in grades)
        {
            if (!names.TryGetValue(studentId, out var name))
            {
                errors.Add("One of the students is not in the course.");
            }
            else if (grade is { } g && (g < 0 || g > 100 || Math.Round(g, 2) != g))
            {
                errors.Add($"{name}: the final grade must be a number from 0 to 100, with two decimals at most.");
            }
        }

        if (errors.Count > 0)
        {
            return ServiceResult.Fail(errors.Distinct().ToList());
        }

        var existing = await db.FinalGrades.Where(g => g.AssignmentId == assignmentId).ToDictionaryAsync(g => g.StudentId, ct);
        var changed = 0;
        foreach (var (studentId, grade) in grades)
        {
            existing.TryGetValue(studentId, out var row);
            if (grade is null)
            {
                if (row is not null)
                {
                    db.FinalGrades.Remove(row);
                    changed++;
                }
            }
            else if (row is null)
            {
                db.FinalGrades.Add(new FinalGrade { AssignmentId = assignmentId, StudentId = studentId, Grade = grade.Value });
                changed++;
            }
            else if (row.Grade != grade.Value)
            {
                row.Grade = grade.Value;
                changed++;
            }
        }

        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok(changed == 0 ? "The final grades did not change." : changed == 1 ? "Saved the final grade of 1 student." : $"Saved the final grades of {changed} students.");
    }

    /// <summary>
    /// Changes the scores of one student. A blank (null) leaves the cell as it is; a score that did not change is skipped. The student is
    /// told about each score that changed, and the feedback the teacher wrote is kept.
    /// </summary>
    public async Task<ServiceResult> SetScoresAsync(int teacherId, int assignmentId, int studentId, IReadOnlyDictionary<int, decimal?> scores, CancellationToken ct = default)
    {
        var book = await GetAsync(teacherId, assignmentId, ct);
        if (book is null)
        {
            return ServiceResult.Fail("This is not your course.");
        }

        var row = book.Rows.FirstOrDefault(r => r.StudentId == studentId);
        if (row is null)
        {
            return ServiceResult.Fail("This student is not in the course.");
        }

        // Check every box first, so a wrong score in one box saves nothing at all.
        var errors = new List<string>();
        var toSave = new List<(GradebookColumn Column, decimal Score)>();
        foreach (var (itemId, score) in scores)
        {
            var column = book.Columns.FirstOrDefault(c => c.ItemId == itemId);
            if (column is null)
            {
                errors.Add("One of the columns is not part of this course.");
                continue;
            }

            if (score is null)
            {
                continue;
            }

            if (score < 0 || score > column.Max)
            {
                errors.Add($"{column.Title}: the score must be between 0 and {column.Max:0.##}.");
                continue;
            }

            var cell = row.Cells.First(c => c.ItemId == itemId);
            if (cell.Status == SubmissionStatus.Graded && cell.Score == Math.Round(score.Value, 2))
            {
                continue;
            }

            toSave.Add((column, score.Value));
        }

        if (errors.Count > 0)
        {
            return ServiceResult.Fail(errors);
        }

        var changed = 0;
        foreach (var (column, score) in toSave)
        {
            var result = await work.GradeAsync(teacherId, column.ItemId, studentId, score, null, ct, keepFeedback: true);
            if (result.Succeeded)
            {
                changed++;
            }
            else
            {
                errors.Add($"{column.Title}: {string.Join(" ", result.Errors)}");
            }
        }

        return errors.Count > 0 ? ServiceResult.Fail(errors) : ServiceResult.Ok(changed == 0 ? "Nothing changed." : $"{changed} score{(changed == 1 ? "" : "s")} saved.");
    }
}
