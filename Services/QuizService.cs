using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Components.Shared;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

public sealed record AttemptQuestion(int Id, int Number, string Text, string Kind, IReadOnlyList<string> Options, decimal Points, string? Answer);

/// <summary>A quiz a student is taking. It never carries the right answers.</summary>
public sealed record QuizAttempt(
    int ItemId, string Title, string SubjectName, int AssignmentId, DateTime StartedAtUtc, DateTime? EndsAtUtc,
    bool AntiCheat, int MaxViolations, int ViolationCount, IReadOnlyList<AttemptQuestion> Questions);

/// <summary>Either the attempt, or the reason the student can't take the quiz now.</summary>
public sealed record StartResult(QuizAttempt? Attempt, string? Error);

/// <summary>What happened when something suspicious was reported while a quiz was open.</summary>
public sealed record ViolationResult(int Count, int Max, bool Ended);

/// <summary>Whether a quiz was just handed in by the system (time is over, or too many warnings).</summary>
public sealed record AnswerSaved(bool Ended, string? Message);

/// <summary>
/// Taking quizzes. The questions are shown in an order that is different for every student (if the teacher wants), the answers are saved
/// as the student goes, and the quiz is graded as soon as it is handed in. When the teacher turned on the anti-cheating watch, the page
/// reports leaving the tab, copying, pasting, printing and taking a screenshot: every report is a warning, the attempt is flagged for the teacher, and after the
/// allowed number of warnings the quiz is handed in by itself. Time is checked here on the server, not only on the screen.
/// </summary>
public sealed class QuizService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider clock)
{
    /// <summary>The page may be a few seconds late sending the last answer.</summary>
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(20);

    private static readonly JsonSerializerOptions Json = new();

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    private sealed class AttemptState
    {
        public List<int> Order { get; set; } = [];
        public Dictionary<int, List<int>> OptionOrder { get; set; } = [];
        public Dictionary<int, string> Answers { get; set; } = [];
    }

    private static AttemptState ReadState(string? json)
    {
        try
        {
            return string.IsNullOrEmpty(json) ? new AttemptState() : JsonSerializer.Deserialize<AttemptState>(json, Json) ?? new AttemptState();
        }
        catch (JsonException)
        {
            return new AttemptState();
        }
    }

    private static DateTime? EndsAt(ModuleItem item, DateTime startedUtc)
    {
        DateTime? ends = item.TimeLimitMinutes is { } minutes ? startedUtc.AddMinutes(minutes) : null;
        if (item.DueAtUtc is { } due && (ends is null || due < ends))
        {
            ends = due;
        }

        return ends;
    }

    // ======================================================================= starting and resuming

