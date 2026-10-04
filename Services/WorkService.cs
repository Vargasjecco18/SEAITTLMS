using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

// ---------------------------------------------------------------------- what the student sees

public sealed record StudentItemView(
    int ItemId, string Kind, string Title, string? Instructions, decimal MaxScore, DateTime? DueAtUtc,
    int ModuleId, string ModuleTitle, int AssignmentId, string SubjectName,
    string Status, string? AnswerText, string? LinkUrl, decimal? Score, string? Feedback, DateTime? SubmittedAtUtc, bool IsLate,
    int Questions, int? TimeLimitMinutes, bool AntiCheat, int MaxViolations, bool ShowResult, int ViolationCount, string? AutoSubmitReason)
{
    public bool IsQuiz => Kind == ItemKinds.Quiz;

    /// <summary>The score may be shown (a quiz can hide it until the teacher says so).</summary>
    public bool ScoreVisible => Score is not null && (!IsQuiz || ShowResult);
}

// ---------------------------------------------------------------------- what the teacher sees

public sealed record SubmissionRow(int StudentId, string StudentName, string? Lrn, string Status, DateTime? SubmittedAtUtc, bool IsLate, decimal? Score, int ViolationCount, bool IsFlagged);

public sealed record ItemSubmissionList(
    int ItemId, string Kind, string Title, decimal MaxScore, DateTime? DueAtUtc, int ModuleId, string ModuleTitle,
    int AssignmentId, string SubjectName, string SectionLabel, bool AntiCheat, int Questions, IReadOnlyList<SubmissionRow> Rows);

public sealed record AnswerReview(string Question, string Kind, string? Given, string Correct, bool IsCorrect, decimal Points, decimal Earned);

public sealed record SubmissionDetail(
    int StudentId, string StudentName, string Kind, string ItemTitle, decimal MaxScore, string Status,
    string? AnswerText, string? LinkUrl, decimal? Score, string? Feedback, DateTime? SubmittedAtUtc, bool IsLate,
    int ViolationCount, string? ViolationLog, string? AutoSubmitReason, bool IsFlagged, IReadOnlyList<AnswerReview> Answers);

/// <summary>An item together with where it belongs, loaded after checking that the person is allowed to see it.</summary>
internal sealed record ItemContext(
    ModuleItem Item, int ModuleId, string ModuleTitle, int AssignmentId, int SectionId, int SubjectId, string SubjectName, int TeacherId, string SectionLabel);

internal static class ItemAccess
{
    /// <summary>An item a student may see: it is posted (the module and the item), and the student is on the roster of the subject.</summary>
    public static async Task<ItemContext?> ForStudentAsync(AppDbContext db, int studentId, int itemId, DateTime nowUtc, bool tracking, CancellationToken ct)
    {
        var items = tracking ? db.ModuleItems.AsQueryable() : db.ModuleItems.AsNoTracking();
        var found = await items
            .Where(i => i.Id == itemId
                        && (i.VisibleFromUtc == null || i.VisibleFromUtc <= nowUtc)
                        && i.Module.VisibleFromUtc != null && i.Module.VisibleFromUtc <= nowUtc)
            .Select(i => new
            {
                Item = i, ModuleTitle = i.Module.Title, i.Module.AssignmentId, i.Module.Assignment.SectionId, i.Module.Assignment.SubjectId,
                Subject = i.Module.Assignment.Subject.Name, i.Module.Assignment.TeacherId,
                i.Module.Assignment.Section.GradeLevel, Strand = i.Module.Assignment.Section.Strand.Code, Section = i.Module.Assignment.Section.Name,
            })
            .FirstOrDefaultAsync(ct);
        if (found is null || !await Roster.Enrollments(db, found.SectionId, found.SubjectId).AnyAsync(e => e.StudentId == studentId, ct))
        {
            return null;
        }

        return new ItemContext(found.Item, found.Item.ModuleId, found.ModuleTitle, found.AssignmentId, found.SectionId, found.SubjectId, found.Subject, found.TeacherId,
            TeachingService.SectionLabel(found.GradeLevel, found.Strand, found.Section));
    }

    /// <summary>An item of a subject the teacher teaches (posted or not).</summary>
    public static async Task<ItemContext?> ForTeacherAsync(AppDbContext db, int teacherId, int itemId, bool tracking, CancellationToken ct)
    {
        var items = tracking ? db.ModuleItems.AsQueryable() : db.ModuleItems.AsNoTracking();
        var found = await items
            .Where(i => i.Id == itemId && i.Module.Assignment.TeacherId == teacherId)
            .Select(i => new
            {
                Item = i, ModuleTitle = i.Module.Title, i.Module.AssignmentId, i.Module.Assignment.SectionId, i.Module.Assignment.SubjectId,
                Subject = i.Module.Assignment.Subject.Name, i.Module.Assignment.TeacherId,
                i.Module.Assignment.Section.GradeLevel, Strand = i.Module.Assignment.Section.Strand.Code, Section = i.Module.Assignment.Section.Name,
            })
            .FirstOrDefaultAsync(ct);
        return found is null
            ? null
            : new ItemContext(found.Item, found.Item.ModuleId, found.ModuleTitle, found.AssignmentId, found.SectionId, found.SubjectId, found.Subject, found.TeacherId,
                TeachingService.SectionLabel(found.GradeLevel, found.Strand, found.Section));
    }
}

