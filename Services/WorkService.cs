using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

// ---------------------------------------------------------------------- what the student sees

public sealed record StudentItemView(
    int ItemId, string Kind, string Title, string? Instructions, decimal MaxScore, DateTime? DueAtUtc,
    int ModuleId, string ModuleTitle, int AssignmentId, string SubjectName,
    string Status, string? AnswerText, string? AnswerHtml, string? LinkUrl, decimal? Score, string? Feedback, DateTime? SubmittedAtUtc, bool IsLate,
    int Questions, int? TimeLimitMinutes, bool AntiCheat, int MaxViolations, bool ShowResult, int ViolationCount, string? AutoSubmitReason, int? SubmissionId = null,
    string? LateDecision = null)
{
    public bool IsQuiz => Kind == ItemKinds.Quiz;

    /// <summary>The score may be shown (a quiz can hide it until the teacher says so).</summary>
    public bool ScoreVisible => Score is not null && (!IsQuiz || ShowResult);
}

// ---------------------------------------------------------------------- what the teacher sees

public sealed record SubmissionRow(int StudentId, string StudentName, string? Lrn, string Status, DateTime? SubmittedAtUtc, bool IsLate, decimal? Score, int ViolationCount, bool IsFlagged, string? LateDecision = null, int? SubmissionId = null)
{
    /// <summary>Handed in and waiting for the teacher: not graded, and not declined as late work.</summary>
    public bool NeedsCheck => Status == SubmissionStatus.Submitted && LateDecision != Data.Entities.LateDecisions.Declined;
}

public sealed record ItemSubmissionList(
    int ItemId, string Kind, string Title, decimal MaxScore, DateTime? DueAtUtc, int ModuleId, string ModuleTitle,
    int AssignmentId, string SubjectName, string SectionLabel, bool AntiCheat, int Questions, IReadOnlyList<SubmissionRow> Rows);

public sealed record AnswerReview(string Question, string Kind, string? Given, string Correct, bool IsCorrect, decimal Points, decimal Earned);

