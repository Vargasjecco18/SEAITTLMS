using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

// ---------------------------------------------------------------------- the student's dashboard

public sealed record DashboardWork(int ItemId, string Kind, string Title, string SubjectName, DateTime? DueAtUtc, bool IsOverdue, bool InProgress);

public sealed record DashboardModule(int ModuleId, string Title, string SubjectName, DateTime PostedAtUtc);

public sealed record RecentGrade(int ItemId, string Title, string SubjectName, string Kind, decimal Score, decimal Max, DateTime? GradedAtUtc);

public sealed record StudentDashboard(
    string FirstName, int OpenWork, int DueThisWeek, int Overdue, int UnopenedModules, int SubjectCount, decimal? Overall, int Failing,
    IReadOnlyList<DashboardWork> Work, IReadOnlyList<DashboardModule> Unopened, IReadOnlyList<RecentGrade> Recent, IReadOnlyList<SubjectGrade> Subjects);

// ---------------------------------------------------------------------- the teacher's dashboard

public sealed record UncheckedWork(int ItemId, string Title, string Kind, string SubjectName, string SectionLabel, int Count, DateTime OldestAtUtc);

public sealed record DraftModule(int ModuleId, int AssignmentId, string Title, string SubjectName, string SectionLabel, DateTime? VisibleFromUtc);

public sealed record FlaggedAttempt(int ItemId, string StudentName, string Title, int Warnings, string? Reason, DateTime? AtUtc);

public sealed record TeacherDashboard(
    string FirstName, int SubjectCount, int StudentCount, int ToCheck, int FlaggedCount, int DraftCount, int DueSoon,
    IReadOnlyList<UncheckedWork> Unchecked, IReadOnlyList<DraftModule> Drafts, IReadOnlyList<FlaggedAttempt> Flagged);

/// <summary>What the dashboards show. Every number is read from the person's own subjects.</summary>
public sealed class DashboardService(IDbContextFactory<AppDbContext> dbFactory, GradeService grades, TimeProvider clock)
{
    public async Task<StudentDashboard?> GetStudentAsync(int studentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;

        var student = await db.Students.AsNoTracking().Where(s => s.Id == studentId).Select(s => s.FirstName).FirstOrDefaultAsync(ct);
        if (student is null)
        {
            return null;
        }

        var summary = await grades.GetStudentSummaryAsync(studentId, ct);
        var assignmentIds = await Roster.AssignmentsOf(db, studentId).Select(a => a.Id).ToListAsync(ct);

        var items = await db.ModuleItems.AsNoTracking()
            .Where(i => assignmentIds.Contains(i.Module.AssignmentId) && i.Module.VisibleFromUtc != null && i.Module.VisibleFromUtc <= now
                        && (i.VisibleFromUtc == null || i.VisibleFromUtc <= now))
            .Select(i => new
            {
                i.Id, i.Kind, i.Title, i.DueAtUtc, Subject = i.Module.Assignment.Subject.Name,
                Status = i.Submissions.Where(s => s.StudentId == studentId).Select(s => s.Status).FirstOrDefault(),
            })
            .ToListAsync(ct);

        var notDone = items.Where(i => i.Status is null or SubmissionStatus.InProgress).ToList();
        var work = notDone
            .OrderBy(i => i.DueAtUtc == null).ThenBy(i => i.DueAtUtc)
            .Take(8)
            .Select(i => new DashboardWork(i.Id, i.Kind, i.Title, i.Subject, i.DueAtUtc, i.DueAtUtc is { } d && d < now, i.Status == SubmissionStatus.InProgress))
            .ToList();

        var opened = db.ModuleViews.Where(v => v.StudentId == studentId).Select(v => v.ModuleId);
        var unopenedQuery = db.Modules.AsNoTracking()
            .Where(m => assignmentIds.Contains(m.AssignmentId) && m.VisibleFromUtc != null && m.VisibleFromUtc <= now && !opened.Contains(m.Id));
        var unopenedCount = await unopenedQuery.CountAsync(ct);
        var unopened = await unopenedQuery.OrderByDescending(m => m.VisibleFromUtc).Take(6)
            .Select(m => new DashboardModule(m.Id, m.Title, m.Assignment.Subject.Name, m.VisibleFromUtc!.Value)).ToListAsync(ct);

        var recent = summary.Subjects
            .SelectMany(s => s.Graded.Select(g => new RecentGrade(g.ItemId, g.Title, s.SubjectName, g.Kind, g.Score, g.Max, g.GradedAtUtc)))
            .OrderByDescending(g => g.GradedAtUtc).Take(5).ToList();

        return new StudentDashboard(
            student, notDone.Count, notDone.Count(i => i.DueAtUtc is { } d && d >= now && d <= now.AddDays(7)), notDone.Count(i => i.DueAtUtc is { } d && d < now),
            unopenedCount, summary.Subjects.Count, summary.Overall, summary.FailingSubjects, work, unopened, recent, summary.Subjects);
    }

