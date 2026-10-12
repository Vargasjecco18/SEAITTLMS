using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Components.Shared;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

// ---------------------------------------------------------------------- what the coordinator types

public sealed class EvaluationInput
{
    public string Title { get; set; } = "";
    public string? Instructions { get; set; }
    public DateTime? DueAtUtc { get; set; }
}

public sealed class EvaluationQuestionInput
{
    public string Text { get; set; } = "";
    public string Kind { get; set; } = EvaluationQuestionKinds.Likert;
    public string? Category { get; set; }

    /// <summary>The options of a ranking or select-all question.</summary>
    public List<string> Options { get; set; } = [];
}

// ---------------------------------------------------------------------- what the coordinator sees

public static class EvaluationStates
{
    public const string Draft = "Draft";
    public const string Open = "Open";
    public const string Closed = "Closed";
}

/// <param name="Expected">How many answers there should be: the students of each course that is evaluated (a student with five courses counts five).</param>
public sealed record EvaluationRow(
    int Id, string Title, string State, DateTime? SentAtUtc, DateTime? DueAtUtc, int Questions, int Courses, int Expected, int Responses)
{
    public int RatePercent => Expected == 0 ? 0 : (int)Math.Round(Responses * 100.0 / Expected);
}

public sealed record EvaluationQuestionView(int Id, int Position, string Text, string Kind, string? Category, IReadOnlyList<string>? Options = null);

/// <summary>A course (a teacher, a section) that an evaluation is about, with how many students answered and their average.</summary>
public sealed record EvaluationCourseRow(
    int AssignmentId, int TeacherId, string TeacherName, string SubjectName, string SectionLabel, int Expected, int Responses, decimal? Average)
{
    public int RatePercent => Expected == 0 ? 0 : (int)Math.Round(Responses * 100.0 / Expected);
}

public sealed record EvaluationDetail(
    int Id, string Title, string? Instructions, string State, DateTime? SentAtUtc, DateTime? DueAtUtc, string? TermLabel,
    IReadOnlyList<EvaluationQuestionView> Questions, IReadOnlyList<EvaluationCourseRow> Courses);

/// <summary>A course of a teacher that the coordinator can send an evaluation about.</summary>
public sealed record EvaluationChoice(int AssignmentId, int TeacherId, string TeacherName, string SubjectName, string SectionLabel, string? Schedule, int Students);

// ---------------------------------------------------------------------- what the student does

public sealed record StudentEvaluationTask(
    int EvaluationId, string Title, int AssignmentId, string SubjectName, string TeacherName, DateTime DueAtUtc, bool Answered, bool Closed);

public sealed record EvaluationForm(
    int EvaluationId, string Title, string? Instructions, int AssignmentId, string SubjectName, string TeacherName, string SectionLabel, DateTime DueAtUtc,
    bool Answered, bool Closed, IReadOnlyList<EvaluationQuestionView> Questions);

public sealed class EvaluationAnswerInput
{
    public int QuestionId { get; set; }

    /// <summary>1 to 5 for a Likert, performance or frequency question.</summary>
    public int? Rating { get; set; }

    public string? Comment { get; set; }

    /// <summary>"Yes" or "No" (binary); the numbers of the options in the order of the student, like "2,0,1" (ranking); the numbers of the options ticked, like "0,2" (select-all).</summary>
    public string? Selection { get; set; }
}

// ---------------------------------------------------------------------- analytics

public sealed record QuestionAverage(int QuestionId, string Text, string? Category, decimal? Average, int Answers);

/// <summary>One option of a binary, ranking or select-all question with how it did: the number of answers and a value (the share in percent, or the average place).</summary>
public sealed record OptionResult(string Label, int Count, decimal? Value);

/// <summary>
/// The result of a binary, ranking or select-all question. Binary and select-all: <see cref="OptionResult.Value"/> is the share of the answers in percent.
/// Ranking: <see cref="OptionResult.Value"/> is the average place (1 is the best) and <see cref="OptionResult.Count"/> how many put it first.
/// </summary>
public sealed record QuestionResult(int QuestionId, string Text, string? Category, string Kind, int Answers, IReadOnlyList<OptionResult> Options);

public sealed record TeacherResult(
    int TeacherId, string TeacherName, int Courses, int Expected, int Responses, decimal? Average, int Rank)
{
    public int RatePercent => Expected == 0 ? 0 : (int)Math.Round(Responses * 100.0 / Expected);
}

public sealed record EvaluationTrend(int EvaluationId, string Title, DateTime SentAtUtc, int Responses, int Expected, decimal? Average)
{
    public int RatePercent => Expected == 0 ? 0 : (int)Math.Round(Responses * 100.0 / Expected);
}

public sealed record EvaluationAnalytics(
    IReadOnlyList<EvaluationTrend> Evaluations, int? EvaluationId, string? Title, int Expected, int Responses, decimal? Average,
    IReadOnlyList<int> Distribution, IReadOnlyList<QuestionAverage> Questions, IReadOnlyList<QuestionAverage> Categories, IReadOnlyList<TeacherResult> Teachers,
    IReadOnlyList<QuestionResult>? OtherQuestions = null)
{
    public int RatePercent => Expected == 0 ? 0 : (int)Math.Round(Responses * 100.0 / Expected);
}

public sealed record EvaluationComment(string Text, string SubjectName, string SectionLabel, string Question, DateTime AtUtc);

public sealed record TeacherDetail(
    int TeacherId, string TeacherName, decimal? Average, int Responses, int Expected, IReadOnlyList<EvaluationCourseRow> Courses,
    IReadOnlyList<QuestionAverage> Questions, IReadOnlyList<int> Distribution, IReadOnlyList<EvaluationComment> Comments,
    IReadOnlyList<QuestionResult>? OtherQuestions = null);