    public async Task<StartResult> StartAsync(int studentId, int itemId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = Now;
        var context = await ItemAccess.ForStudentAsync(db, studentId, itemId, now, tracking: true, ct);
        if (context is null || context.Item.Kind != ItemKinds.Quiz)
        {
            return new StartResult(null, "This quiz was not found.");
        }

        var item = context.Item;
        var questions = await db.QuizQuestions.AsNoTracking().Where(q => q.ItemId == itemId).OrderBy(q => q.Position).ThenBy(q => q.Id).ToListAsync(ct);
        if (questions.Count == 0)
        {
            return new StartResult(null, "Your teacher has not added the questions yet.");
        }

        var submission = await db.Submissions.FirstOrDefaultAsync(s => s.ItemId == itemId && s.StudentId == studentId, ct);
        if (submission is not null && submission.Status != SubmissionStatus.InProgress)
        {
            return new StartResult(null, "You already handed in this quiz.");
        }

        if (submission is not null)
        {
            // Resume. If the time ran out while the student was away, the quiz is handed in now.
            if (EndsAt(item, submission.StartedAtUtc ?? now) is { } ends && now > ends + Grace)
            {
                Finalize(submission, item, questions, now, "time");
                await db.SaveChangesAsync(ct);
                return new StartResult(null, "The time for this quiz is over. Your answers were handed in.");
            }

            return new StartResult(BuildAttempt(context, submission, questions), null);
        }

        if (item.DueAtUtc is { } due && now > due)
        {
            return new StartResult(null, $"This quiz is closed. It was due {PhilippineTime.FormatDateTime(due)}.");
        }

        var state = new AttemptState();
        var ids = questions.Select(q => q.Id).ToList();
        state.Order = item.ShuffleQuestions ? ids.OrderBy(_ => Random.Shared.Next()).ToList() : ids;
        foreach (var q in questions.Where(q => q.Kind == QuestionKinds.MultipleChoice))
        {
            var count = ModuleService.SplitOptions(q.OptionsText).Count;
            var order = Enumerable.Range(0, count).ToList();
            state.OptionOrder[q.Id] = item.ShuffleOptions ? order.OrderBy(_ => Random.Shared.Next()).ToList() : order;
        }

        submission = new Submission { ItemId = itemId, StudentId = studentId, Status = SubmissionStatus.InProgress, StartedAtUtc = now, AnswersJson = JsonSerializer.Serialize(state, Json) };
        db.Submissions.Add(submission);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return new StartResult(null, "The quiz is already open in another window.");
        }