/// <summary>
/// Handing in work and grading it: students hand in activities and task performances (text and/or a link); the teacher of the subject
/// gives a score and feedback. Quizzes are handed in and graded by <see cref="QuizService"/>, the teacher can still change the score here.
/// </summary>
public sealed class WorkService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider clock, NotificationService notifications)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // ======================================================================= the student

    public async Task<StudentItemView?> GetStudentItemAsync(int studentId, int itemId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var context = await ItemAccess.ForStudentAsync(db, studentId, itemId, Now, tracking: false, ct);
        if (context is null)
        {
            return null;
        }

        var i = context.Item;
        var s = await db.Submissions.AsNoTracking().FirstOrDefaultAsync(x => x.ItemId == itemId && x.StudentId == studentId, ct);
        var questions = i.Kind == ItemKinds.Quiz ? await db.QuizQuestions.CountAsync(q => q.ItemId == itemId, ct) : 0;

        return new StudentItemView(
            i.Id, i.Kind, i.Title, i.Instructions, i.MaxScore, i.DueAtUtc, context.ModuleId, context.ModuleTitle, context.AssignmentId, context.SubjectName,
            s?.Status ?? "NotStarted", s?.AnswerText, s?.LinkUrl, s?.Score, s?.Feedback, s?.SubmittedAtUtc, s?.IsLate ?? false,
            questions, i.TimeLimitMinutes, i.AntiCheat, i.MaxViolations, i.ShowResult, s?.ViolationCount ?? 0, s?.AutoSubmitReason);
    }

    /// <summary>Hands in an activity or a task performance. It can be handed in again until the teacher has graded it. Late work is marked late.</summary>
    public async Task<ServiceResult> SubmitAsync(int studentId, int itemId, string? answerText, string? linkUrl, CancellationToken ct = default)
    {
        answerText = string.IsNullOrWhiteSpace(answerText) ? null : answerText.Trim();
        linkUrl = string.IsNullOrWhiteSpace(linkUrl) ? null : linkUrl.Trim();

        var errors = new List<string>();
        if (answerText is null && linkUrl is null)
        {
            errors.Add("Write your answer or add a link to your work.");
        }

        if (answerText is { Length: > 20000 })
        {
            errors.Add("Your answer can have at most 20,000 characters.");
        }

        if (linkUrl is not null && !ModuleService.IsWebAddress(linkUrl))
        {
            errors.Add("The link must start with http:// or https://.");
        }

        if (errors.Count > 0)
        {
            return ServiceResult.Fail(errors);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = Now;
        var context = await ItemAccess.ForStudentAsync(db, studentId, itemId, now, tracking: false, ct);
        if (context is null)
        {
            return ServiceResult.Fail("This work was not found.");
        }

        if (context.Item.Kind == ItemKinds.Quiz)
        {
            return ServiceResult.Fail("A quiz is taken on its own page.");
        }

        var submission = await db.Submissions.FirstOrDefaultAsync(s => s.ItemId == itemId && s.StudentId == studentId, ct);
        if (submission is { Status: SubmissionStatus.Graded })
        {
            return ServiceResult.Fail("This work was already graded, so it can't be handed in again.");
        }

        var isNew = submission is null;
        submission ??= db.Submissions.Add(new Submission { ItemId = itemId, StudentId = studentId }).Entity;
        submission.Status = SubmissionStatus.Submitted;
        submission.AnswerText = answerText;
        submission.LinkUrl = linkUrl;
        submission.SubmittedAtUtc = now;
        submission.IsLate = context.Item.DueAtUtc is { } due && now > due;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (isNew)
        {
            return ServiceResult.Fail("Your work was already handed in. Reload the page.");
        }

        return ServiceResult.Ok(submission.IsLate ? "Handed in. It was after the due date, so it is marked late." : "Handed in. Your teacher will check it.");
    }

    // ======================================================================= the teacher

    public async Task<ItemSubmissionList?> GetItemSubmissionsAsync(int teacherId, int itemId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var context = await ItemAccess.ForTeacherAsync(db, teacherId, itemId, tracking: false, ct);
        if (context is null)
        {
            return null;
        }

        var students = await Roster.Enrollments(db, context.SectionId, context.SubjectId)
            .Select(e => new { e.StudentId, e.Student.FirstName, e.Student.LastName, e.Student.Lrn }).Distinct()
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName).ToListAsync(ct);
        var submissions = (await db.Submissions.AsNoTracking().Where(s => s.ItemId == itemId).ToListAsync(ct)).ToDictionary(s => s.StudentId);

        var rows = students.Select(st =>
        {
            submissions.TryGetValue(st.StudentId, out var s);
            return new SubmissionRow(st.StudentId, $"{st.LastName}, {st.FirstName}", st.Lrn, s?.Status ?? "NotStarted", s?.SubmittedAtUtc, s?.IsLate ?? false, s?.Score, s?.ViolationCount ?? 0, s?.IsFlagged ?? false);
        }).ToList();

        var i = context.Item;
        var questions = i.Kind == ItemKinds.Quiz ? await db.QuizQuestions.CountAsync(q => q.ItemId == itemId, ct) : 0;
        return new ItemSubmissionList(i.Id, i.Kind, i.Title, i.MaxScore, i.DueAtUtc, context.ModuleId, context.ModuleTitle, context.AssignmentId, context.SubjectName, context.SectionLabel, i.AntiCheat, questions, rows);
    }

    public async Task<SubmissionDetail?> GetSubmissionAsync(int teacherId, int itemId, int studentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var context = await ItemAccess.ForTeacherAsync(db, teacherId, itemId, tracking: false, ct);
        if (context is null)
        {
            return null;
        }

        var student = await Roster.Enrollments(db, context.SectionId, context.SubjectId).Where(e => e.StudentId == studentId)
            .Select(e => e.Student.LastName + ", " + e.Student.FirstName).FirstOrDefaultAsync(ct);
        if (student is null)
        {
            return null;
        }

        var s = await db.Submissions.AsNoTracking().FirstOrDefaultAsync(x => x.ItemId == itemId && x.StudentId == studentId, ct);
        var answers = context.Item.Kind == ItemKinds.Quiz && s is not null ? await QuizService.ReviewAsync(db, itemId, s, ct) : [];

        return new SubmissionDetail(
            studentId, student, context.Item.Kind, context.Item.Title, context.Item.MaxScore, s?.Status ?? "NotStarted",
            s?.AnswerText, s?.LinkUrl, s?.Score, s?.Feedback, s?.SubmittedAtUtc, s?.IsLate ?? false,
            s?.ViolationCount ?? 0, s?.ViolationLog, s?.AutoSubmitReason, s?.IsFlagged ?? false, answers);
    }

    /// <summary>Gives a score and feedback. Works also when the student handed nothing in (a task performance done in class). The student is told.</summary>
    public async Task<ServiceResult> GradeAsync(int teacherId, int itemId, int studentId, decimal? score, string? feedback, CancellationToken ct = default, bool keepFeedback = false)
    {
        feedback = string.IsNullOrWhiteSpace(feedback) ? null : feedback.Trim();

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var context = await ItemAccess.ForTeacherAsync(db, teacherId, itemId, tracking: false, ct);
        if (context is null)
        {
            return ServiceResult.Fail("This work was not found.");
        }

        if (score is null)
        {
            return ServiceResult.Fail("Type the score.");
        }

        if (score < 0 || score > context.Item.MaxScore)
        {
            return ServiceResult.Fail($"The score must be between 0 and {context.Item.MaxScore:0.##}.");
        }

        if (feedback is { Length: > 1000 })
        {
            return ServiceResult.Fail("The feedback can have at most 1000 characters.");
        }

        var studentUserId = await Roster.Enrollments(db, context.SectionId, context.SubjectId).Where(e => e.StudentId == studentId)
            .Select(e => (int?)e.Student.UserId).FirstOrDefaultAsync(ct);
        if (studentUserId is null)
        {
            return ServiceResult.Fail("This student is not in the subject.");
        }

        var now = Now;
        var s = await db.Submissions.FirstOrDefaultAsync(x => x.ItemId == itemId && x.StudentId == studentId, ct);
        var isNew = s is null;
        s ??= db.Submissions.Add(new Submission { ItemId = itemId, StudentId = studentId }).Entity;
        s.Score = Math.Round(score.Value, 2);
        if (!keepFeedback)
        {
            s.Feedback = feedback;
        }

        s.Status = SubmissionStatus.Graded;
        s.GradedAtUtc = now;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (isNew)
        {
            return ServiceResult.Fail("The student's work changed while you were grading. Open it again.");
        }

        await notifications.AddAsync(studentUserId.Value, "grade", $"{context.Item.Title} was graded",
            $"{context.SubjectName} · {s.Score:0.##} / {context.Item.MaxScore:0.##}", $"student/items/{itemId}", ct);
        return ServiceResult.Ok("Saved. The student was told.");
    }
}