/// <summary>
/// The teacher evaluation: the academic coordinator makes a questionnaire, sends it to the students of the courses they choose with a
/// due date, and reads the answers. A student answers once for each teacher. The answers are anonymous: nobody sees who wrote
/// what, the coordinator only sees who has not answered yet. Every change checks again, in the database, who is asking.
/// </summary>
public sealed class EvaluationService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider clock, NotificationService notifications)
{
    private const string CoordinatorOnly = "Only the academic coordinator runs the teacher evaluation.";
    private const int MaxQuestions = 40;

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>The questions a new evaluation starts with, in the areas a teacher is usually looked at. The coordinator changes, adds and removes them.</summary>
    public static readonly IReadOnlyList<EvaluationQuestionInput> DefaultQuestions =
    [
        new() { Category = "Knowledge of the subject", Text = "The teacher knows the subject well and explains it clearly." },
        new() { Category = "Knowledge of the subject", Text = "The teacher answers my questions in a way I understand." },
        new() { Category = "Teaching", Text = "The lessons and activities help me learn." },
        new() { Category = "Teaching", Text = "The teacher gives clear instructions for activities, quizzes and tasks." },
        new() { Category = "Teaching", Text = "The teacher checks my work and gives my scores on time." },
        new() { Category = "Classroom management", Kind = EvaluationQuestionKinds.Frequency, Text = "How often does the class start and end on time?" },
        new() { Category = "Classroom management", Text = "The teacher is fair and treats every student with respect." },
        new() { Category = "Classroom management", Text = "The teacher keeps the class orderly." },
        new() { Category = "Support", Text = "The teacher is easy to approach and helps students who need help." },
        new() { Category = "Support", Text = "The teacher makes the lessons interesting." },
        new() { Category = "Overall", Kind = EvaluationQuestionKinds.Performance, Text = "Overall, how would you rate this teacher?" },
        new() { Category = "Overall", Kind = EvaluationQuestionKinds.Binary, Text = "Would you recommend this teacher to other students?" },
        new()
        {
            Category = "Overall", Kind = EvaluationQuestionKinds.Ranking, Text = "Rank what helps you learn the most, from the first (best) to the last.",
            Options = ["Explanations in class", "Activities and worksheets", "Quizzes", "Task performances (projects)"],
        },
        new()
        {
            Category = "Comments", Kind = EvaluationQuestionKinds.SelectAll, Text = "What does this teacher do well? Select all that apply.",
            Options = ["Explains clearly", "Gives helpful feedback", "Makes the lessons interesting", "Is fair to everybody", "Is patient", "Is always prepared"],
        },
        new() { Category = "Comments", Kind = EvaluationQuestionKinds.Comment, Text = "What does this teacher do well?" },
        new() { Category = "Comments", Kind = EvaluationQuestionKinds.Comment, Text = "What should this teacher do better?" },
    ];

    public static string StateOf(DateTime? sentAtUtc, DateTime? dueAtUtc, DateTime now) =>
        sentAtUtc is null ? EvaluationStates.Draft : dueAtUtc is { } due && due <= now ? EvaluationStates.Closed : EvaluationStates.Open;

    // ======================================================================= the coordinator: reading

    public async Task<IReadOnlyList<EvaluationRow>> ListAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = Now;

        var rows = await db.TeacherEvaluations.AsNoTracking()
            .OrderByDescending(e => e.SentAtUtc == null).ThenByDescending(e => e.SentAtUtc).ThenByDescending(e => e.Id)
            .Select(e => new { e.Id, e.Title, e.SentAtUtc, e.DueAtUtc, Questions = e.Questions.Count, Courses = e.Targets.Count, Responses = e.Responses.Count })
            .ToListAsync(ct);

        var result = new List<EvaluationRow>();
        foreach (var r in rows)
        {
            var expected = r.SentAtUtc is null ? 0 : await ExpectedAsync(db, r.Id, ct);
            result.Add(new EvaluationRow(r.Id, r.Title, StateOf(r.SentAtUtc, r.DueAtUtc, now), r.SentAtUtc, r.DueAtUtc, r.Questions, r.Courses, expected, r.Responses));
        }

