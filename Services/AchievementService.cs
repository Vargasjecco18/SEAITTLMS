using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Components.Shared;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>A badge a student can earn. Unearned ones show how far the student is.</summary>
public sealed record Achievement(string Key, string Title, string Description, string Icon, AccentColor Color, bool Earned, int Progress, int Target, string? Detail = null);

/// <summary>
/// The achievements of a student, worked out from their scores, their handing in and what they opened. They are recomputed when the dashboard
/// is opened, so they always match the current scores. Only the fact that an achievement was earned is stored (<c>achievement_awards</c>), so the
/// "you earned it" box is shown once.
/// </summary>
public sealed class AchievementService(IDbContextFactory<AppDbContext> dbFactory, GradeService grades, AttendanceService attendance, TimeProvider clock)
{
    public const decimal HighGradePercent = 90;
    public const decimal TopGradePercent = 95;
    public const int OnTimeTarget = 5;

    /// <summary>Days marked present, with no late and no absent day, for the perfect attendance badge.</summary>
    public const int PerfectAttendanceDays = 5;

    public async Task<IReadOnlyList<Achievement>> GetAsync(int studentId, CancellationToken ct = default)
    {
        var summary = await grades.GetStudentSummaryAsync(studentId, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var work = summary.AllGraded.ToList();

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var perfect = work.Count(w => w.Max > 0 && w.Score >= w.Max);
        var quizAces = work.Count(w => w.Kind == ItemKinds.Quiz && w.Percent >= HighGradePercent);

        // Handed-in work (graded or not) and how much of it was late.
        var handedIn = await db.Submissions.AsNoTracking()
            .Where(s => s.StudentId == studentId && s.Status != SubmissionStatus.InProgress && s.Item.Kind != ItemKinds.Quiz && s.Item.Module.VisibleFromUtc <= now)
            .Select(s => s.IsLate).ToListAsync(ct);
        var onTime = handedIn.Count(late => !late);
        var anyLate = handedIn.Any(late => late);

        // Quizzes taken, and the works handed in at least a day before they were due.
        var quizzesDone = await db.Submissions.AsNoTracking()
            .CountAsync(s => s.StudentId == studentId && s.Status != SubmissionStatus.InProgress && s.Item.Kind == ItemKinds.Quiz, ct);
        var early = (await db.Submissions.AsNoTracking()
                .Where(s => s.StudentId == studentId && s.Status != SubmissionStatus.InProgress && s.Item.Kind != ItemKinds.Quiz && s.Item.DueAtUtc != null && s.SubmittedAtUtc != null)
                .Select(s => new { At = s.SubmittedAtUtc!.Value, Due = s.Item.DueAtUtc!.Value }).ToListAsync(ct))
            .Count(x => x.At <= x.Due.AddDays(-1));
        var kindsGraded = work.Select(w => w.Kind).Distinct().Count(k => k is ItemKinds.Activity or ItemKinds.Quiz or ItemKinds.TaskPerformance);
        var missing = summary.Subjects.Sum(x => x.Missing);
        var totalWork = summary.Subjects.Sum(x => x.TotalItems);

        // Modules: opened every posted module (at least 3 so far).
        var assignmentIds = await Roster.AssignmentsOf(db, studentId).Select(a => a.Id).ToListAsync(ct);
        var posted = await db.Modules.AsNoTracking().Where(m => assignmentIds.Contains(m.AssignmentId) && !m.IsExamSet && m.VisibleFromUtc != null && m.VisibleFromUtc <= now).Select(m => m.Id).ToListAsync(ct);
        var opened = await db.ModuleViews.AsNoTracking().Where(v => v.StudentId == studentId && posted.Contains(v.ModuleId)).CountAsync(ct);

        var days = await attendance.GetStudentAsync(studentId, ct);
        var perfectAttendance = days.Late == 0 && days.Absent == 0 && days.Present >= PerfectAttendanceDays;

        var average = summary.Overall;
        var highGrades = average is >= HighGradePercent && work.Count >= 3;

        var list = new List<Achievement>
        {
            PerfectAttendance(perfectAttendance, days.Late + days.Absent == 0 ? days.Present : 0, PerfectAttendanceDays,
                days.Late + days.Absent > 0 ? "A missed or late day resets this" : null),
            new("perfect-score", "Perfect score", "Get the highest score on an assignment, quiz or task performance.", "star", AccentColor.Amber, perfect > 0, Math.Min(perfect, 1), 1,
                perfect > 1 ? $"{perfect} times" : null),
            new("high-grades", "High grades", $"Keep an average of {HighGradePercent:0}% or more (at least 3 graded assignments).", "trophy", AccentColor.Violet, highGrades, highGrades ? 1 : 0, 1,
                average is { } avg ? $"Now {avg:0.#}%" : null),
            new("quiz-ace", "Quiz ace", $"Score {HighGradePercent:0}% or more on 3 quizzes.", "award", AccentColor.Blue, quizAces >= 3, Math.Min(quizAces, 3), 3),
            new("on-time", "Always on time", $"Hand in {OnTimeTarget} assignments, none of them late.", "alarm-clock", AccentColor.Green, !anyLate && onTime >= OnTimeTarget, anyLate ? 0 : Math.Min(onTime, OnTimeTarget), OnTimeTarget,
                anyLate ? "A late assignment resets this" : null),
            new("curious", "Curious mind", "Open every module your teachers posted (at least 3).", "book-open", AccentColor.Rose, posted.Count >= 3 && opened >= posted.Count, posted.Count == 0 ? 0 : Math.Min(opened, Math.Max(posted.Count, 3)), Math.Max(posted.Count, 3)),

            // ---- more to earn
            new("first-step", "First step", "Hand in your first assignment.", "send", AccentColor.Blue, handedIn.Count + quizzesDone >= 1, Math.Min(handedIn.Count + quizzesDone, 1), 1),
            new("quiz-taker", "Quiz taker", "Take your first quiz.", "file-question", AccentColor.Violet, quizzesDone >= 1, Math.Min(quizzesDone, 1), 1),
            new("triple-perfect", "Triple perfect", "Get the highest score 3 times.", "medal", AccentColor.Amber, perfect >= 3, Math.Min(perfect, 3), 3),
            new("all-rounder", "All-rounder", "Have a graded assignment, a graded quiz and a graded task performance.", "layers", AccentColor.Blue, kindsGraded >= 3, kindsGraded, 3),
            new("early-bird", "Early bird", "Hand in 3 assignments at least a day before they are due.", "sparkles", AccentColor.Green, early >= 3, Math.Min(early, 3), 3),
            new("bookworm", "Bookworm", "Open 10 modules.", "book-marked", AccentColor.Rose, opened >= 10, Math.Min(opened, 10), 10),
            new("top-of-class", "Top of the class", $"Keep an average of {TopGradePercent:0}% or more (at least 5 graded assignments).", "trending-up", AccentColor.Violet, average is >= TopGradePercent && work.Count >= 5, average is >= TopGradePercent && work.Count >= 5 ? 1 : 0, 1,
                average is { } top ? $"Now {top:0.#}%" : null),
            new("nothing-missed", "Nothing missed", "Have no missing assignments, with at least 5 assignments posted.", "circle-check", AccentColor.Green, totalWork >= 5 && missing == 0, missing > 0 ? 0 : Math.Min(totalWork, 5), 5,
                missing > 0 ? $"{missing} missing" : null),
        };

        return list;
    }

    // ======================================================================= the "you earned it" box

    /// <summary>
    /// Writes down the achievements the student has earned (once each) and gives back the ones the student has not been shown yet, oldest first.
    /// The dashboard shows a box in the middle of the screen for each, then calls <see cref="MarkSeenAsync"/>.
    /// </summary>
    public async Task<IReadOnlyList<Achievement>> GetUnseenAsync(int studentId, CancellationToken ct = default)
    {
        var all = await GetAsync(studentId, ct);
        var earned = all.Where(a => a.Earned).ToList();
        if (earned.Count == 0)
        {
            return [];
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var known = (await db.AchievementAwards.AsNoTracking().Where(a => a.StudentId == studentId).Select(a => a.AchievementKey).ToListAsync(ct)).ToHashSet();
        var fresh = earned.Where(a => !known.Contains(a.Key)).ToList();
        if (fresh.Count > 0)
        {
            db.AchievementAwards.AddRange(fresh.Select(a => new AchievementAward { StudentId = studentId, AchievementKey = a.Key, AwardedAtUtc = now }));
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // The dashboard was opened twice at once: the other one wrote them.
            }
        }

        var unseen = await db.AchievementAwards.AsNoTracking()
            .Where(a => a.StudentId == studentId && a.SeenAtUtc == null)
            .OrderBy(a => a.AwardedAtUtc).ThenBy(a => a.Id)
            .Select(a => a.AchievementKey).ToListAsync(ct);
        return unseen.Select(key => earned.FirstOrDefault(a => a.Key == key)).OfType<Achievement>().ToList();
    }

    /// <summary>The student has seen the box of these achievements: it is not shown again.</summary>
    public async Task MarkSeenAsync(int studentId, IReadOnlyCollection<string> keys, CancellationToken ct = default)
    {
        if (keys.Count == 0)
        {
            return;
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        await db.AchievementAwards.Where(a => a.StudentId == studentId && keys.Contains(a.AchievementKey) && a.SeenAtUtc == null)
            .ExecuteUpdateAsync(set => set.SetProperty(a => a.SeenAtUtc, now), ct);
    }

    private static Achievement PerfectAttendance(bool earned, int daysPresent, int target, string? detail = null) =>
        new("perfect-attendance", "Perfect attendance", $"Be present on every class day (at least {target} days recorded).", "calendar-check", AccentColor.Green, earned, Math.Min(daysPresent, target), target, detail);
}
