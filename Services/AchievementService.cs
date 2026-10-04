using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Components.Shared;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>A badge a student can earn. Unearned ones show how far the student is.</summary>
public sealed record Achievement(string Key, string Title, string Description, string Icon, AccentColor Color, bool Earned, int Progress, int Target, string? Detail = null);

/// <summary>
/// The achievements of a student, worked out from their scores, their handing in and what they opened. Nothing is stored: they are
/// recomputed when the dashboard is opened, so they always match the current scores.
/// </summary>
public sealed class AchievementService(IDbContextFactory<AppDbContext> dbFactory, GradeService grades, AttendanceService attendance, TimeProvider clock)
{
    public const decimal HighGradePercent = 90;
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

        // Modules: opened every posted module (at least 3 so far).
        var assignmentIds = await Roster.AssignmentsOf(db, studentId).Select(a => a.Id).ToListAsync(ct);
        var posted = await db.Modules.AsNoTracking().Where(m => assignmentIds.Contains(m.AssignmentId) && m.VisibleFromUtc != null && m.VisibleFromUtc <= now).Select(m => m.Id).ToListAsync(ct);
        var opened = await db.ModuleViews.AsNoTracking().Where(v => v.StudentId == studentId && posted.Contains(v.ModuleId)).CountAsync(ct);

        var days = await attendance.GetStudentAsync(studentId, ct);
        var perfectAttendance = days.Late == 0 && days.Absent == 0 && days.Present >= PerfectAttendanceDays;

        var average = summary.Overall;
        var highGrades = average is >= HighGradePercent && work.Count >= 3;

        var list = new List<Achievement>
        {
            PerfectAttendance(perfectAttendance, days.Late + days.Absent == 0 ? days.Present : 0, PerfectAttendanceDays,
                days.Late + days.Absent > 0 ? "A missed or late day resets this" : null),
            new("perfect-score", "Perfect score", "Get the highest score on an activity, quiz or task performance.", "star", AccentColor.Amber, perfect > 0, Math.Min(perfect, 1), 1,
                perfect > 1 ? $"{perfect} times" : null),
            new("high-grades", "High grades", $"Keep an average of {HighGradePercent:0}% or more (at least 3 graded works).", "trophy", AccentColor.Violet, highGrades, highGrades ? 1 : 0, 1,
                average is { } avg ? $"Now {avg:0.#}%" : null),
            new("quiz-ace", "Quiz ace", $"Score {HighGradePercent:0}% or more on 3 quizzes.", "award", AccentColor.Blue, quizAces >= 3, Math.Min(quizAces, 3), 3),
            new("on-time", "Always on time", $"Hand in {OnTimeTarget} works, none of them late.", "alarm-clock", AccentColor.Green, !anyLate && onTime >= OnTimeTarget, anyLate ? 0 : Math.Min(onTime, OnTimeTarget), OnTimeTarget,
                anyLate ? "A late work resets this" : null),
            new("curious", "Curious mind", "Open every module your teachers posted (at least 3).", "book-open", AccentColor.Rose, posted.Count >= 3 && opened >= posted.Count, posted.Count == 0 ? 0 : Math.Min(opened, Math.Max(posted.Count, 3)), Math.Max(posted.Count, 3)),
        };

        return list;
    }

    private static Achievement PerfectAttendance(bool earned, int daysPresent, int target, string? detail = null) =>
        new("perfect-attendance", "Perfect attendance", $"Be present on every class day (at least {target} days recorded).", "calendar-check", AccentColor.Green, earned, Math.Min(daysPresent, target), target, detail);
}