        return result;
    }

    public async Task<EvaluationDetail?> GetAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var e = await db.TeacherEvaluations.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.Id, x.Title, x.Instructions, x.SentAtUtc, x.DueAtUtc, Term = x.Term == null ? null : x.Term.Name + " · " + x.Term.SchoolYear.Name })
            .FirstOrDefaultAsync(ct);
        if (e is null)
        {
            return null;
        }

        var questions = (await db.EvaluationQuestions.AsNoTracking().Where(q => q.EvaluationId == id).OrderBy(q => q.Position).ThenBy(q => q.Id).ToListAsync(ct))
            .Select(ViewOf).ToList();

        var courses = e.SentAtUtc is null ? [] : await CourseRowsAsync(db, id, null, ct);
        return new EvaluationDetail(e.Id, e.Title, e.Instructions, StateOf(e.SentAtUtc, e.DueAtUtc, Now), e.SentAtUtc, e.DueAtUtc, e.Term, questions, courses);
    }

    /// <summary>The courses of the teachers of the current school year that have students, to choose from when sending.</summary>
    public async Task<IReadOnlyList<EvaluationChoice>> GetChoicesAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var yearId = await MaterialService.CurrentYearIdAsync(db, ct);

        var rows = await db.TeachingAssignments.AsNoTracking().Where(a => a.Section.SchoolYearId == yearId)
            .OrderBy(a => a.Teacher.LastName).ThenBy(a => a.Teacher.FirstName).ThenBy(a => a.Subject.Name).ThenBy(a => a.Section.Name)
            .Select(a => new
            {
                a.Id, a.TeacherId, a.Teacher.FirstName, a.Teacher.MiddleName, a.Teacher.LastName, a.Teacher.NameExtension, SubjectName = a.Subject.Name, a.SectionId, a.SubjectId,
                a.Section.GradeLevel, StrandCode = a.Section.Strand.Code, SectionName = a.Section.Name, Schedule = a.Section.Schedule != null ? a.Section.Schedule.Code : null,
            })
            .ToListAsync(ct);

        var result = new List<EvaluationChoice>();
        foreach (var r in rows)
        {
            var students = await Roster.StudentIds(db, r.SectionId, r.SubjectId).CountAsync(ct);
            if (students > 0)
            {
                result.Add(new EvaluationChoice(
                    r.Id, r.TeacherId, UserDirectory.FullName(r.FirstName, r.MiddleName, r.LastName, r.NameExtension), r.SubjectName,
                    TeachingService.SectionLabel(r.GradeLevel, r.StrandCode, r.SectionName), r.Schedule, students));
            }
        }

        return result;
    }

    /// <summary>The names of the students of a course who have not answered an evaluation yet.</summary>
    public async Task<IReadOnlyList<string>> GetNotAnsweredAsync(int evaluationId, int assignmentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var a = await db.EvaluationTargets.AsNoTracking().Where(t => t.EvaluationId == evaluationId && t.AssignmentId == assignmentId)
            .Select(t => new { t.Assignment.SectionId, t.Assignment.SubjectId }).FirstOrDefaultAsync(ct);
        if (a is null)
        {
            return [];
        }

        var answered = db.EvaluationResponses.Where(r => r.EvaluationId == evaluationId && r.AssignmentId == assignmentId).Select(r => r.StudentId);
        return (await db.Students.AsNoTracking()
            .Where(s => Roster.StudentIds(db, a.SectionId, a.SubjectId).Contains(s.Id) && !answered.Contains(s.Id))
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .Select(s => new { s.FirstName, s.MiddleName, s.LastName, s.NameExtension }).ToListAsync(ct))
            .Select(s => UserDirectory.FullName(s.FirstName, s.MiddleName, s.LastName, s.NameExtension)).ToList();
    }

    // ======================================================================= the coordinator: making and sending

    /// <summary>A new draft with the usual questions. Returns its id.</summary>
    public async Task<ServiceResult> CreateAsync(int userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsCoordinatorAsync(db, userId, ct))
        {
            return ServiceResult.Fail(CoordinatorOnly);
        }

        var term = await db.Terms.AsNoTracking().Where(t => t.IsCurrent).Select(t => t.Name + " · " + t.SchoolYear.Name).FirstOrDefaultAsync(ct);
        var evaluation = new TeacherEvaluation
        {
            Title = term is null ? "Teacher evaluation" : $"Teacher evaluation · {term}",
            Instructions = "Answer honestly. Your answers are anonymous: your teachers will not know who wrote what.",
            CreatedByUserId = userId,
        };
        var position = 0;
        foreach (var q in DefaultQuestions)
        {
            evaluation.Questions.Add(new EvaluationQuestion
            {
                Position = ++position, Text = q.Text, Kind = q.Kind, Category = q.Category,
                OptionsText = EvaluationQuestionKinds.HasOptions(q.Kind) ? string.Join('\n', q.Options) : null,
            });
        }

        db.TeacherEvaluations.Add(evaluation);
        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok("The evaluation was made with the usual questions. Change them, then send it.", evaluation.Id);
    }

    public async Task<ServiceResult> SaveAsync(int userId, int id, EvaluationInput input, CancellationToken ct = default)
    {
        var errors = new List<string>();
        var title = (input.Title ?? "").Trim();
        if (title.Length is 0 or > 200)
        {
            errors.Add("Write a title (up to 200 characters).");
        }

        if (input.Instructions is { Length: > 2000 })
        {
            errors.Add("The instructions can have at most 2,000 characters.");
        }

        if (errors.Count > 0)
        {
            return ServiceResult.Fail(errors);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsCoordinatorAsync(db, userId, ct))
        {
            return ServiceResult.Fail(CoordinatorOnly);
        }

        var evaluation = await db.TeacherEvaluations.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (evaluation is null)
        {
            return ServiceResult.Fail("This evaluation was deleted.");
        }

        if (evaluation.SentAtUtc is not null)
        {
            // Sent: the students are answering. Only a later due date can be given (or the same).
            if (input.DueAtUtc is not { } due || due <= Now)
            {
                return ServiceResult.Fail("Choose a due date in the future. To end it now, use Close now.");
            }

            evaluation.DueAtUtc = due;
        }

        evaluation.Title = title;
        evaluation.Instructions = string.IsNullOrWhiteSpace(input.Instructions) ? null : input.Instructions.Trim();
        if (evaluation.SentAtUtc is null)
        {
            evaluation.DueAtUtc = input.DueAtUtc;
        }

        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok("Saved.");
    }

    public async Task<ServiceResult> SaveQuestionsAsync(int userId, int id, IReadOnlyList<EvaluationQuestionInput> questions, CancellationToken ct = default)
    {
        var errors = new List<string>();
        for (var n = 0; n < questions.Count; n++)
        {
            var q = questions[n];
            if (string.IsNullOrWhiteSpace(q.Text) || q.Text.Trim().Length > 500)
            {
                errors.Add($"Question {n + 1}: write the question (up to 500 characters).");
            }

            if (!EvaluationQuestionKinds.All.Contains(q.Kind))
            {
                errors.Add($"Question {n + 1}: choose how the students answer.");
            }

            if (q.Category is { Length: > 60 })
            {
                errors.Add($"Question {n + 1}: the area can have at most 60 characters.");
            }

            if (EvaluationQuestionKinds.HasOptions(q.Kind))
            {
                var options = CleanOptions(q.Options);
                if (options.Count is < EvaluationQuestionKinds.MinOptions or > EvaluationQuestionKinds.MaxOptions)
                {
                    errors.Add($"Question {n + 1}: write {EvaluationQuestionKinds.MinOptions} to {EvaluationQuestionKinds.MaxOptions} different options.");
                }
                else if (options.Any(o => o.Length > 120))
                {
                    errors.Add($"Question {n + 1}: an option can have at most 120 characters.");
                }
            }
        }

        if (questions.Count == 0 || !questions.Any(q => EvaluationQuestionKinds.IsScale(q.Kind)))
        {
            errors.Add("Keep at least one question with a 5-point scale (Likert, performance or frequency), so the teachers can be compared.");
        }

        if (questions.Count > MaxQuestions)
        {
            errors.Add($"An evaluation can have at most {MaxQuestions} questions.");
        }

        if (errors.Count > 0)
        {
            return ServiceResult.Fail(errors);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsCoordinatorAsync(db, userId, ct))
        {
            return ServiceResult.Fail(CoordinatorOnly);
        }

        var evaluation = await db.TeacherEvaluations.AsNoTracking().Where(e => e.Id == id).Select(e => new { e.Id, e.SentAtUtc }).FirstOrDefaultAsync(ct);
        if (evaluation is null)
        {
            return ServiceResult.Fail("This evaluation was deleted.");
        }

        if (evaluation.SentAtUtc is not null)
        {
            return ServiceResult.Fail("The questions can't be changed once the evaluation was sent: the students are answering them.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.EvaluationQuestions.Where(q => q.EvaluationId == id).ExecuteDeleteAsync(ct);
        var position = 0;
        foreach (var q in questions)
        {
            db.EvaluationQuestions.Add(new EvaluationQuestion
            {
                EvaluationId = id, Position = ++position, Text = q.Text.Trim(), Kind = q.Kind,
                Category = string.IsNullOrWhiteSpace(q.Category) ? null : q.Category.Trim(),
                OptionsText = EvaluationQuestionKinds.HasOptions(q.Kind) ? string.Join('\n', CleanOptions(q.Options)) : null,
            });
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ServiceResult.Ok($"{questions.Count} question{(questions.Count == 1 ? "" : "s")} saved.");
    }

    /// <summary>
    /// Sends the evaluation to the students of the chosen courses: each of them is told, and finds one questionnaire for each teacher
    /// in their dashboard and at "Teacher evaluation". The due date is the last time they can answer.
    /// </summary>
    public async Task<ServiceResult> SendAsync(int userId, int id, IReadOnlyCollection<int> assignmentIds, DateTime dueAtUtc, CancellationToken ct = default)
    {
        if (assignmentIds.Count == 0)
        {
            return ServiceResult.Fail("Choose the teachers' courses it is about.");
        }

        var now = Now;
        if (dueAtUtc <= now.AddMinutes(5))
        {
            return ServiceResult.Fail("Choose a due date in the future.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsCoordinatorAsync(db, userId, ct))
        {
            return ServiceResult.Fail(CoordinatorOnly);
        }

        var evaluation = await db.TeacherEvaluations.Include(e => e.Questions).FirstOrDefaultAsync(e => e.Id == id, ct);
        if (evaluation is null)
        {
            return ServiceResult.Fail("This evaluation was deleted.");
        }

        if (evaluation.SentAtUtc is not null)
        {
            return ServiceResult.Fail("This evaluation was sent already.");
        }

        if (!evaluation.Questions.Any(q => EvaluationQuestionKinds.IsScale(q.Kind)))
        {
            return ServiceResult.Fail("Add at least one question with a 5-point scale (Likert, performance or frequency).");
        }

        var yearId = await MaterialService.CurrentYearIdAsync(db, ct);
        var targets = await db.TeachingAssignments.AsNoTracking()
            .Where(a => assignmentIds.Contains(a.Id) && a.Section.SchoolYearId == yearId)
            .Select(a => new { a.Id, a.SectionId, a.SubjectId })
            .ToListAsync(ct);

        var students = new HashSet<int>();
        var courses = new List<int>();
        foreach (var t in targets)
        {
            var ids = await db.Students.AsNoTracking()
                .Where(s => s.Status == StudentStatus.Active && s.User.IsActive && Roster.StudentIds(db, t.SectionId, t.SubjectId).Contains(s.Id))
                .Select(s => s.UserId).ToListAsync(ct);
            if (ids.Count == 0)
            {
                continue;
            }

            courses.Add(t.Id);
            students.UnionWith(ids);
        }

        if (courses.Count == 0)
        {
            return ServiceResult.Fail("None of the courses you chose has students. Choose courses that have students.");
        }

        evaluation.SentAtUtc = now;
        evaluation.DueAtUtc = dueAtUtc;
        evaluation.TermId = await db.Terms.AsNoTracking().Where(t => t.IsCurrent).Select(t => (int?)t.Id).FirstOrDefaultAsync(ct);
        foreach (var assignmentId in courses)
        {
            db.EvaluationTargets.Add(new EvaluationTarget { EvaluationId = id, AssignmentId = assignmentId });
        }

        await db.SaveChangesAsync(ct);

        await notifications.AddForManyAsync(
            students, "evaluation", $"Teacher evaluation: {evaluation.Title}",
            $"Please rate your teachers before {PhilippineTime.FormatDateTime(dueAtUtc)}. Your answers are anonymous.", "student/evaluations", ct);

        return ServiceResult.Ok($"Sent to {students.Count} student{(students.Count == 1 ? "" : "s")} for {courses.Count} course{(courses.Count == 1 ? "" : "s")} of teachers.", id);
    }

    /// <summary>Ends the evaluation now: the students can't answer any more.</summary>
    public async Task<ServiceResult> CloseNowAsync(int userId, int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsCoordinatorAsync(db, userId, ct))
        {
            return ServiceResult.Fail(CoordinatorOnly);
        }

        var now = Now;
        var changed = await db.TeacherEvaluations.Where(e => e.Id == id && e.SentAtUtc != null && e.DueAtUtc > now)
            .ExecuteUpdateAsync(set => set.SetProperty(e => e.DueAtUtc, now), ct);
        return changed == 0 ? ServiceResult.Fail("This evaluation is not open.") : ServiceResult.Ok("The evaluation is closed. The students can't answer any more.");
    }

    public async Task<ServiceResult> DeleteAsync(int userId, int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsCoordinatorAsync(db, userId, ct))
        {
            return ServiceResult.Fail(CoordinatorOnly);
        }

        var deleted = await db.TeacherEvaluations.Where(e => e.Id == id).ExecuteDeleteAsync(ct);
        return deleted == 0 ? ServiceResult.Fail("This evaluation was already deleted.") : ServiceResult.Ok("The evaluation was deleted with its answers.");
    }

    // ======================================================================= the student

    /// <summary>The questionnaires a student has: one for each teacher's course of each evaluation that was sent to them (also the ones answered or closed).</summary>
    public async Task<IReadOnlyList<StudentEvaluationTask>> GetStudentTasksAsync(int studentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = Now;

        var mine = await Roster.AssignmentsOf(db, studentId).Select(a => a.Id).ToListAsync(ct);
        var answered = (await db.EvaluationResponses.AsNoTracking().Where(r => r.StudentId == studentId)
            .Select(r => new { r.EvaluationId, r.AssignmentId }).ToListAsync(ct)).Select(r => (r.EvaluationId, r.AssignmentId)).ToHashSet();

        var rows = await db.EvaluationTargets.AsNoTracking()
            .Where(t => t.Evaluation.SentAtUtc != null && t.Evaluation.DueAtUtc != null
                        && (mine.Contains(t.AssignmentId) || db.EvaluationResponses.Any(r => r.StudentId == studentId && r.EvaluationId == t.EvaluationId && r.AssignmentId == t.AssignmentId)))
            .Select(t => new
            {
                t.EvaluationId, t.Evaluation.Title, Due = t.Evaluation.DueAtUtc!.Value, t.AssignmentId, SubjectName = t.Assignment.Subject.Name,
                t.Assignment.Teacher.FirstName, t.Assignment.Teacher.MiddleName, t.Assignment.Teacher.LastName, t.Assignment.Teacher.NameExtension,
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new StudentEvaluationTask(
                r.EvaluationId, r.Title, r.AssignmentId, r.SubjectName, UserDirectory.FullName(r.FirstName, r.MiddleName, r.LastName, r.NameExtension), r.Due,
                answered.Contains((r.EvaluationId, r.AssignmentId)), r.Due <= now))
            .OrderBy(t => t.Answered || t.Closed).ThenBy(t => t.DueAtUtc).ThenBy(t => t.SubjectName)
            .ToList();
    }

    /// <summary>One questionnaire, or null when it is not the student's (not sent to their course).</summary>
    public async Task<EvaluationForm?> GetFormAsync(int studentId, int evaluationId, int assignmentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = Now;

        var target = await db.EvaluationTargets.AsNoTracking()
            .Where(t => t.EvaluationId == evaluationId && t.AssignmentId == assignmentId && t.Evaluation.SentAtUtc != null && t.Evaluation.DueAtUtc != null)
            .Select(t => new
            {
                t.Evaluation.Title, t.Evaluation.Instructions, Due = t.Evaluation.DueAtUtc!.Value, SubjectName = t.Assignment.Subject.Name,
                t.Assignment.SectionId, t.Assignment.SubjectId, t.Assignment.Teacher.FirstName, t.Assignment.Teacher.MiddleName, t.Assignment.Teacher.LastName,
                t.Assignment.Teacher.NameExtension, t.Assignment.Section.GradeLevel, StrandCode = t.Assignment.Section.Strand.Code, SectionName = t.Assignment.Section.Name,
            })
            .FirstOrDefaultAsync(ct);
        if (target is null)
        {
            return null;
        }

        var answered = await db.EvaluationResponses.AnyAsync(r => r.StudentId == studentId && r.EvaluationId == evaluationId && r.AssignmentId == assignmentId, ct);
        if (!answered && !await Roster.Enrollments(db, target.SectionId, target.SubjectId).AnyAsync(e => e.StudentId == studentId, ct))
        {
            return null;
        }

        var questions = (await db.EvaluationQuestions.AsNoTracking().Where(q => q.EvaluationId == evaluationId).OrderBy(q => q.Position).ThenBy(q => q.Id).ToListAsync(ct))
            .Select(ViewOf).ToList();

        return new EvaluationForm(
            evaluationId, target.Title, target.Instructions, assignmentId, target.SubjectName,
            UserDirectory.FullName(target.FirstName, target.MiddleName, target.LastName, target.NameExtension),
            TeachingService.SectionLabel(target.GradeLevel, target.StrandCode, target.SectionName), target.Due, answered, target.Due <= now, questions);
    }

    public async Task<ServiceResult> SubmitAsync(int studentId, int evaluationId, int assignmentId, IReadOnlyList<EvaluationAnswerInput> answers, CancellationToken ct = default, bool validateOnly = false)
    {
        var form = await GetFormAsync(studentId, evaluationId, assignmentId, ct);
        if (form is null)
        {
            return ServiceResult.Fail("This evaluation is not for you.");
        }

        if (form.Answered)
        {
            return ServiceResult.Fail("You already answered this one. Thank you!");
        }

        if (form.Closed)
        {
            return ServiceResult.Fail("This evaluation is closed: the due date has passed.");
        }

        var errors = new List<string>();
        var byQuestion = answers.GroupBy(a => a.QuestionId).ToDictionary(g => g.Key, g => g.Last());
        var rows = new List<EvaluationAnswer>();
        foreach (var q in form.Questions)
        {
            byQuestion.TryGetValue(q.Id, out var given);
            var options = q.Options ?? [];
            if (EvaluationQuestionKinds.IsScale(q.Kind))
            {
                var rating = given?.Rating ?? 0;
                if (rating is < 1 or > 5)
                {
                    errors.Add($"Answer question {q.Position}: choose from 1 to 5.");
                    continue;
                }

                rows.Add(new EvaluationAnswer { QuestionId = q.Id, Rating = (byte)rating });
            }
            else if (q.Kind == EvaluationQuestionKinds.Binary)
            {
                var choice = given?.Selection?.Trim();
                if (choice != EvaluationQuestionKinds.Yes && choice != EvaluationQuestionKinds.No)
                {
                    errors.Add($"Answer question {q.Position}: Yes or No.");
                    continue;
                }

                rows.Add(new EvaluationAnswer { QuestionId = q.Id, Selection = choice });
            }
            else if (q.Kind == EvaluationQuestionKinds.Ranking)
            {
                var order = ParseIndexes(given?.Selection);
                if (order is null || order.Count != options.Count || !order.OrderBy(i => i).SequenceEqual(Enumerable.Range(0, options.Count)))
                {
                    errors.Add($"Question {q.Position}: give every option its own place, from 1 to {options.Count}.");
                    continue;
                }

                rows.Add(new EvaluationAnswer { QuestionId = q.Id, Selection = string.Join(',', order) });
            }
            else if (q.Kind == EvaluationQuestionKinds.SelectAll)
            {
                var ticked = ParseIndexes(given?.Selection);
                if (ticked is null || ticked.Any(i => i < 0 || i >= options.Count) || ticked.Distinct().Count() != ticked.Count)
                {
                    errors.Add($"Question {q.Position}: choose from the options.");
                    continue;
                }

                if (ticked.Count > 0)
                {
                    rows.Add(new EvaluationAnswer { QuestionId = q.Id, Selection = string.Join(',', ticked.OrderBy(i => i)) });
                }
            }
            else
            {
                var text = (given?.Comment ?? "").Trim();
                if (text.Length > 1000)
                {
                    errors.Add($"Question {q.Position}: write at most 1,000 characters.");
                }
                else if (text.Length > 0)
                {
                    rows.Add(new EvaluationAnswer { QuestionId = q.Id, Comment = text });
                }
            }
        }

        if (errors.Count > 0)
        {
            return ServiceResult.Fail(errors);
        }

        // Only asked whether the answers are complete (the page lists every problem at once): nothing is saved.
        if (validateOnly)
        {
            return ServiceResult.Ok();
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var response = new EvaluationResponse
        {
            EvaluationId = evaluationId, AssignmentId = assignmentId, StudentId = studentId, SubmittedAtUtc = Now, Answers = rows,
        };
        db.EvaluationResponses.Add(response);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Fail("You already answered this one. Thank you!");
        }

        return ServiceResult.Ok("Thank you! Your answers were sent. Nobody will know who wrote them.");
    }

    // ======================================================================= analytics

    /// <summary>The results of one evaluation (the latest one that was sent when <paramref name="evaluationId"/> is empty), with the trend over all of them.</summary>
    public async Task<EvaluationAnalytics> GetAnalyticsAsync(int? evaluationId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var sent = await db.TeacherEvaluations.AsNoTracking().Where(e => e.SentAtUtc != null)
            .OrderBy(e => e.SentAtUtc).ThenBy(e => e.Id)
            .Select(e => new { e.Id, e.Title, SentAt = e.SentAtUtc!.Value }).ToListAsync(ct);

        var trend = new List<EvaluationTrend>();
        foreach (var e in sent)
        {
            var responses = await db.EvaluationResponses.CountAsync(r => r.EvaluationId == e.Id, ct);
            var avg = await AverageAsync(db.EvaluationAnswers.Where(a => a.Response.EvaluationId == e.Id), ct);
            trend.Add(new EvaluationTrend(e.Id, e.Title, e.SentAt, responses, await ExpectedAsync(db, e.Id, ct), avg));
        }

        var chosen = sent.FirstOrDefault(e => e.Id == evaluationId) ?? sent.LastOrDefault();
        if (chosen is null)
        {
            return new EvaluationAnalytics(trend, null, null, 0, 0, null, [0, 0, 0, 0, 0], [], [], [], []);
        }

        var questions = await db.EvaluationQuestions.AsNoTracking().Where(q => q.EvaluationId == chosen.Id).OrderBy(q => q.Position).ThenBy(q => q.Id).ToListAsync(ct);
        var ratings = await db.EvaluationAnswers.AsNoTracking()
            .Where(a => a.Response.EvaluationId == chosen.Id && a.Rating != null)
            .Select(a => new { a.QuestionId, a.Response.AssignmentId, Rating = (int)a.Rating! })
            .ToListAsync(ct);

        var distribution = Enumerable.Range(1, 5).Select(n => ratings.Count(r => r.Rating == n)).ToList();
        var questionAverages = questions.Where(q => EvaluationQuestionKinds.IsScale(q.Kind))
            .Select(q =>
            {
                var own = ratings.Where(r => r.QuestionId == q.Id).ToList();
                return new QuestionAverage(q.Id, q.Text, q.Category, own.Count == 0 ? null : Math.Round((decimal)own.Average(r => r.Rating), 2), own.Count);
            })
            .ToList();

        var categoryAverages = questions.Where(q => EvaluationQuestionKinds.IsScale(q.Kind) && !string.IsNullOrWhiteSpace(q.Category))
            .GroupBy(q => q.Category!)
            .Select(g =>
            {
                var ids = g.Select(q => q.Id).ToHashSet();
                var own = ratings.Where(r => ids.Contains(r.QuestionId)).ToList();
                return new QuestionAverage(0, g.Key, g.Key, own.Count == 0 ? null : Math.Round((decimal)own.Average(r => r.Rating), 2), own.Count);
            })
            .ToList();

        var courses = await CourseRowsAsync(db, chosen.Id, ratings.GroupBy(r => r.AssignmentId).ToDictionary(g => g.Key, g => g.Select(r => r.Rating).ToList()), ct);
        var teachers = courses.GroupBy(c => c.TeacherId)
            .Select(g =>
            {
                var own = g.SelectMany(c => ratings.Where(r => r.AssignmentId == c.AssignmentId)).ToList();
                return new
                {
                    TeacherId = g.Key, Name = g.First().TeacherName, Courses = g.Count(), Expected = g.Sum(c => c.Expected), Responses = g.Sum(c => c.Responses),
                    Average = own.Count == 0 ? (decimal?)null : Math.Round((decimal)own.Average(r => r.Rating), 2),
                };
            })
            .OrderByDescending(t => t.Average ?? 0).ThenBy(t => t.Name)
            .Select((t, i) => new TeacherResult(t.TeacherId, t.Name, t.Courses, t.Expected, t.Responses, t.Average, i + 1))
            .ToList();

        var selections = await db.EvaluationAnswers.AsNoTracking()
            .Where(a => a.Response.EvaluationId == chosen.Id && a.Selection != null)
            .Select(a => new { a.QuestionId, a.Selection }).ToListAsync(ct);
        var others = OtherResults(questions, selections.Select(a => (a.QuestionId, a.Selection!)).ToList());

        var row = trend.First(t => t.EvaluationId == chosen.Id);
        return new EvaluationAnalytics(trend, chosen.Id, chosen.Title, row.Expected, row.Responses, row.Average, distribution, questionAverages, categoryAverages, teachers, others);
    }

    /// <summary>One teacher's results in an evaluation: course by course, question by question, and what the students wrote (without names).</summary>
    public async Task<TeacherDetail?> GetTeacherDetailAsync(int evaluationId, int teacherId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var teacher = await db.Teachers.AsNoTracking().Where(t => t.Id == teacherId)
            .Select(t => new { t.FirstName, t.MiddleName, t.LastName, t.NameExtension }).FirstOrDefaultAsync(ct);
        if (teacher is null)
        {
            return null;
        }

        var assignmentIds = await db.EvaluationTargets.AsNoTracking().Where(t => t.EvaluationId == evaluationId && t.Assignment.TeacherId == teacherId)
            .Select(t => t.AssignmentId).ToListAsync(ct);
        if (assignmentIds.Count == 0)
        {
            return null;
        }

        var questions = await db.EvaluationQuestions.AsNoTracking().Where(q => q.EvaluationId == evaluationId).OrderBy(q => q.Position).ThenBy(q => q.Id).ToListAsync(ct);
        var answers = await db.EvaluationAnswers.AsNoTracking()
            .Where(a => a.Response.EvaluationId == evaluationId && assignmentIds.Contains(a.Response.AssignmentId))
            .Select(a => new
            {
                a.QuestionId, a.Response.AssignmentId, a.Rating, a.Comment, a.Selection, a.Response.SubmittedAtUtc,
                Subject = a.Response.Assignment.Subject.Name, a.Response.Assignment.Section.GradeLevel, StrandCode = a.Response.Assignment.Section.Strand.Code,
                SectionName = a.Response.Assignment.Section.Name,
            })
            .ToListAsync(ct);

        var ratings = answers.Where(a => a.Rating != null).ToList();
        var byAssignment = ratings.GroupBy(a => a.AssignmentId).ToDictionary(g => g.Key, g => g.Select(a => (int)a.Rating!.Value).ToList());
        var courses = (await CourseRowsAsync(db, evaluationId, byAssignment, ct)).Where(c => c.TeacherId == teacherId).ToList();

        var questionRows = questions.Where(q => EvaluationQuestionKinds.IsScale(q.Kind))
            .Select(q =>
            {
                var own = ratings.Where(a => a.QuestionId == q.Id).ToList();
                return new QuestionAverage(q.Id, q.Text, q.Category, own.Count == 0 ? null : Math.Round((decimal)own.Average(a => (int)a.Rating!.Value), 2), own.Count);
            })
            .ToList();

        var commentQuestions = questions.Where(q => q.Kind == EvaluationQuestionKinds.Comment).ToDictionary(q => q.Id, q => q.Text);
        var comments = answers.Where(a => !string.IsNullOrWhiteSpace(a.Comment) && commentQuestions.ContainsKey(a.QuestionId))
            .OrderByDescending(a => a.SubmittedAtUtc)
            .Select(a => new EvaluationComment(a.Comment!, a.Subject, TeachingService.SectionLabel(a.GradeLevel, a.StrandCode, a.SectionName), commentQuestions[a.QuestionId], a.SubmittedAtUtc))
            .ToList();

        return new TeacherDetail(
            teacherId, UserDirectory.FullName(teacher.FirstName, teacher.MiddleName, teacher.LastName, teacher.NameExtension),
            ratings.Count == 0 ? null : Math.Round((decimal)ratings.Average(a => (int)a.Rating!.Value), 2), courses.Sum(c => c.Responses), courses.Sum(c => c.Expected),
            courses, questionRows, Enumerable.Range(1, 5).Select(n => ratings.Count(a => a.Rating == n)).ToList(), comments,
            OtherResults(questions, answers.Where(a => a.Selection != null).Select(a => (a.QuestionId, a.Selection!)).ToList()));
    }

    // ======================================================================= helpers

    private static EvaluationQuestionView ViewOf(EvaluationQuestion q) =>
        new(q.Id, q.Position, q.Text, q.Kind, q.Category, EvaluationQuestionKinds.HasOptions(q.Kind) ? EvaluationQuestionKinds.OptionsOf(q.OptionsText) : null);

    /// <summary>The options as they are saved: trimmed, without empty lines and without the same option twice.</summary>
    public static List<string> CleanOptions(IEnumerable<string>? options) =>
        (options ?? []).Select(o => (o ?? "").Trim()).Where(o => o.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>"2,0,1" to 2, 0, 1. An empty text is an empty list; anything else that is not numbers is null.</summary>
    internal static List<int>? ParseIndexes(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var list = new List<int>();
        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(part, out var n))
            {
                return null;
            }

            list.Add(n);
        }

        return list;
    }

    /// <summary>The results of the binary, ranking and select-all questions, from the choices the students made.</summary>
    private static IReadOnlyList<QuestionResult> OtherResults(IEnumerable<EvaluationQuestion> questions, IReadOnlyList<(int QuestionId, string Selection)> selections)
    {
        var results = new List<QuestionResult>();
        foreach (var q in questions.Where(q => q.Kind is EvaluationQuestionKinds.Binary or EvaluationQuestionKinds.Ranking or EvaluationQuestionKinds.SelectAll))
        {
            var own = selections.Where(s => s.QuestionId == q.Id).Select(s => s.Selection).ToList();
            if (q.Kind == EvaluationQuestionKinds.Binary)
            {
                var yes = own.Count(x => x == EvaluationQuestionKinds.Yes);
                var no = own.Count(x => x == EvaluationQuestionKinds.No);
                var total = yes + no;
                results.Add(new QuestionResult(q.Id, q.Text, q.Category, q.Kind, total,
                [
                    new(EvaluationQuestionKinds.Yes, yes, total == 0 ? null : Math.Round(yes * 100m / total, 1)),
                    new(EvaluationQuestionKinds.No, no, total == 0 ? null : Math.Round(no * 100m / total, 1)),
                ]));
                continue;
            }

            var options = EvaluationQuestionKinds.OptionsOf(q.OptionsText);
            var parsed = own.Select(x => ParseIndexes(x)).Where(x => x is not null).Select(x => x!).ToList();
            if (q.Kind == EvaluationQuestionKinds.Ranking)
            {
                results.Add(new QuestionResult(q.Id, q.Text, q.Category, q.Kind, parsed.Count,
                    options.Select((label, i) =>
                    {
                        var places = parsed.Where(order => order.Contains(i)).Select(order => order.IndexOf(i) + 1).ToList();
                        return new OptionResult(label, places.Count(p => p == 1), places.Count == 0 ? null : Math.Round((decimal)places.Average(), 2));
                    })
                    .OrderBy(o => o.Value ?? decimal.MaxValue).ToList()));
            }
            else
            {
                results.Add(new QuestionResult(q.Id, q.Text, q.Category, q.Kind, parsed.Count,
                    options.Select((label, i) =>
                    {
                        var count = parsed.Count(ticked => ticked.Contains(i));
                        return new OptionResult(label, count, parsed.Count == 0 ? null : Math.Round(count * 100m / parsed.Count, 1));
                    }).ToList()));
            }
        }

        return results;
    }

    private static async Task<bool> IsCoordinatorAsync(AppDbContext db, int userId, CancellationToken ct) =>
        await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.IsActive && u.RoleId == AppRoles.AcademicCoordinatorId, ct);

    /// <summary>The students of every course the evaluation is about, added up (the answers it should get).</summary>
    private static async Task<int> ExpectedAsync(AppDbContext db, int evaluationId, CancellationToken ct)
    {
        var targets = await db.EvaluationTargets.AsNoTracking().Where(t => t.EvaluationId == evaluationId)
            .Select(t => new { t.Assignment.SectionId, t.Assignment.SubjectId }).ToListAsync(ct);
        var total = 0;
        foreach (var t in targets)
        {
            total += await Roster.StudentIds(db, t.SectionId, t.SubjectId).CountAsync(ct);
        }

        return total;
    }

    private static async Task<decimal?> AverageAsync(IQueryable<EvaluationAnswer> answers, CancellationToken ct)
    {
        var rated = answers.Where(a => a.Rating != null);
        if (!await rated.AnyAsync(ct))
        {
            return null;
        }

        return Math.Round((decimal)await rated.AverageAsync(a => (double)a.Rating!.Value, ct), 2);
    }

    /// <summary>One row for each course an evaluation was sent about, with the answers it has and (when the ratings are given) their average.</summary>
    private static async Task<IReadOnlyList<EvaluationCourseRow>> CourseRowsAsync(
        AppDbContext db, int evaluationId, Dictionary<int, List<int>>? ratingsByAssignment, CancellationToken ct)
    {
        var targets = await db.EvaluationTargets.AsNoTracking().Where(t => t.EvaluationId == evaluationId)
            .OrderBy(t => t.Assignment.Teacher.LastName).ThenBy(t => t.Assignment.Teacher.FirstName).ThenBy(t => t.Assignment.Subject.Name).ThenBy(t => t.Assignment.Section.Name)
            .Select(t => new
            {
                t.AssignmentId, t.Assignment.TeacherId, t.Assignment.Teacher.FirstName, t.Assignment.Teacher.MiddleName, t.Assignment.Teacher.LastName,
                t.Assignment.Teacher.NameExtension, SubjectName = t.Assignment.Subject.Name, t.Assignment.SectionId, t.Assignment.SubjectId,
                t.Assignment.Section.GradeLevel, StrandCode = t.Assignment.Section.Strand.Code, SectionName = t.Assignment.Section.Name,
            })
            .ToListAsync(ct);

        var responses = await db.EvaluationResponses.AsNoTracking().Where(r => r.EvaluationId == evaluationId)
            .GroupBy(r => r.AssignmentId).Select(g => new { AssignmentId = g.Key, Count = g.Count() }).ToListAsync(ct);

        if (ratingsByAssignment is null)
        {
            var all = await db.EvaluationAnswers.AsNoTracking().Where(a => a.Response.EvaluationId == evaluationId && a.Rating != null)
                .Select(a => new { a.Response.AssignmentId, Rating = (int)a.Rating! }).ToListAsync(ct);
            ratingsByAssignment = all.GroupBy(a => a.AssignmentId).ToDictionary(g => g.Key, g => g.Select(a => a.Rating).ToList());
        }

        var rows = new List<EvaluationCourseRow>();
        foreach (var t in targets)
        {
            var expected = await Roster.StudentIds(db, t.SectionId, t.SubjectId).CountAsync(ct);
            var given = responses.FirstOrDefault(r => r.AssignmentId == t.AssignmentId)?.Count ?? 0;
            ratingsByAssignment.TryGetValue(t.AssignmentId, out var list);
            rows.Add(new EvaluationCourseRow(
                t.AssignmentId, t.TeacherId, UserDirectory.FullName(t.FirstName, t.MiddleName, t.LastName, t.NameExtension), t.SubjectName,
                TeachingService.SectionLabel(t.GradeLevel, t.StrandCode, t.SectionName), Math.Max(expected, given), given,
                list is { Count: > 0 } ? Math.Round((decimal)list.Average(), 2) : null));
        }

        return rows;
    }
}