    public async Task<TeacherDashboard?> GetTeacherAsync(int teacherId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;

        var teacher = await db.Teachers.AsNoTracking().Where(t => t.Id == teacherId).Select(t => t.FirstName).FirstOrDefaultAsync(ct);
        if (teacher is null)
        {
            return null;
        }

        var assignments = await db.TeachingAssignments.AsNoTracking().Where(a => a.TeacherId == teacherId)
            .Select(a => new { a.Id, a.SectionId, a.SubjectId, a.Section.GradeLevel, Strand = a.Section.Strand.Code, Section = a.Section.Name })
            .ToListAsync(ct);
        string Label(int assignmentId) { var a = assignments.First(x => x.Id == assignmentId); return TeachingService.SectionLabel(a.GradeLevel, a.Strand, a.Section); }

        var rosters = new Dictionary<int, List<int>>();
        foreach (var a in assignments)
        {
            rosters[a.Id] = await Roster.StudentIds(db, a.SectionId, a.SubjectId).ToListAsync(ct);
        }

        var studentCount = rosters.Values.SelectMany(x => x).Distinct().Count();

        // Work waiting for a score.
        var waiting = await db.Submissions.AsNoTracking()
            .Where(s => s.Status == SubmissionStatus.Submitted && s.Item.Module.Assignment.TeacherId == teacherId)
            .Select(s => new { s.ItemId, s.Item.Title, s.Item.Kind, Subject = s.Item.Module.Assignment.Subject.Name, s.Item.Module.AssignmentId, At = s.SubmittedAtUtc ?? s.UpdatedAtUtc })
            .ToListAsync(ct);
        var unchecked_ = waiting.GroupBy(w => w.ItemId)
            .Select(g => new UncheckedWork(g.Key, g.First().Title, g.First().Kind, g.First().Subject, Label(g.First().AssignmentId), g.Count(), g.Min(x => x.At)))
            .OrderBy(u => u.OldestAtUtc).Take(8).ToList();

        // How many deadlines fall in the next week (the number on the dashboard).
        var dueSoon = await db.ModuleItems.CountAsync(i => i.Module.Assignment.TeacherId == teacherId && i.DueAtUtc != null && i.DueAtUtc >= now && i.DueAtUtc <= now.AddDays(7), ct);

        // Modules that students can't see yet.
        var draftsQuery = db.Modules.AsNoTracking().Where(m => m.Assignment.TeacherId == teacherId && (m.VisibleFromUtc == null || m.VisibleFromUtc > now));
        var draftCount = await draftsQuery.CountAsync(ct);
        var drafts = (await draftsQuery.OrderBy(m => m.VisibleFromUtc == null).ThenBy(m => m.VisibleFromUtc).ThenByDescending(m => m.Id).Take(5)
                .Select(m => new { m.Id, m.AssignmentId, m.Title, Subject = m.Assignment.Subject.Name, m.VisibleFromUtc }).ToListAsync(ct))
            .Select(m => new DraftModule(m.Id, m.AssignmentId, m.Title, m.Subject, Label(m.AssignmentId), m.VisibleFromUtc)).ToList();

        // Quiz attempts the anti-cheating watch flagged in the last month.
        var since = now.AddDays(-30);
        var flaggedQuery = db.Submissions.AsNoTracking().Where(s => s.IsFlagged && s.Item.Module.Assignment.TeacherId == teacherId && s.UpdatedAtUtc >= since);
        var flaggedCount = await flaggedQuery.CountAsync(ct);
        var flagged = (await flaggedQuery.OrderByDescending(s => s.UpdatedAtUtc).Take(5)
                .Select(s => new { s.ItemId, Name = s.Student.LastName + ", " + s.Student.FirstName, s.Item.Title, s.ViolationCount, s.AutoSubmitReason, At = s.SubmittedAtUtc })
                .ToListAsync(ct))
            .Select(s => new FlaggedAttempt(s.ItemId, s.Name, s.Title, s.ViolationCount, s.AutoSubmitReason, s.At)).ToList();

        return new TeacherDashboard(teacher, assignments.Count, studentCount, waiting.Count, flaggedCount, draftCount, dueSoon, unchecked_, drafts, flagged);
    }
}