        return new StartResult(BuildAttempt(context, submission, questions), null);
    }

    private static QuizAttempt BuildAttempt(ItemContext context, Submission submission, List<QuizQuestion> questions)
    {
        var state = ReadState(submission.AnswersJson);
        var byId = questions.ToDictionary(q => q.Id);
        var order = state.Order.Where(byId.ContainsKey).ToList();
        order.AddRange(questions.Select(q => q.Id).Except(order)); // a question that is not in the saved order goes last

        var shown = new List<AttemptQuestion>();
        var number = 0;
        foreach (var id in order)
        {
            var q = byId[id];
            var options = new List<string>();
            if (q.Kind == QuestionKinds.MultipleChoice)
            {
                var all = ModuleService.SplitOptions(q.OptionsText);
                var sequence = state.OptionOrder.TryGetValue(id, out var o) && o.Count == all.Count ? o : Enumerable.Range(0, all.Count).ToList();
                options = sequence.Select(n => all[n]).ToList();
            }
            else if (q.Kind == QuestionKinds.TrueFalse)
            {
                options = ["True", "False"];
            }

            shown.Add(new AttemptQuestion(id, ++number, q.Text, q.Kind, options, q.Points, state.Answers.GetValueOrDefault(id)));
        }

        var started = submission.StartedAtUtc ?? submission.CreatedAtUtc;
        return new QuizAttempt(context.Item.Id, context.Item.Title, context.SubjectName, context.AssignmentId, started, EndsAt(context.Item, started),
            context.Item.AntiCheat, context.Item.MaxViolations, submission.ViolationCount, shown);
    }

    // ======================================================================= answering

    /// <summary>Remembers one answer. If the time is over, the quiz is handed in instead and the caller is told.</summary>
    public async Task<AnswerSaved> SaveAnswerAsync(int studentId, int itemId, int questionId, string? answer, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = Now;
        var open = await LoadOpenAsync(db, studentId, itemId, ct);
        if (open is null)
        {
            return new AnswerSaved(true, "This quiz was already handed in.");
        }

        var (submission, item, questions) = open.Value;
        if (TimeIsOver(item, submission, now))
        {
            Finalize(submission, item, questions, now, "time");
            await db.SaveChangesAsync(ct);
            return new AnswerSaved(true, "The time is over. Your answers were handed in.");
        }

        if (questions.Any(q => q.Id == questionId))
        {
            var state = ReadState(submission.AnswersJson);
            state.Answers[questionId] = (answer ?? "").Length > 500 ? answer![..500] : answer ?? "";
            submission.AnswersJson = JsonSerializer.Serialize(state, Json);
            await db.SaveChangesAsync(ct);
        }

        return new AnswerSaved(false, null);
    }

    /// <summary>Hands in the quiz with the answers on the screen. They are graded now.</summary>
    public async Task<ServiceResult> SubmitAsync(int studentId, int itemId, IReadOnlyDictionary<int, string?> answers, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = Now;
        var open = await LoadOpenAsync(db, studentId, itemId, ct);
        if (open is null)
        {
            return ServiceResult.Fail("This quiz was already handed in.");
        }

        var (submission, item, questions) = open.Value;
        var late = TimeIsOver(item, submission, now);

        // The answers are only taken if the time is not over.
        if (!late)
        {
            var state = ReadState(submission.AnswersJson);
            foreach (var q in questions.Where(q => answers.ContainsKey(q.Id)))
            {
                var text = answers[q.Id] ?? "";
                state.Answers[q.Id] = text.Length > 500 ? text[..500] : text;
            }

            submission.AnswersJson = JsonSerializer.Serialize(state, Json);
        }

        Finalize(submission, item, questions, now, late ? "time" : null);
        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok(item.ShowResult ? $"Handed in. You got {submission.Score:0.##} out of {item.MaxScore:0.##}." : "Handed in. Your teacher will share the result.");
    }

    // ======================================================================= the anti-cheating watch

    /// <summary>
    /// Counts one warning (left the tab, copied, pasted ...). Does nothing for a quiz without the watch. After the allowed number the quiz
    /// is handed in. The attempt is flagged, so the teacher sees it.
    /// </summary>
    public async Task<ViolationResult> ReportViolationAsync(int studentId, int itemId, string what, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = Now;
        var open = await LoadOpenAsync(db, studentId, itemId, ct);
        if (open is null)
        {
            return new ViolationResult(0, 0, true);
        }

        var (submission, item, questions) = open.Value;
        if (!item.AntiCheat)
        {
            return new ViolationResult(submission.ViolationCount, item.MaxViolations, false);
        }

        submission.ViolationCount++;
        submission.IsFlagged = true;
        var line = $"{PhilippineTime.FormatTime(now)} {Clean(what)}";
        submission.ViolationLog = string.IsNullOrEmpty(submission.ViolationLog) ? line : submission.ViolationLog + "\n" + line;

        var ended = submission.ViolationCount >= item.MaxViolations;
        if (ended)
        {
            Finalize(submission, item, questions, now, "violations");
        }

        await db.SaveChangesAsync(ct);
        return new ViolationResult(submission.ViolationCount, item.MaxViolations, ended);
    }

    private static string Clean(string what)
    {
        var text = new string(what.Where(c => !char.IsControl(c)).ToArray()).Trim();
        return text.Length is 0 ? "something suspicious" : text.Length > 80 ? text[..80] : text;
    }

    // ======================================================================= results and housekeeping

    /// <summary>Hands in every quiz that is still open although its time is over (the student closed the window). Run by the background worker.</summary>
    public static async Task<int> FinalizeExpiredAsync(IDbContextFactory<AppDbContext> dbFactory, DateTime nowUtc, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var open = await db.Submissions.Include(s => s.Item)
            .Where(s => s.Status == SubmissionStatus.InProgress && s.Item.Kind == ItemKinds.Quiz)
            .ToListAsync(ct);

        var done = 0;
        foreach (var s in open)
        {
            if (EndsAt(s.Item, s.StartedAtUtc ?? s.CreatedAtUtc) is not { } ends || nowUtc <= ends + Grace)
            {
                continue;
            }

            var questions = await db.QuizQuestions.AsNoTracking().Where(q => q.ItemId == s.ItemId).ToListAsync(ct);
            Finalize(s, s.Item, questions, nowUtc, "time");
            done++;
        }

        if (done > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return done;
    }

    /// <summary>The questions with the student's answers and the right ones, for the teacher, or for the student when the quiz shows results.</summary>
    internal static async Task<IReadOnlyList<AnswerReview>> ReviewAsync(AppDbContext db, int itemId, Submission submission, CancellationToken ct)
    {
        if (submission.Status == SubmissionStatus.InProgress)
        {
            return [];
        }

        var state = ReadState(submission.AnswersJson);
        var questions = await db.QuizQuestions.AsNoTracking().Where(q => q.ItemId == itemId).OrderBy(q => q.Position).ThenBy(q => q.Id).ToListAsync(ct);
        return questions.Select(q =>
        {
            var given = state.Answers.GetValueOrDefault(q.Id);
            var correct = IsCorrect(q, given);
            return new AnswerReview(q.Text, q.Kind, string.IsNullOrWhiteSpace(given) ? null : given, q.CorrectAnswer.Replace("|", " / "), correct, q.Points, correct ? q.Points : 0);
        }).ToList();
    }

    /// <summary>The result of a finished quiz for the student who took it, or null while it is still open or the teacher hides results.</summary>
    public async Task<IReadOnlyList<AnswerReview>?> GetMyReviewAsync(int studentId, int itemId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var context = await ItemAccess.ForStudentAsync(db, studentId, itemId, Now, tracking: false, ct);
        if (context is null || context.Item.Kind != ItemKinds.Quiz || !context.Item.ShowResult)
        {
            return null;
        }

        var submission = await db.Submissions.AsNoTracking().FirstOrDefaultAsync(s => s.ItemId == itemId && s.StudentId == studentId, ct);
        return submission is null || submission.Status == SubmissionStatus.InProgress ? null : await ReviewAsync(db, itemId, submission, ct);
    }

    // ======================================================================= grading

    private async Task<(Submission Submission, ModuleItem Item, List<QuizQuestion> Questions)?> LoadOpenAsync(AppDbContext db, int studentId, int itemId, CancellationToken ct)
    {
        var submission = await db.Submissions.Include(s => s.Item).FirstOrDefaultAsync(s => s.ItemId == itemId && s.StudentId == studentId && s.Status == SubmissionStatus.InProgress && s.Item.Kind == ItemKinds.Quiz, ct);
        if (submission is null)
        {
            return null;
        }

        var questions = await db.QuizQuestions.AsNoTracking().Where(q => q.ItemId == itemId).OrderBy(q => q.Position).ThenBy(q => q.Id).ToListAsync(ct);
        return (submission, submission.Item, questions);
    }

    private static bool TimeIsOver(ModuleItem item, Submission submission, DateTime now) =>
        EndsAt(item, submission.StartedAtUtc ?? now) is { } ends && now > ends + Grace;

    /// <summary>Grades the answers and marks the attempt as handed in. The caller saves.</summary>
    private static void Finalize(Submission submission, ModuleItem item, IReadOnlyList<QuizQuestion> questions, DateTime now, string? reason)
    {
        var state = ReadState(submission.AnswersJson);
        submission.Score = Math.Round(questions.Where(q => IsCorrect(q, state.Answers.GetValueOrDefault(q.Id))).Sum(q => q.Points), 2);
        submission.Status = SubmissionStatus.Graded;
        submission.SubmittedAtUtc = now;
        submission.GradedAtUtc = now;
        submission.AutoSubmitReason = reason;
        submission.IsLate = false;
        if (reason is "violations")
        {
            submission.IsFlagged = true;
        }
    }

    public static bool IsCorrect(QuizQuestion q, string? given)
    {
        if (string.IsNullOrWhiteSpace(given))
        {
            return false;
        }

        return q.Kind switch
        {
            QuestionKinds.ShortAnswer => q.CorrectAnswer.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(a => string.Equals(Normalize(a), Normalize(given), StringComparison.OrdinalIgnoreCase)),
            _ => string.Equals(q.CorrectAnswer.Trim(), given.Trim(), StringComparison.OrdinalIgnoreCase),
        };
    }

    private static string Normalize(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
