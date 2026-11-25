using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

public sealed record GradebookColumn(int ItemId, string Title, string Kind, decimal Max, string ModuleTitle)
{
    /// <summary>The short name of the column: the kind and its number among the work of that kind (A1, A2, Q1, TP1 ...). Moving the column does not change it.</summary>
    public string Code { get; init; } = "";

    /// <summary>True for the summative exam of the academic provider (the Summative Test); an exam the teacher added in the gradebook is a Term Examination.</summary>
    public bool Summative { get; init; }

    /// <summary>Round 46: a quiz or an exam that has questions: the system checks it by itself, so its score is not typed in the gradebook (only an essay is scored by the teacher).</summary>
    public bool AutoChecked { get; init; }

    /// <summary>True when the questions have an essay: the teacher gives that score, so the cell can be changed.</summary>
    public bool HasEssay { get; init; }

    /// <summary>The part of the grade this column counts in (Written/Oral Works, Performance Task, Summative Test or Term Examination).</summary>
    public GradePart Part => GradingSystem.PartOf(Kind, Summative);

    /// <summary>True when the teacher types the scores of this column in the gradebook.</summary>
    public bool Typed => !AutoChecked || HasEssay;
}

/// <param name="Status">NotStarted, InProgress, Submitted (waiting for a score) or Graded.</param>
public sealed record GradebookCell(int ItemId, string Status, decimal? Score, bool IsLate, bool IsFlagged, string? LateDecision = null);

/// <param name="Percent">The grade as it stands now (the weighted average of the parts that have graded work), or nothing when no work is graded yet.</param>
/// <param name="FinalGrade">The final grade that was sent to the registrar, when there is one.</param>
/// <param name="Grade">The grade worked out by the grading system: the percent of each part and the final grade (0 until every score is in).</param>
public sealed record GradebookRow(int StudentId, string Name, string? Lrn, IReadOnlyList<GradebookCell> Cells, decimal? Percent, int ToCheck, decimal? FinalGrade = null, Sex? Sex = null, GradeResult? Grade = null)
{
    /// <summary>The final grade the gradebook shows: 0 until every activity, quiz and exam has a score.</summary>
    public int? Final => Grade?.Final;
}

/// <summary>The gradebook of one course as the student sees it: the columns and the one row of the student (nothing to change).</summary>
public sealed record StudentBook(int AssignmentId, IReadOnlyList<GradebookColumn> Columns, GradebookRow Row)
{
    /// <summary>True while the teacher hides the grades of the course from the students: the columns are listed but there are no scores, no average and no final grade.</summary>
    public bool Hidden { get; init; }
}

