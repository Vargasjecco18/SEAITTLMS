using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>One student in one subject: their grade, the work they missed and their attendance. The base of the referrals and the analytics.</summary>
internal sealed record PairMetric(int StudentId, int AssignmentId, string SubjectName, decimal? Percent, int Graded, int Missing, int Present, int Late, int Absent)
{
    public int Days => Present + Late + Absent;

    public decimal? AttendanceRate => AttendanceService.Rate(Present, Late, Absent);
}

internal static class StudentMetrics
{
    /// <summary>
    /// The numbers of every (student, subject) pair of the students who are still at school (Active) and enrolled now. A grade is the points earned
    /// divided by the points possible of the graded work; work is missing when it was due, is posted, and the student has not handed it in.
    /// </summary>
    /// <param name="assignmentIds">Only these subjects (a teacher's own), or all of them when null.</param>
    /// <param name="studentId">Only this student, or everybody when null.</param>
    public static async Task<List<PairMetric>> LoadAsync(AppDbContext db, DateTime nowUtc, IReadOnlyCollection<int>? assignmentIds, int? studentId, CancellationToken ct)
    {
        var pairs = await db.EnrollmentSubjects.AsNoTracking()
            .Where(x => x.Enrollment.Status == EnrollmentStatus.Enrolled && x.Enrollment.Student.Status == StudentStatus.Active
                        && (studentId == null || x.Enrollment.StudentId == studentId))
            .Select(x => new { x.Enrollment.StudentId, x.Enrollment.SectionId, x.SubjectId })
            .Distinct().ToListAsync(ct);

        var assignments = await db.TeachingAssignments.AsNoTracking()
            .Where(a => assignmentIds == null || assignmentIds.Contains(a.Id))
            .Select(a => new { a.Id, a.SectionId, a.SubjectId, Subject = a.Subject.Name }).ToListAsync(ct);
        var bySlot = assignments.ToDictionary(a => (a.SectionId, a.SubjectId));

        var roster = pairs.Where(p => bySlot.ContainsKey((p.SectionId, p.SubjectId))).Select(p => (p.StudentId, Assignment: bySlot[(p.SectionId, p.SubjectId)])).ToList();
        if (roster.Count == 0)
        {
            return [];
        }

        var assignmentIdList = roster.Select(r => r.Assignment.Id).Distinct().ToList();
        var items = await db.ModuleItems.AsNoTracking()
            .Where(i => assignmentIdList.Contains(i.Module.AssignmentId) && i.Module.VisibleFromUtc != null && i.Module.VisibleFromUtc <= nowUtc
                        && (i.VisibleFromUtc == null || i.VisibleFromUtc <= nowUtc))
            .Select(i => new { i.Id, AssignmentId = i.Module.AssignmentId, i.MaxScore, i.DueAtUtc })
            .ToListAsync(ct);
        var itemIds = items.Select(i => i.Id).ToList();
        var students = roster.Select(r => r.StudentId).Distinct().ToList();

        var submissions = await db.Submissions.AsNoTracking()
            .Where(s => itemIds.Contains(s.ItemId) && students.Contains(s.StudentId))
            .Select(s => new { s.ItemId, s.StudentId, s.Status, s.Score })
            .ToListAsync(ct);
        var subBySlot = submissions.ToDictionary(s => (s.StudentId, s.ItemId));

        var attendance = await db.AttendanceRecords.AsNoTracking()
            .Where(r => assignmentIdList.Contains(r.AssignmentId) && students.Contains(r.StudentId))
            .Select(r => new { r.StudentId, r.AssignmentId, r.Status })
            .ToListAsync(ct);
        var attBySlot = attendance.GroupBy(a => (a.StudentId, a.AssignmentId)).ToDictionary(g => g.Key, g => g.Select(x => x.Status).ToList());

        var result = new List<PairMetric>();
        foreach (var (student, assignment) in roster)
        {
            decimal earned = 0, possible = 0;
            var graded = 0;
            var missing = 0;
            foreach (var item in items.Where(i => i.AssignmentId == assignment.Id))
            {
                subBySlot.TryGetValue((student, item.Id), out var sub);
                if (sub is { Status: SubmissionStatus.Graded, Score: { } score })
                {
                    earned += score;
                    possible += item.MaxScore;
                    graded++;
                }
                else if ((sub is null || sub.Status == SubmissionStatus.InProgress) && item.DueAtUtc is { } due && due < nowUtc)
                {
                    missing++;
                }
            }

            attBySlot.TryGetValue((student, assignment.Id), out var marks);
            marks ??= [];
            result.Add(new PairMetric(student, assignment.Id, assignment.Subject, possible <= 0 ? null : Math.Round(earned / possible * 100, 1), graded, missing,
                marks.Count(m => m == AttendanceStatus.Present), marks.Count(m => m == AttendanceStatus.Late), marks.Count(m => m == AttendanceStatus.Absent)));
        }

        return result;
    }
}