public sealed record SubmissionDetail(
    int StudentId, string StudentName, string Kind, string ItemTitle, decimal MaxScore, string Status,
    string? AnswerText, string? AnswerHtml, string? LinkUrl, decimal? Score, string? Feedback, DateTime? SubmittedAtUtc, bool IsLate,
    int ViolationCount, string? ViolationLog, string? AutoSubmitReason, bool IsFlagged, IReadOnlyList<AnswerReview> Answers, int? SubmissionId = null, string? LateDecision = null);

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
/// Handing in work and grading it: students hand in activities and task performances (text and/or files); the teacher of the subject
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
            s?.Status ?? "NotStarted", s?.AnswerText, s?.AnswerHtml, s?.LinkUrl, s?.Score, s?.Feedback, s?.SubmittedAtUtc, s?.IsLate ?? false,
            questions, i.TimeLimitMinutes, i.AntiCheat, i.MaxViolations, i.ShowResult, s?.ViolationCount ?? 0, s?.AutoSubmitReason, s?.Id, s?.LateDecision);
    }

    /// <summary>
    /// The id of the student's piece of work for an item, made (as "in progress") when there is none yet, so the student can attach files to it before handing in.
    /// Null when the student can't work on it here: it was not found, it is a quiz with questions, or it was graded already.
    /// </summary>
    public async Task<int?> EnsureDraftAsync(int studentId, int itemId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var context = await ItemAccess.ForStudentAsync(db, studentId, itemId, Now, tracking: false, ct);
        if (context is null || (context.Item.Kind == ItemKinds.Quiz && await db.QuizQuestions.AnyAsync(q => q.ItemId == itemId, ct)))
        {
            return null;
        }

        var submission = await db.Submissions.AsNoTracking().Where(s => s.ItemId == itemId && s.StudentId == studentId).Select(s => new { s.Id, s.Status }).FirstOrDefaultAsync(ct);
        if (submission is not null)
        {
            return submission.Status == SubmissionStatus.Graded ? null : submission.Id;
        }

        var created = db.Submissions.Add(new Submission { ItemId = itemId, StudentId = studentId, Status = SubmissionStatus.InProgress });
        try
        {
            await db.SaveChangesAsync(ct);
            return created.Entity.Id;
        }
        catch (DbUpdateException)
        {
            // Made at the same moment by another request: use that one.
            return await db.Submissions.AsNoTracking().Where(s => s.ItemId == itemId && s.StudentId == studentId && s.Status != SubmissionStatus.Graded).Select(s => (int?)s.Id).FirstOrDefaultAsync(ct);
        }
    }

    /// <summary>Remembers that the student opened the page of an activity, a quiz or a task performance (the first time only).</summary>
    public async Task RecordViewAsync(int studentId, int itemId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (await db.ItemViews.AnyAsync(v => v.ItemId == itemId && v.StudentId == studentId, ct))
        {
            return;
        }

        var context = await ItemAccess.ForStudentAsync(db, studentId, itemId, Now, tracking: false, ct);
        if (context is null)
        {
            return;
        }

        db.ItemViews.Add(new ItemView { ItemId = itemId, StudentId = studentId, FirstOpenedAtUtc = Now });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Opened twice at the same moment: it is remembered either way.
        }
    }

    /// <summary>
    /// Hands in an activity or a task performance. It can be handed in again until the teacher has graded it. Late work is marked late.
    /// The answer is either plain text (<paramref name="answerText"/>) or what the student wrote in the editor (<paramref name="answerHtml"/>,
    /// cleaned here: only the allowed formatting is kept, and the plain words are kept next to it).
    /// </summary>
    public async Task<ServiceResult> SubmitAsync(int studentId, int itemId, string? answerText, string? linkUrl, CancellationToken ct = default, string? answerHtml = null)
    {
        if (!string.IsNullOrWhiteSpace(answerHtml))
        {
            answerHtml = RichText.Sanitize(answerHtml);
            answerText = RichText.ToPlainText(answerHtml);
            if (answerHtml.Length > RichText.MaxHtmlLength)
            {
                return ServiceResult.Fail("Your answer is too long. Remove some of it or some of the formatting.");
            }
        }
        else
        {
            answerHtml = null;
        }

        answerText = string.IsNullOrWhiteSpace(answerText) ? null : answerText.Trim();
        if (answerText is null)
        {
            answerHtml = null;
        }

        linkUrl = string.IsNullOrWhiteSpace(linkUrl) ? null : linkUrl.Trim();

        var errors = new List<string>();
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
            return ServiceResult.Fail("This assignment was not found.");
        }

        // A quiz with questions is taken on its own page. A quiz that is only a file (no questions) is answered here, like an activity, and the teacher gives the score.
        if (context.Item.Kind == ItemKinds.Quiz && await db.QuizQuestions.AnyAsync(q => q.ItemId == itemId, ct))
        {
            return ServiceResult.Fail("A quiz is taken on its own page.");
        }

        var submission = await db.Submissions.FirstOrDefaultAsync(s => s.ItemId == itemId && s.StudentId == studentId, ct);
        if (submission is { Status: SubmissionStatus.Graded })
        {
            return ServiceResult.Fail("This assignment was already graded, so it can't be handed in again.");
        }

        if (submission is { LateDecision: LateDecisions.Declined })
        {
            return ServiceResult.Fail("Your teacher did not accept your late work, so it can't be handed in again.");
        }

        // The work is the written answer, the files the student sent with it, or both.
        var hasFiles = submission is not null && await db.Attachments.AnyAsync(a => a.SubmissionId == submission.Id, ct);
        if (answerText is null && linkUrl is null && !hasFiles)
        {
            return ServiceResult.Fail("Write your answer or send a file.");
        }

        var isNew = submission is null;
        submission ??= db.Submissions.Add(new Submission { ItemId = itemId, StudentId = studentId }).Entity;
        submission.Status = SubmissionStatus.Submitted;
        submission.AnswerText = answerText;
        submission.AnswerHtml = answerHtml;
        submission.LinkUrl = linkUrl;
        submission.SubmittedAtUtc = now;
        submission.IsLate = context.Item.DueAtUtc is { } due && now > due;

        // Work handed in after the due date can be handed in, but the teacher decides whether to accept it. Only accepted late work is graded.
        submission.LateDecision = !submission.IsLate ? null : submission.LateDecision == LateDecisions.Accepted ? LateDecisions.Accepted : LateDecisions.Pending;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (isNew)
        {
            return ServiceResult.Fail("Your answer was already handed in. Reload the page.");
        }

        await TellTeacherAsync(db, context, studentId, submission.IsLate, ct);

        return ServiceResult.Ok(submission.IsLate ? "Handed in late. Your teacher decides if it is accepted." : "Handed in. Your teacher will check it.");
    }

    /// <summary>The teacher is told that a student handed in work to check. Handing in again while the first notice is still unread does not tell again.</summary>
    private async Task TellTeacherAsync(AppDbContext db, ItemContext context, int studentId, bool late, CancellationToken ct)
    {
        var teacherUserId = await db.Teachers.AsNoTracking().Where(x => x.Id == context.TeacherId).Select(x => (int?)x.UserId).FirstOrDefaultAsync(ct);
        var student = await db.Students.AsNoTracking().Where(x => x.Id == studentId).Select(x => new { x.FirstName, x.LastName }).FirstOrDefaultAsync(ct);
        if (teacherUserId is not { } teacherUser || student is null)
        {
            return;
        }

        var title = $"{student.FirstName} {student.LastName} handed in {context.Item.Title}";
        var link = $"teacher/items/{context.Item.Id}";
        if (await db.Notifications.AsNoTracking().AnyAsync(n => n.UserId == teacherUser && n.Link == link && n.Title == title && !n.IsRead, ct))
        {
            return;
        }

        await notifications.AddAsync(teacherUser, "activity", late ? title + " (late)" : title, late ? $"{context.SubjectName} · accept or decline it" : context.SubjectName, link, ct);
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
            return new SubmissionRow(st.StudentId, $"{st.LastName}, {st.FirstName}", st.Lrn, s?.Status ?? "NotStarted", s?.SubmittedAtUtc, s?.IsLate ?? false, s?.Score, s?.ViolationCount ?? 0, s?.IsFlagged ?? false, s?.LateDecision, s?.Id);
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
            s?.AnswerText, s?.AnswerHtml, s?.LinkUrl, s?.Score, s?.Feedback, s?.SubmittedAtUtc, s?.IsLate ?? false,
            s?.ViolationCount ?? 0, s?.ViolationLog, s?.AutoSubmitReason, s?.IsFlagged ?? false, answers, s?.Id, s?.LateDecision);
    }

    /// <summary>
    /// The teacher accepts or declines work that was handed in after the due date. Accepted work is graded like any other; declined work is not, and the student can't
    /// hand it in again. The student is told. The decision can be changed until the work is graded.
    /// </summary>
    public async Task<ServiceResult> DecideLateAsync(int teacherId, int itemId, int studentId, bool accept, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var context = await ItemAccess.ForTeacherAsync(db, teacherId, itemId, tracking: false, ct);
        if (context is null)
        {
            return ServiceResult.Fail("This assignment was not found.");
        }

        var s = await db.Submissions.FirstOrDefaultAsync(x => x.ItemId == itemId && x.StudentId == studentId, ct);
        if (s is null || !s.IsLate || s.LateDecision is null)
        {
            return ServiceResult.Fail("This work was not handed in late.");
        }

        if (s.Status == SubmissionStatus.Graded)
        {
            return ServiceResult.Fail("This work was graded already.");
        }

        var decision = accept ? LateDecisions.Accepted : LateDecisions.Declined;
        if (s.LateDecision == decision)
        {
            return ServiceResult.Ok(accept ? "It was accepted already." : "It was declined already.");
        }

        s.LateDecision = decision;
        await db.SaveChangesAsync(ct);

        var studentUserId = await db.Students.AsNoTracking().Where(x => x.Id == studentId).Select(x => (int?)x.UserId).FirstOrDefaultAsync(ct);
        if (studentUserId is { } userId)
        {
            await notifications.AddAsync(userId, "activity",
                accept ? $"Your late {context.Item.Title} was accepted" : $"Your late {context.Item.Title} was not accepted",
                accept ? $"{context.SubjectName} · your teacher will check it" : $"{context.SubjectName} · it will not be graded",
                $"student/items/{itemId}", ct);
        }

        return ServiceResult.Ok(accept ? "Accepted. You can give it a score now. The student was told." : "Declined. The student was told.");
    }

    /// <summary>Gives a score and feedback. Works also when the student handed nothing in (a task performance done in class). The student is told.</summary>
    public async Task<ServiceResult> GradeAsync(int teacherId, int itemId, int studentId, decimal? score, string? feedback, CancellationToken ct = default, bool keepFeedback = false)
    {
        feedback = string.IsNullOrWhiteSpace(feedback) ? null : feedback.Trim();

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var context = await ItemAccess.ForTeacherAsync(db, teacherId, itemId, tracking: false, ct);
        if (context is null)
        {
            return ServiceResult.Fail("This assignment was not found.");
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
            return ServiceResult.Fail("This student is not in the course.");
        }

        var now = Now;
        var s = await db.Submissions.FirstOrDefaultAsync(x => x.ItemId == itemId && x.StudentId == studentId, ct);
        if (s is { IsLate: true, LateDecision: LateDecisions.Pending })
        {
            return ServiceResult.Fail("This work was handed in late. Accept it first, then give the score.");
        }

        if (s is { IsLate: true, LateDecision: LateDecisions.Declined })
        {
            return ServiceResult.Fail("You did not accept this late work. Accept it first if you want to give it a score.");
        }

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
            return ServiceResult.Fail("The student's answer changed while you were grading. Open it again.");
        }

        await notifications.AddAsync(studentUserId.Value, "grade", $"{context.Item.Title} was graded",
            $"{context.SubjectName} · {s.Score:0.##} / {context.Item.MaxScore:0.##}", $"student/items/{itemId}", ct);
        return ServiceResult.Ok("Saved. The student was told.");
    }
}
