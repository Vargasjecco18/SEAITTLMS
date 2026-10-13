using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Components.Shared;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

public sealed record BandCounts(int Outstanding, int VerySatisfactory, int Satisfactory, int Fair, int Failing)
{
    public int Total => Outstanding + VerySatisfactory + Satisfactory + Fair + Failing;
}

public sealed record GroupBands(string Label, BandCounts Bands);

/// <summary>How many times students were present, late and absent in a group (a section for the school, a course for a teacher), and the attendance rate.</summary>
public sealed record AttendanceGroup(string Label, int Present, int Late, int Absent, decimal? Rate);

public sealed record AnalyticsAttendance(int Present, int Late, int Absent, decimal? Rate, IReadOnlyList<AttendanceGroup> Groups)
{
    public static AnalyticsAttendance Empty { get; } = new(0, 0, 0, null, []);

    public int Marks => Present + Late + Absent;
}

public sealed record ConcernCount(string Reason, int Count);

public sealed record InterventionStat(string Intervention, int Total, int Improved);

public sealed record GroupPerformance(string Label, int Students, decimal? Average, decimal? Attendance, int Failing);

public sealed record AnalyticsReport(
    bool SchoolWide, string ScopeText, int Students, decimal? AverageGrade, decimal? AttendanceRate, int FailingStudents, int AtRiskStudents, int OpenReferrals,
    string BandsTitle, IReadOnlyList<GroupBands> Bands, AnalyticsAttendance Attendance,
    string ConcernTitle, IReadOnlyList<ConcernCount> Concerns,
    int Resolved, int Improved, IReadOnlyList<InterventionStat> Interventions,
    string PerformanceTitle, IReadOnlyList<GroupPerformance> Performance);

/// <summary>
/// The analytics page. The administrator and the guidance counselor see the whole school; a teacher sees only their own subjects. Everything is
/// worked out from the scores and attendance that are already recorded, so there is nothing to type in.
/// </summary>
public sealed class AnalyticsService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider clock)
{
    private static BandCounts Count(IEnumerable<decimal> percents)
    {
        int o = 0, v = 0, s = 0, f = 0, x = 0;
        foreach (var p in percents)
        {
            if (p >= 90) { o++; }
            else if (p >= 85) { v++; }
            else if (p >= 80) { s++; }
            else if (p >= GradeRules.PassingPercent) { f++; }
            else { x++; }
        }

        return new BandCounts(o, v, s, f, x);
    }

    private static decimal? Avg(IEnumerable<decimal> values)
    {
        var list = values.ToList();
        return list.Count == 0 ? null : Math.Round(list.Average(), 1);
    }

    private static decimal? Pooled(IEnumerable<PairMetric> metrics)
    {
        var list = metrics.ToList();
        return AttendanceService.Rate(list.Sum(m => m.Present), list.Sum(m => m.Late), list.Sum(m => m.Absent));
    }