public sealed record SubjectGradebook(
    int AssignmentId, string SubjectName, string SectionLabel, IReadOnlyList<GradebookColumn> Columns, IReadOnlyList<GradebookRow> Rows, decimal? ClassAverage, int Failing, bool GradesHidden = false)
{
    /// <summary>Students whose gradebook is not complete yet (a score is missing), so their final grade is 0.</summary>
    public int Incomplete => Rows.Count(r => r.Grade is { Complete: false, Total: > 0 });

    /// <summary>The two summary tables of the class (descriptors, passed/failed) with male, female and total, for the final grades of all the students.</summary>
    public (IReadOnlyList<GradingSystem.SummaryLine> Descriptor, IReadOnlyList<GradingSystem.SummaryLine> Remark, int Male, int Female, int Total) Summary =>
        GradingSystem.Summarize(Rows.Where(r => r.Final is not null).Select(r => ((decimal)r.Final!.Value, r.Sex)));
}

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
            .Select(x => new { x.Id, x.SectionId, x.SubjectId, Subject = x.Subject.Name, x.Section.GradeLevel, Strand = x.Section.Strand.Code, Section = x.Section.Name, x.GradesHidden })
            .FirstOrDefaultAsync(ct);
        if (a is null)
        {
            return null;
        }

        var columns = await GetColumnsAsync(db, assignmentId, now, ct);
        var itemIds = columns.Select(c => c.ItemId).ToList();

        var students = await Roster.Enrollments(db, a.SectionId, a.SubjectId)
            .Select(e => new { e.StudentId, e.Student.FirstName, e.Student.LastName, e.Student.Lrn, e.Student.Sex }).Distinct()
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName).ToListAsync(ct);
        var subs = await db.Submissions.AsNoTracking().Where(s => itemIds.Contains(s.ItemId)).Select(s => new SubmissionLine(s.ItemId, s.StudentId, s.Status, s.Score, s.IsLate, s.IsFlagged, s.LateDecision)).ToListAsync(ct);
        var bySlot = subs.ToDictionary(s => (s.StudentId, s.ItemId));
        var finals = await db.FinalGrades.AsNoTracking().Where(g => g.AssignmentId == assignmentId).ToDictionaryAsync(g => g.StudentId, g => g.Grade, ct);

        var rows = new List<GradebookRow>();
        foreach (var st in students)
        {
            var (cells, toCheck, grade) = BuildCells(columns, id => bySlot.TryGetValue((st.StudentId, id), out var s) ? s : null, forTeacher: true);
            rows.Add(new GradebookRow(st.StudentId, $"{st.LastName}, {st.FirstName}", st.Lrn, cells, grade.Running, toCheck, finals.TryGetValue(st.StudentId, out var final) ? final : null, st.Sex, grade));
        }

        // The tiles count the students whose gradebook is complete: a student with a score missing has the final grade 0 (it is not a result yet).
        var finished = rows.Where(r => r.Grade is { Complete: true }).ToList();
        return new SubjectGradebook(a.Id, a.Subject, TeachingService.SectionLabel(a.GradeLevel, a.Strand, a.Section), columns, rows,
            finished.Count == 0 ? null : Math.Round(finished.Average(r => (decimal)r.Final!.Value), 1), finished.Count(r => r.Final < GradeRules.PassingPercent), a.GradesHidden);
    }

    /// <summary>The cells of one student for the columns, the number of works that wait to be checked, and the grade the grading system works out.</summary>
    private static (List<GradebookCell> Cells, int ToCheck, GradeResult Grade) BuildCells(IReadOnlyList<GradebookColumn> columns, Func<int, SubmissionLine?> find, bool forTeacher)
    {
        var cells = new List<GradebookCell>();
        var works = new List<GradeWork>();
        var toCheck = 0;
        foreach (var c in columns)
        {
            var s = find(c.ItemId);
            decimal? score = null;
            if (s is not null)
            {
                cells.Add(new GradebookCell(c.ItemId, s.Status, s.Score, s.IsLate, forTeacher && s.IsFlagged, s.LateDecision));
                if (s.Score is { } value && s.Status == SubmissionStatus.Graded)
                {
                    score = value;
                }

                if (forTeacher && s.Status == SubmissionStatus.Submitted && s.LateDecision != LateDecisions.Declined)
                {
                    toCheck++;
                }
            }
            else
            {
                cells.Add(new GradebookCell(c.ItemId, "NotStarted", null, false, false));
            }

            works.Add(new GradeWork(c.Kind, c.Summative, c.Max, score));
        }

        return (cells, toCheck, GradingSystem.Compute(works));
    }

    /// <summary>What the gradebook needs of one submission (the slot of a student and a column).</summary>
    private sealed record SubmissionLine(int ItemId, int StudentId, string Status, decimal? Score, bool IsLate, bool IsFlagged, string? LateDecision);

    /// <summary>
    /// The button Hide from students of the gradebook: the grades of the course are hidden from the students (or shown again). The students keep seeing their work and their feedback, but not the
    /// scores of the gradebook, the average, the grade of the course and the final grade.
    /// </summary>
    public async Task<ServiceResult> SetGradesHiddenAsync(int teacherId, int assignmentId, bool hidden, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var changed = await db.TeachingAssignments.Where(a => a.Id == assignmentId && a.TeacherId == teacherId)
            .ExecuteUpdateAsync(set => set.SetProperty(a => a.GradesHidden, hidden), ct);
        if (changed == 0)
        {
            return ServiceResult.Fail("This is not your course.");
        }

        return ServiceResult.Ok(hidden ? "The grades of this course are hidden from the students." : "The students can see their grades of this course again.");
    }

    /// <summary>
    /// The gradebook of each course of a student, read only: the columns the teacher's gradebook has (the work that is open), the scores of the student, their average and the
    /// final grade the teacher wrote. The same numbers the teacher sees in the student's row.
    /// </summary>
    public async Task<IReadOnlyDictionary<int, StudentBook>> GetForStudentAsync(int studentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var student = await db.Students.AsNoTracking().Where(s => s.Id == studentId).Select(s => new { s.FirstName, s.LastName, s.Lrn, s.Sex }).FirstOrDefaultAsync(ct);
        if (student is null)
        {
            return new Dictionary<int, StudentBook>();
        }

        var assignmentIds = await Roster.AssignmentsOf(db, studentId).Select(a => a.Id).ToListAsync(ct);
        var hiddenIds = (await db.TeachingAssignments.AsNoTracking().Where(a => assignmentIds.Contains(a.Id) && a.GradesHidden).Select(a => a.Id).ToListAsync(ct)).ToHashSet();
        var finals = await db.FinalGrades.AsNoTracking().Where(g => g.StudentId == studentId && assignmentIds.Contains(g.AssignmentId)).ToDictionaryAsync(g => g.AssignmentId, g => g.Grade, ct);
        var books = new Dictionary<int, StudentBook>();
        foreach (var assignmentId in assignmentIds)
        {
            var columns = await GetColumnsAsync(db, assignmentId, now, ct, studentView: true);
            if (hiddenIds.Contains(assignmentId))
            {
                // the teacher hides the grades: the columns are there, the scores, the average and the final grade are not
                books[assignmentId] = new StudentBook(assignmentId, columns, new GradebookRow(studentId, $"{student.LastName}, {student.FirstName}", student.Lrn,
                    [.. columns.Select(c => new GradebookCell(c.ItemId, "NotStarted", null, false, false))], null, 0, null, student.Sex)) { Hidden = true };
                continue;
            }

            var itemIds = columns.Select(c => c.ItemId).ToList();
            var mine = await db.Submissions.AsNoTracking().Where(s => s.StudentId == studentId && itemIds.Contains(s.ItemId))
                .Select(s => new SubmissionLine(s.ItemId, s.StudentId, s.Status, s.Score, s.IsLate, false, s.LateDecision)).ToDictionaryAsync(s => s.ItemId, ct);

            var (cells, _, grade) = BuildCells(columns, id => mine.GetValueOrDefault(id), forTeacher: false);
            books[assignmentId] = new StudentBook(assignmentId, columns, new GradebookRow(studentId, $"{student.LastName}, {student.FirstName}", student.Lrn, cells,
                grade.Running, 0, finals.TryGetValue(assignmentId, out var final) ? final : null, student.Sex, grade));
        }

        return books;
    }

    /// <summary>
    /// The columns in the order the teacher put them (the ones that were never moved come last, module by module). The codes (A1, Q1 ...) count the work of each kind in the
    /// order of the modules, so a column that is moved keeps its name.
    /// </summary>
    private static async Task<List<GradebookColumn>> GetColumnsAsync(AppDbContext db, int assignmentId, DateTime now, CancellationToken ct, bool studentView = false)
    {
        var rows = await db.ModuleItems.AsNoTracking()
            .Where(i => i.Module.AssignmentId == assignmentId && i.Module.VisibleFromUtc != null && i.Module.VisibleFromUtc <= now
                        && (!studentView || i.VisibleFromUtc == null || i.VisibleFromUtc <= now))   // a student does not see the work that is not open yet
            .OrderBy(i => i.Module.Position).ThenBy(i => i.Module.Id).ThenBy(i => i.Position).ThenBy(i => i.Id)
            .Select(i => new
            {
                i.Id, i.Title, i.Kind, i.MaxScore, ModuleTitle = i.Module.Title, i.GradebookOrder, Summative = i.MaterialItemId != null,
                HasQuestions = i.Questions.Any(), HasEssay = i.Questions.Any(q => q.Kind == QuestionKinds.Essay),
            })
            .ToListAsync(ct);

        // E is a Term Examination the teacher added in the gradebook; SE (the Summative Test) is the summative exam of the academic provider. A course has ONE summative exam, so its column is just "SE" (no number), and an
        // older second copy of it (what used to be SE2) is not a column of the gradebook.
        var firstSummative = rows.FirstOrDefault(r => r.Kind == ItemKinds.Exam && r.Summative)?.Id;
        rows = [.. rows.Where(r => !(r.Kind == ItemKinds.Exam && r.Summative) || r.Id == firstSummative)];
        var counters = new Dictionary<string, int>();
        var columns = new List<(GradebookColumn Column, int Order, int Index)>();
        for (var n = 0; n < rows.Count; n++)
        {
            var r = rows[n];
            var summative = r.Kind == ItemKinds.Exam && r.Summative;
            var letters = summative ? "SE" : r.Kind == ItemKinds.Exam ? "TE" : ItemKinds.Code(r.Kind);
            counters[letters] = counters.GetValueOrDefault(letters) + 1;
            columns.Add((new GradebookColumn(r.Id, r.Title, r.Kind, r.MaxScore, r.ModuleTitle)
            {
                Code = summative ? "SE" : $"{letters}{counters[letters]}",
                Summative = summative,
                // a quiz or an exam with questions is checked by the system (an exam with no questions is only a column to type scores in)
                AutoChecked = ItemKinds.IsQuizLike(r.Kind) && r.HasQuestions,
                HasEssay = r.HasEssay,
            }, r.GradebookOrder, n));
        }

        return columns.OrderBy(c => c.Order == 0 ? 1 : 0).ThenBy(c => c.Order).ThenBy(c => c.Index).Select(c => c.Column).ToList();
    }

    /// <summary>
    /// Moves a column of the gradebook one place to the left (<paramref name="delta"/> -1) or to the right (+1). Every column gets its place, so the order is kept.
    /// The students do not see the order: it is only how the teacher's gradebook, and the Excel file, list the work.
    /// </summary>
    public async Task<ServiceResult> MoveColumnAsync(int teacherId, int assignmentId, int itemId, int delta, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await db.TeachingAssignments.AnyAsync(x => x.Id == assignmentId && x.TeacherId == teacherId, ct))
        {
            return ServiceResult.Fail("This is not your course.");
        }

        var columns = await GetColumnsAsync(db, assignmentId, clock.GetUtcNow().UtcDateTime, ct);
        var from = columns.FindIndex(c => c.ItemId == itemId);
        if (from < 0)
        {
            return ServiceResult.Fail("This column is not part of the gradebook.");
        }

        var to = Math.Clamp(from + Math.Sign(delta), 0, columns.Count - 1);
        if (to == from)
        {
            return ServiceResult.Ok("It is already there.");
        }

        (columns[from], columns[to]) = (columns[to], columns[from]);
        var places = columns.Select((c, n) => (c.ItemId, Place: n + 1)).ToDictionary(x => x.ItemId, x => x.Place);
        var items = await db.ModuleItems.Where(i => places.Keys.Contains(i.Id)).ToListAsync(ct);
        foreach (var item in items)
        {
            item.GradebookOrder = places[item.Id];
        }

        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok("Moved.");
    }

    /// <summary>
    /// Round 46: the button Send final grades. The final grades are the ones the gradebook worked out (the Final grade column): they are not typed. They go to the registrar (and are kept for the students):
    /// a student whose gradebook is not complete has the final grade 0, as in the gradebook.
    /// </summary>
    public async Task<ServiceResult> SendFinalGradesAsync(int teacherId, int assignmentId, CancellationToken ct = default)
    {
        var book = await GetAsync(teacherId, assignmentId, ct);
        if (book is null)
        {
            return ServiceResult.Fail("This is not your course.");
        }

        var grades = book.Rows.Where(r => r.Final is not null).ToDictionary(r => r.StudentId, r => (decimal?)r.Final!.Value);
        if (grades.Count == 0)
        {
            return ServiceResult.Fail("There is no final grade to send yet: the gradebook has no work.");
        }

        var sent = await SetFinalGradesAsync(teacherId, assignmentId, grades, ct);
        if (!sent.Succeeded)
        {
            return sent;
        }

        var incomplete = book.Rows.Count(r => r.Grade is { Complete: false, Total: > 0 });
        var tail = incomplete == 0 ? "" : $" {incomplete} student{(incomplete == 1 ? " has" : "s have")} a score missing, so the final grade sent is 0.";
        return ServiceResult.Ok(sent.Message + tail);
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
        return ServiceResult.Ok(changed == 0 ? "The final grades did not change." : changed == 1 ? "Sent the final grade of 1 student." : $"Sent the final grades of {changed} students.");
    }

    /// <summary>
    /// Round 46: Import class grades. The file is the Excel the gradebook exports (the sheet with the columns Student and LRN and then the code of each column: A1, Q1, TP1, TE1 ...); the teacher
    /// types the scores in it and imports it back. A student is found by the LRN (or by the name "Last, First"), a column by its code. The columns that the system checks by itself (a quiz, an
    /// exam with questions) are left as they are. An empty cell leaves the score as it is. A wrong score (over the highest score, not a number) is told and nothing of that student is saved.
    /// </summary>
    public async Task<ServiceResult> ImportScoresAsync(int teacherId, int assignmentId, byte[] file, CancellationToken ct = default)
    {
        var book = await GetAsync(teacherId, assignmentId, ct);
        if (book is null)
        {
            return ServiceResult.Fail("This is not your course.");
        }

        IReadOnlyList<XlsxSheetData> sheets;
        try
        {
            sheets = XlsxReader.Read(file);
        }
        catch (InvalidDataException e)
        {
            return ServiceResult.Fail(e.Message);
        }

        // the sheet with the header row (the cells Student and LRN)
        XlsxSheetData? sheet = null;
        var headerRow = 0;
        foreach (var candidate in sheets)
        {
            for (var r = 1; r <= Math.Min(candidate.Rows.Count, 12); r++)
            {
                if (candidate.Cell(r, 1).Trim().Equals("Student", StringComparison.OrdinalIgnoreCase) && candidate.Cell(r, 2).Trim().Equals("LRN", StringComparison.OrdinalIgnoreCase))
                {
                    sheet = candidate;
                    headerRow = r;
                    break;
                }
            }

            if (sheet is not null)
            {
                break;
            }
        }

        if (sheet is null)
        {
            return ServiceResult.Fail("This is not the Excel of the gradebook: the first columns must be Student and LRN. Use Export to Excel and type the scores in that file.");
        }

        var columnOfCode = new Dictionary<int, GradebookColumn>();
        var skipped = new List<string>();
        for (var c = 3; c <= sheet.Rows[headerRow - 1].Count; c++)
        {
            var code = sheet.Cell(headerRow, c).Trim();
            var column = book.Columns.FirstOrDefault(x => x.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
            if (column is null)
            {
                continue;
            }

            if (column.Typed)
            {
                columnOfCode[c] = column;
            }
            else
            {
                skipped.Add(column.Code);
            }
        }

        if (columnOfCode.Count == 0)
        {
            return ServiceResult.Fail(skipped.Count > 0
                ? "The columns of this file are quizzes and exams that the system checks by itself, so there is nothing to import."
                : "No column of this file has the code of a column of the gradebook (A1, Q1, TP1, TE1 ...). Use Export to Excel and type the scores in that file.");
        }

        var errors = new List<string>();
        var students = 0;
        var scoresSaved = 0;
        for (var r = headerRow + 1; r <= sheet.Rows.Count; r++)
        {
            var lrn = new string(sheet.Cell(r, 2).Where(char.IsAsciiDigit).ToArray());
            var name = sheet.Cell(r, 1).Trim();
            var student = (lrn.Length > 0 ? book.Rows.FirstOrDefault(x => x.Lrn == lrn) : null)
                          ?? (name.Length > 0 ? book.Rows.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) : null);
            if (student is null)
            {
                if (lrn.Length > 0 || name.Length > 0)
                {
                    // the rows under the table (the labels of the Highest score row ...) are not students: only a row with an LRN is reported
                    if (lrn.Length > 0)
                    {
                        errors.Add($"Row {r}: the student with the LRN {lrn} is not in this course.");
                    }
                }

                continue;
            }

            var scores = new Dictionary<int, decimal?>();
            var bad = false;
            foreach (var (c, column) in columnOfCode)
            {
                var text = sheet.Cell(r, c).Trim();
                if (text.Length == 0)
                {
                    continue;
                }

                if (!decimal.TryParse(text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var value))
                {
                    errors.Add($"{student.Name}, {column.Code}: \"{text}\" is not a number.");
                    bad = true;
                    continue;
                }

                scores[column.ItemId] = value;
            }

            if (bad || scores.Count == 0)
            {
                continue;
            }

            var saved = await SetScoresAsync(teacherId, assignmentId, student.StudentId, scores, ct);
            if (saved.Succeeded)
            {
                students++;
                scoresSaved += scores.Count;
            }
            else
            {
                errors.AddRange(saved.Errors.Select(e => $"{student.Name}: {e}"));
            }
        }

        if (students == 0 && errors.Count > 0)
        {
            return ServiceResult.Fail(errors.Distinct().Take(8).ToList());
        }

        var message = students == 0 ? "No score was found to import." : $"Imported the scores of {students} student{(students == 1 ? "" : "s")}.";
        if (errors.Count > 0)
        {
            message += $" {errors.Count} problem{(errors.Count == 1 ? "" : "s")}: {string.Join(" ", errors.Distinct().Take(3))}";
        }

        if (skipped.Count > 0)
        {
            message += $" ({string.Join(", ", skipped)} {(skipped.Count == 1 ? "is" : "are")} checked by the system and was not imported.)";
        }

        return ServiceResult.Ok(message);
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

            if (!column.Typed)
            {
                errors.Add($"{column.Title}: a quiz or an exam is checked by the system, so its score can't be typed here. (Only an essay is scored by the teacher.)");
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
