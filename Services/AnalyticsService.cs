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

/// <summary>One student in one course, as the analytics file lists them: the grade, how it is, the work missed and the attendance.</summary>
/// <summary>
/// One student in one course of a teacher: how many of the posted modules the student opened, how many of the activities that are open the student opened, how many were handed in, and the
/// progress (handed in of what is open).
/// </summary>
public sealed record StudentActivityRow(
    int StudentId, string Student, string Lrn, string Section, string Course, int ModulesOpened, int Modules, int ItemsOpened, int Items, int HandedIn, int? Progress);

public sealed record StudentResultRow(string Student, string Lrn, string Section, string Course, decimal? Grade, string Status, int Missing, int Present, int Late, int Absent, decimal? Attendance);

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

    /// <summary>
    /// Every student of a teacher's courses, course by course, with what they opened and how far they are: the modules opened, the activities (seatwork, assignments, quizzes, task performances and
    /// exams that are open now) opened, and the progress. They are the students the teacher teaches, so the teacher sees at once who has not even opened the lessons.
    /// </summary>
    public async Task<IReadOnlyList<StudentActivityRow>> GetTeacherStudentsAsync(int teacherId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var assignments = await db.TeachingAssignments.AsNoTracking().Where(a => a.TeacherId == teacherId)
            .Select(a => new { a.Id, a.SectionId, a.SubjectId, Subject = a.Subject.Name, a.Section.GradeLevel, Strand = a.Section.Strand.Code, Section = a.Section.Name })
            .ToListAsync(ct);
        if (assignments.Count == 0)
        {
            return [];
        }

        var assignmentIds = assignments.Select(a => a.Id).ToList();
        var sectionIds = assignments.Select(a => a.SectionId).Distinct().ToList();
        var roster = (await db.EnrollmentSubjects.AsNoTracking()
            .Where(x => x.Enrollment.Status == EnrollmentStatus.Enrolled && sectionIds.Contains(x.Enrollment.SectionId))
            .Select(x => new { x.Enrollment.SectionId, x.SubjectId, x.Enrollment.StudentId, x.Enrollment.Student.FirstName, x.Enrollment.Student.LastName, x.Enrollment.Student.Lrn, x.Enrollment.Student.User.IsActive })
            .ToListAsync(ct)).Where(x => x.IsActive).ToList();

        var modules = await db.Modules.AsNoTracking()
            .Where(m => assignmentIds.Contains(m.AssignmentId) && !m.IsExamSet && m.VisibleFromUtc != null && m.VisibleFromUtc <= now)
            .Select(m => new { m.Id, m.AssignmentId, m.IsSyllabus }).ToListAsync(ct);
        var moduleIds = modules.Select(m => m.Id).ToList();
        var moduleViews = (await db.ModuleViews.AsNoTracking().Where(v => moduleIds.Contains(v.ModuleId)).Select(v => new { v.StudentId, v.ModuleId }).ToListAsync(ct))
            .GroupBy(v => v.ModuleId).ToDictionary(g => g.Key, g => g.Select(v => v.StudentId).ToHashSet());

        var items = await db.ModuleItems.AsNoTracking()
            .Where(i => assignmentIds.Contains(i.Module.AssignmentId) && i.Module.VisibleFromUtc != null && i.Module.VisibleFromUtc <= now && (i.VisibleFromUtc == null || i.VisibleFromUtc <= now))
            .Select(i => new { i.Id, i.Module.AssignmentId }).ToListAsync(ct);
        var itemIds = items.Select(i => i.Id).ToList();
        var itemViews = (await db.ItemViews.AsNoTracking().Where(v => itemIds.Contains(v.ItemId)).Select(v => new { v.StudentId, v.ItemId }).ToListAsync(ct))
            .GroupBy(v => v.ItemId).ToDictionary(g => g.Key, g => g.Select(v => v.StudentId).ToHashSet());
        var handedIn = (await db.Submissions.AsNoTracking().Where(s => itemIds.Contains(s.ItemId) && s.Status != SubmissionStatus.InProgress).Select(s => new { s.StudentId, s.ItemId }).ToListAsync(ct))
            .GroupBy(s => s.ItemId).ToDictionary(g => g.Key, g => g.Select(s => s.StudentId).ToHashSet());

        var rows = new List<StudentActivityRow>();
        foreach (var a in assignments)
        {
            var section = TeachingService.SectionLabel(a.GradeLevel, a.Strand, a.Section);
            var myModules = modules.Where(m => m.AssignmentId == a.Id).Select(m => m.Id).ToList();
            var myItems = items.Where(i => i.AssignmentId == a.Id).Select(i => i.Id).ToList();
            foreach (var st in roster.Where(r => r.SectionId == a.SectionId && r.SubjectId == a.SubjectId).DistinctBy(r => r.StudentId))
            {
                var modulesOpened = myModules.Count(id => moduleViews.TryGetValue(id, out var who) && who.Contains(st.StudentId));
                var itemsOpened = myItems.Count(id => itemViews.TryGetValue(id, out var who) && who.Contains(st.StudentId));
                var done = myItems.Count(id => handedIn.TryGetValue(id, out var who) && who.Contains(st.StudentId));
                rows.Add(new StudentActivityRow(st.StudentId, $"{st.LastName}, {st.FirstName}", st.Lrn, section, a.Subject, modulesOpened, myModules.Count, itemsOpened, myItems.Count, done,
                    myItems.Count == 0 ? null : (int)Math.Round(100.0 * done / myItems.Count, MidpointRounding.AwayFromZero)));
            }
        }

        return [.. rows.OrderBy(r => r.Course).ThenBy(r => r.Section).ThenBy(r => r.Student)];
    }

    /// <summary>
    /// The results behind the numbers of the page: every student of the person's scope (the whole school, or a teacher's own courses) in every course, with the grade, whether it is failing,
    /// at risk or passing, the work missed and the attendance. They are in the Excel file of the analytics (the page only shows the totals).
    /// </summary>
    public async Task<IReadOnlyList<StudentResultRow>> GetStudentResultsAsync(CurrentUserInfo user, CancellationToken ct = default)
    {
        var schoolWide = user.Role is AppRoles.Admin or AppRoles.GuidanceCounselor;
        if (!schoolWide && user.Role != AppRoles.Teacher)
        {
            return [];
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;

        List<int>? assignmentIds = null;
        if (!schoolWide)
        {
            assignmentIds = await db.TeachingAssignments.AsNoTracking().Where(a => a.Teacher.UserId == user.UserId).Select(a => a.Id).ToListAsync(ct);
            if (assignmentIds.Count == 0)
            {
                return [];
            }
        }

        var metrics = await StudentMetrics.LoadAsync(db, now, assignmentIds, null, ct);
        if (metrics.Count == 0)
        {
            return [];
        }

        var studentIds = metrics.Select(m => m.StudentId).Distinct().ToList();
        var people = await db.Students.AsNoTracking().Where(st => studentIds.Contains(st.Id))
            .Select(st => new { st.Id, st.Lrn, st.FirstName, st.MiddleName, st.LastName, st.NameExtension }).ToDictionaryAsync(st => st.Id, ct);
        var assignmentList = metrics.Select(m => m.AssignmentId).Distinct().ToList();
        var sections = await db.TeachingAssignments.AsNoTracking().Where(a => assignmentList.Contains(a.Id))
            .Select(a => new { a.Id, a.Section.GradeLevel, Strand = a.Section.Strand.Code, Section = a.Section.Name }).ToDictionaryAsync(a => a.Id, ct);

        return metrics
            .Select(m =>
            {
                var who = people[m.StudentId];
                var sec = sections[m.AssignmentId];
                var status = m.Percent is null ? "No grade yet"
                    : m.Percent < GradeRules.PassingPercent ? "Failing"
                    : m.Percent < GradeRules.AtRiskUntil || m.Missing >= ReferralService.MissingForRisk ? "At risk"
                    : "Passing";
                return new StudentResultRow(UserDirectory.FullName(who.FirstName, who.MiddleName, who.LastName, who.NameExtension), who.Lrn, TeachingService.SectionLabel(sec.GradeLevel, sec.Strand, sec.Section),
                    m.SubjectName, m.Percent, status, m.Missing, m.Present, m.Late, m.Absent, m.AttendanceRate);
            })
            .OrderBy(r => r.Section, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Student, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Course, StringComparer.OrdinalIgnoreCase)
            .ToList();
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

            // The guidance counselor looks after attendance and violations only; the administrator sees every kind (failing grades and students at risk are the teachers').
            var shown = user.Role == AppRoles.GuidanceCounselor ? ReferralReasons.All : [ReferralReasons.FailingGrades, ReferralReasons.AtRisk, ReferralReasons.LowAttendance, ReferralReasons.Violation];
            concerns = shown.Select(r => new ConcernCount(r, reasons.FirstOrDefault(x => x.Reason == r)?.Count ?? 0)).ToList();
            openReferrals = user.Role == AppRoles.GuidanceCounselor
                ? await db.Referrals.Where(r => r.Reason == ReferralReasons.LowAttendance || r.Reason == ReferralReasons.Violation).Select(r => r.StudentId).Distinct().CountAsync(ct)
                : await db.Referrals.CountAsync(r => r.Status != ReferralStatus.Resolved, ct);

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
            concerns = new[] { ReferralReasons.FailingGrades, ReferralReasons.AtRisk, ReferralReasons.LowAttendance }.Select(r => new ConcernCount(r, findings.Count(f => f.Reason == r))).ToList();
            openReferrals = await db.Referrals.CountAsync(r => r.Status != ReferralStatus.Resolved && r.AssignmentId != null && assignmentIds!.Contains(r.AssignmentId.Value), ct);
        }

        return new AnalyticsReport(schoolWide, schoolWide ? "the whole school" : "your courses", byStudent.Count, Avg(overall.Values.Where(v => v is not null).Select(v => v!.Value)), Pooled(metrics),
            failingStudents, atRiskStudents, openReferrals, bandsTitle, bands, attendance, concernTitle, concerns, resolved, improved, interventions, performanceTitle, performance);
    }
}