    public async Task<AnalyticsReport?> GetAsync(CurrentUserInfo user, CancellationToken ct = default)
    {
        var schoolWide = user.Role is AppRoles.Admin or AppRoles.GuidanceCounselor;
        if (!schoolWide && user.Role != AppRoles.Teacher)
        {
            return null;
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;

        List<int>? assignmentIds = null;
        if (!schoolWide)
        {
            assignmentIds = await db.TeachingAssignments.AsNoTracking().Where(a => a.Teacher.UserId == user.UserId).Select(a => a.Id).ToListAsync(ct);
            if (assignmentIds.Count == 0)
            {
                return new AnalyticsReport(false, "your courses", 0, null, null, 0, 0, 0, "Performance by course", [], AnalyticsAttendance.Empty, "Students who need attention", [], 0, 0, [], "Performance by course", []);
            }
        }

        var metrics = await StudentMetrics.LoadAsync(db, now, assignmentIds, null, ct);
        var byStudent = metrics.GroupBy(m => m.StudentId).ToDictionary(g => g.Key, g => g.ToList());

        var overall = byStudent.ToDictionary(kv => kv.Key, kv => Avg(kv.Value.Where(m => m.Percent is not null).Select(m => m.Percent!.Value)));
        var failingStudents = byStudent.Count(kv => kv.Value.Any(m => m.Percent < GradeRules.PassingPercent));
        var atRiskStudents = byStudent.Count(kv => !kv.Value.Any(m => m.Percent < GradeRules.PassingPercent)
                                                    && kv.Value.Any(m => m.Percent < GradeRules.AtRiskUntil || m.Missing >= ReferralService.MissingForRisk));

        // ---- the bands: by grade level for the school, by subject for a teacher
        string bandsTitle;
        List<GroupBands> bands;
        string performanceTitle;
        List<GroupPerformance> performance;
        if (schoolWide)
        {
            var students = await db.Students.AsNoTracking().Where(s => byStudent.Keys.Contains(s.Id)).Select(s => new { s.Id, s.GradeLevel, s.StrandId }).ToDictionaryAsync(s => s.Id, ct);
            var strands = await db.Strands.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Code, ct);

            bandsTitle = "Performance by grade level";
            bands = byStudent.Keys.GroupBy(id => students.GetValueOrDefault(id)?.GradeLevel)
                .OrderBy(g => g.Key)
                .Select(g => new GroupBands(g.Key is { } gl ? $"Grade {gl}" : "Not placed", Count(g.Select(id => overall[id]).Where(p => p is not null).Select(p => p!.Value))))
                .ToList();

            performanceTitle = "Performance by strand";
            performance = byStudent.Keys.GroupBy(id => students.GetValueOrDefault(id)?.StrandId)
                .Select(g =>
                {
                    var rows = g.SelectMany(id => byStudent[id]).ToList();
                    return new GroupPerformance(g.Key is { } sid ? strands.GetValueOrDefault(sid, "?") : "Not placed", g.Count(), Avg(g.Select(id => overall[id]).Where(p => p is not null).Select(p => p!.Value)),
                        Pooled(rows), g.Count(id => byStudent[id].Any(m => m.Percent < GradeRules.PassingPercent)));
                })
                .OrderBy(p => p.Label).ToList();
        }
        else
        {
            var labels = await db.TeachingAssignments.AsNoTracking().Where(a => assignmentIds!.Contains(a.Id))
                .Select(a => new { a.Id, Subject = a.Subject.Name, a.Section.GradeLevel, Strand = a.Section.Strand.Code, Section = a.Section.Name }).ToListAsync(ct);
            string Label(int id) { var a = labels.First(x => x.Id == id); return $"{a.Subject} · {TeachingService.SectionLabel(a.GradeLevel, a.Strand, a.Section)}"; }

            bandsTitle = "Performance by course";
            bands = metrics.GroupBy(m => m.AssignmentId).OrderBy(g => Label(g.Key))
                .Select(g => new GroupBands(Label(g.Key), Count(g.Where(m => m.Percent is not null).Select(m => m.Percent!.Value)))).ToList();

            performanceTitle = "Courses at a glance";
            performance = metrics.GroupBy(m => m.AssignmentId).OrderBy(g => Label(g.Key))
                .Select(g => new GroupPerformance(Label(g.Key), g.Count(), Avg(g.Where(m => m.Percent is not null).Select(m => m.Percent!.Value)), Pooled(g), g.Count(m => m.Percent < GradeRules.PassingPercent))).ToList();
        }

        // ---- attendance: how many times present, late and absent, by section for the school and by course for a teacher
        Dictionary<int, string>? sectionLabels = null, courseLabels = null;
        var assignmentsOfMetrics = metrics.Select(m => m.AssignmentId).Distinct().ToList();
        var named = await db.TeachingAssignments.AsNoTracking().Where(a => assignmentsOfMetrics.Contains(a.Id))
            .Select(a => new { a.Id, Subject = a.Subject.Name, a.Section.GradeLevel, Strand = a.Section.Strand.Code, Section = a.Section.Name }).ToListAsync(ct);
        if (schoolWide)
        {
            sectionLabels = named.ToDictionary(a => a.Id, a => TeachingService.SectionLabel(a.GradeLevel, a.Strand, a.Section));
        }
        else
        {
            courseLabels = named.ToDictionary(a => a.Id, a => $"{a.Subject} · {TeachingService.SectionLabel(a.GradeLevel, a.Strand, a.Section)}");
        }

        string GroupLabel(int assignmentId) => schoolWide ? sectionLabels![assignmentId] : courseLabels![assignmentId];
        var groups = metrics.Where(m => m.Present + m.Late + m.Absent > 0).GroupBy(m => GroupLabel(m.AssignmentId))
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new AttendanceGroup(g.Key, g.Sum(m => m.Present), g.Sum(m => m.Late), g.Sum(m => m.Absent), Pooled(g))).ToList();
        var attendance = new AnalyticsAttendance(metrics.Sum(m => m.Present), metrics.Sum(m => m.Late), metrics.Sum(m => m.Absent), Pooled(metrics), groups);

        // ---- concerns and the help given
        string concernTitle;
        List<ConcernCount> concerns;
        var resolved = 0;
        var improved = 0;
        var interventions = new List<InterventionStat>();
        int openReferrals;
        if (schoolWide)
        {
            concernTitle = "Primary concern categories";
            var reasons = await db.Referrals.AsNoTracking().GroupBy(r => r.Reason).Select(g => new { Reason = g.Key, Count = g.Count() }).ToListAsync(ct);
            concerns = ReferralReasons.All.Select(r => new ConcernCount(r, reasons.FirstOrDefault(x => x.Reason == r)?.Count ?? 0)).ToList();
            openReferrals = await db.Referrals.CountAsync(r => r.Status != ReferralStatus.Resolved, ct);

            var done = await db.Referrals.AsNoTracking().Where(r => r.Status == ReferralStatus.Resolved && r.Outcome != null).Select(r => new { r.Intervention, r.Outcome }).ToListAsync(ct);
            resolved = done.Count;
            improved = done.Count(d => d.Outcome == ReferralOutcomes.Improved);
            interventions = done.GroupBy(d => d.Intervention ?? "Not recorded")
                .Select(g => new InterventionStat(g.Key, g.Count(), g.Count(d => d.Outcome == ReferralOutcomes.Improved))).OrderByDescending(i => i.Total).ToList();
        }
        else
        {
            concernTitle = "Students who need attention";
            var findings = ReferralService.FindingsOf(metrics);
            concerns = ReferralReasons.All.Select(r => new ConcernCount(r, findings.Count(f => f.Reason == r))).ToList();
            openReferrals = await db.Referrals.CountAsync(r => r.Status != ReferralStatus.Resolved && r.AssignmentId != null && assignmentIds!.Contains(r.AssignmentId.Value), ct);
        }

        return new AnalyticsReport(schoolWide, schoolWide ? "the whole school" : "your courses", byStudent.Count, Avg(overall.Values.Where(v => v is not null).Select(v => v!.Value)), Pooled(metrics),
            failingStudents, atRiskStudents, openReferrals, bandsTitle, bands, attendance, concernTitle, concerns, resolved, improved, interventions, performanceTitle, performance);
    }
}
