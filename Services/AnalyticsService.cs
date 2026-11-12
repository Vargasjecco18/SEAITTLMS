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

/// <summary>One student in a course, as the administrator sees it when a course is opened in "Performance by courses" (round 41).</summary>
public sealed record CourseStudentRow(string Student, string? Lrn, string Section, decimal? Percent, string Status, decimal? Attendance);

/// <summary>A course of a strand: how many students take it, their average grade, how many are failing, and who they are.</summary>
public sealed record CoursePerformanceRow(string Course, int Students, decimal? Average, int Failing, IReadOnlyList<CourseStudentRow> Roster);

/// <summary>A strand with its courses (the administrator chooses the strand first, then a course).</summary>
public sealed record StrandCourses(string Strand, IReadOnlyList<CoursePerformanceRow> Courses);

public sealed record AnalyticsReport(
    bool SchoolWide, string ScopeText, int Students, decimal? AverageGrade, decimal? AttendanceRate, int FailingStudents, int AtRiskStudents, int OpenReferrals,
    string BandsTitle, IReadOnlyList<GroupBands> Bands, AnalyticsAttendance Attendance,
    string ConcernTitle, IReadOnlyList<ConcernCount> Concerns,
    int Resolved, int Improved, IReadOnlyList<InterventionStat> Interventions,
    string PerformanceTitle, IReadOnlyList<GroupPerformance> Performance,
    int Solved = 0, int SolvedRecently = 0, IReadOnlyList<ConcernCount>? SolvedByReason = null, IReadOnlyList<ConcernStudent>? ConcernStudents = null,
    IReadOnlyList<AbsentDay>? AbsentHistory = null);

/// <summary>
/// How much a student uses the LMS, from the last <see cref="LmsActivityRules.WindowDays"/> days: visiting the LMS (the days the student signed in), viewing handouts and handing in
/// activities. The score is 0 to 100; a student who never signed in is apart (red).
/// </summary>
public static class LmsActivityRules
{
    public const int WindowDays = 14;

    /// <summary>The days of visits that give the full 40 points, the handouts viewed that give the full 30, the activities handed in that give the full 30.</summary>
    public const int FullVisitDays = 5;
    public const int FullHandouts = 5;
    public const int FullAnswers = 3;

    /// <summary>A student with this score or more is active (green); less is inactive (yellow).</summary>
    public const int ActiveFrom = 70;

    public const string Active = "Active";
    public const string Inactive = "Inactive";
    public const string NeverLoggedIn = "NeverLoggedIn";

    public static int Score(int visitDays, int handouts, int answers) =>
        (int)Math.Round(40.0 * Math.Min(visitDays, FullVisitDays) / FullVisitDays + 30.0 * Math.Min(handouts, FullHandouts) / FullHandouts + 30.0 * Math.Min(answers, FullAnswers) / FullAnswers, MidpointRounding.AwayFromZero);

    public static string StateOf(bool everSignedIn, int score) => !everSignedIn ? NeverLoggedIn : score >= ActiveFrom ? Active : Inactive;

    public static string Display(string state) => state switch { Active => "Active", Inactive => "Inactive", _ => "Never logged in" };
}

/// <summary>One student and how much they use the LMS (<see cref="LmsActivityRules"/>).</summary>
public sealed record LmsActivityRow(int StudentId, string Name, string Lrn, string? Section, byte? GradeLevel, string State, int Score, int VisitDays, int Handouts, int Answers, DateTime? LastLoginAtUtc);

/// <summary>The students by how they use the LMS: active, inactive, never logged in, and by grade level.</summary>
public sealed record LmsActivity(int Active, int Inactive, int NeverLoggedIn, IReadOnlyList<LmsActivityRow> Students, IReadOnlyList<GradeActivity> ByGrade)
{
    public int Total => Active + Inactive + NeverLoggedIn;
}

public sealed record GradeActivity(string Label, int Active, int Inactive, int NeverLoggedIn);

/// <summary>A student who was absent on a day, with the courses they were absent in (for the guidance counselor's history of absences).</summary>
public sealed record AbsentStudent(int StudentId, string Name, string Lrn, IReadOnlyList<string> Courses);

/// <summary>A school day and the students who were marked absent in at least one course that day.</summary>
public sealed record AbsentDay(DateOnly Day, IReadOnlyList<AbsentStudent> Students);

/// <summary>A student who has an attendance concern or a violation concern now (one line for each student and kind), for the guidance counselor's analytics.</summary>
/// <param name="Detail">What was found: the course of an attendance concern, "Attendance as low as 75% in 3 courses" when there are several, or the violation.</param>
public sealed record ConcernStudent(string Reason, int StudentId, string Name, string Lrn, string Detail, DateTime SinceUtc);

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

    /// <summary>
    /// "Performance by courses" for the administrator (round 41): every strand with its courses (the same course of two sections counts once), each with the students who take it, the average grade
    /// of those students in the course, how many are failing and the list of the students with their grade and attendance.
    /// </summary>
    public async Task<IReadOnlyList<StrandCourses>> GetCoursePerformanceAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var metrics = await StudentMetrics.LoadAsync(db, now, null, null, ct);
        if (metrics.Count == 0)
        {
            return [];
        }

        var assignmentIds = metrics.Select(m => m.AssignmentId).Distinct().ToList();
        var slots = await db.TeachingAssignments.AsNoTracking().Where(a => assignmentIds.Contains(a.Id))
            .Select(a => new { a.Id, a.SubjectId, Subject = a.Subject.Name, StrandId = a.Section.StrandId, Strand = a.Section.Strand.Code, StrandName = a.Section.Strand.Name, a.Section.GradeLevel, Section = a.Section.Name })
            .ToDictionaryAsync(a => a.Id, ct);
        var studentIds = metrics.Select(m => m.StudentId).Distinct().ToList();
        var people = await db.Students.AsNoTracking().Where(st => studentIds.Contains(st.Id))
            .Select(st => new { st.Id, st.Lrn, st.FirstName, st.MiddleName, st.LastName, st.NameExtension }).ToDictionaryAsync(st => st.Id, ct);

        return [.. metrics.GroupBy(m => slots[m.AssignmentId].StrandId)
            .Select(strand =>
            {
                var first = slots[strand.First().AssignmentId];
                var courses = strand.GroupBy(m => slots[m.AssignmentId].SubjectId)
                    .Select(course =>
                    {
                        var roster = course.Select(m =>
                        {
                            var who = people[m.StudentId];
                            var slot = slots[m.AssignmentId];
                            var status = m.Percent is null ? "No grade yet"
                                : m.Percent < GradeRules.PassingPercent ? "Failing"
                                : m.Percent < GradeRules.AtRiskUntil || m.Missing >= ReferralService.MissingForRisk ? "At risk"
                                : "Passing";
                            return new CourseStudentRow($"{who.LastName}, {who.FirstName}", who.Lrn, TeachingService.SectionLabel(slot.GradeLevel, slot.Strand, slot.Section), m.Percent, status, m.AttendanceRate);
                        }).OrderBy(r => r.Section, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Student, StringComparer.OrdinalIgnoreCase).ToList();
                        var graded = roster.Where(r => r.Percent is not null).Select(r => r.Percent!.Value).ToList();
                        return new CoursePerformanceRow(slots[course.First().AssignmentId].Subject, roster.Count, graded.Count == 0 ? null : Math.Round(graded.Average(), 1), roster.Count(r => r.Status == "Failing"), roster);
                    })
                    .OrderBy(c => c.Course, StringComparer.OrdinalIgnoreCase).ToList();
                return new StrandCourses($"{first.Strand} · {first.StrandName}", courses);
            })
            .OrderBy(g => g.Strand, StringComparer.OrdinalIgnoreCase)];
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
        var solved = 0;
        var solvedRecently = 0;
        var solvedByReason = new List<ConcernCount>();
        var concernStudents = new List<ConcernStudent>();
        int openReferrals;
        IReadOnlyList<AbsentDay>? absentHistory = null;
        if (schoolWide)
        {
            concernTitle = "Primary concern categories";

            // The history of the students who were absent: each school day of the last 30 days, with the students marked absent that day and the courses.
            if (user.Role == AppRoles.GuidanceCounselor)
            {
                absentHistory = await GetAbsentHistoryAsync(db, PhilippineTime.DayOf(now), 30, ct);
            }
            var reasons = await db.Referrals.AsNoTracking().GroupBy(r => r.Reason).Select(g => new { Reason = g.Key, Count = g.Count() }).ToListAsync(ct);

            // The concerns are attendance and violations (failing grades and students at risk are the teachers', in their own Analytics).
            var shown = ReferralReasons.All;
            concerns = shown.Select(r => new ConcernCount(r, reasons.FirstOrDefault(x => x.Reason == r)?.Count ?? 0)).ToList();
            openReferrals = user.Role == AppRoles.GuidanceCounselor
                ? await db.Referrals.Where(r => r.Reason == ReferralReasons.LowAttendance || r.Reason == ReferralReasons.Violation).Select(r => r.StudentId).Distinct().CountAsync(ct)
                : await db.Referrals.CountAsync(r => r.Status != ReferralStatus.Resolved, ct);

            // Solved: the concerns of attendance and violations the guidance counselor marked as solved (how many, by kind, and how many in the last 30 days).
            var solvedRows = await db.Referrals.AsNoTracking().Where(r => r.Status == ReferralStatus.Resolved && (r.Reason == ReferralReasons.LowAttendance || r.Reason == ReferralReasons.Violation))
                .Select(r => new { r.Reason, r.ResolvedAtUtc }).ToListAsync(ct);
            solved = solvedRows.Count;
            solvedRecently = solvedRows.Count(r => r.ResolvedAtUtc != null && r.ResolvedAtUtc >= now.AddDays(-30));
            solvedByReason = ReferralReasons.All.Select(r => new ConcernCount(r, solvedRows.Count(x => x.Reason == r))).ToList();

            // The students who have an attendance concern or a violation concern now (the ones the counselor has not solved).
            var open = await db.Referrals.AsNoTracking()
                .Where(r => r.Status != ReferralStatus.Resolved && (r.Reason == ReferralReasons.LowAttendance || r.Reason == ReferralReasons.Violation))
                .Select(r => new { r.StudentId, r.Reason, r.Detail, r.Metric, r.CreatedAtUtc, r.Student.FirstName, r.Student.LastName, r.Student.Lrn })
                .ToListAsync(ct);
            concernStudents = open.GroupBy(r => (r.StudentId, r.Reason))
                .Select(g => new ConcernStudent(g.Key.Reason, g.Key.StudentId, g.First().LastName + ", " + g.First().FirstName, g.First().Lrn,
                    g.Count() == 1 ? g.First().Detail : g.Any(r => r.Metric != null) ? $"Attendance as low as {g.Where(r => r.Metric != null).Min(r => r.Metric!.Value):0.#}% in {g.Count()} courses" : $"Low in {g.Count()} courses", g.Min(r => r.CreatedAtUtc)))
                .OrderBy(c => c.Name).ToList();

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
            failingStudents, atRiskStudents, openReferrals, bandsTitle, bands, attendance, concernTitle, concerns, resolved, improved, interventions, performanceTitle, performance, solved, solvedRecently, solvedByReason, concernStudents, absentHistory);
    }

    /// <summary>
    /// The students of the school by how they use the LMS in the last 14 days. A student is <b>active</b> (green) when the score is 50 or more: visiting the LMS often
    /// (up to 40 points), viewing handouts (up to 30) and handing in activities (up to 30). A student with less is <b>inactive</b> (yellow), and a student who never signed in
    /// is <b>never logged in</b> (red).
    /// </summary>
    public async Task<LmsActivity> GetLmsActivityAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var since = now.AddDays(-LmsActivityRules.WindowDays);

        var students = await db.Students.AsNoTracking().Where(s => s.Status == StudentStatus.Active && s.User.IsActive)
            .Select(s => new { s.Id, s.UserId, s.FirstName, s.LastName, s.Lrn, s.GradeLevel, s.SectionId, s.User.LastLoginAtUtc }).ToListAsync(ct);

        // The days (Philippine dates) the student signed in.
        var logins = await db.SecurityLogs.AsNoTracking().Where(l => l.Kind == SecurityEvents.SignInSucceeded && l.UserId != null && l.CreatedAtUtc >= since)
            .Select(l => new { UserId = l.UserId!.Value, l.CreatedAtUtc }).ToListAsync(ct);
        var visitDays = logins.GroupBy(l => l.UserId).ToDictionary(g => g.Key, g => g.Select(l => PhilippineTime.DayOf(l.CreatedAtUtc)).Distinct().Count());

        var views = (await db.ModuleViews.AsNoTracking().Where(v => v.FirstOpenedAtUtc >= since).GroupBy(v => v.StudentId).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct)).ToDictionary(x => x.Key, x => x.N);
        var answers = (await db.Submissions.AsNoTracking().Where(x => x.SubmittedAtUtc != null && x.SubmittedAtUtc >= since && x.Status != SubmissionStatus.InProgress)
            .GroupBy(x => x.StudentId).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct)).ToDictionary(x => x.Key, x => x.N);
        var sections = await db.Sections.AsNoTracking().Select(x => new { x.Id, x.GradeLevel, Strand = x.Strand.Code, x.Name }).ToListAsync(ct);
        var labels = sections.ToDictionary(x => x.Id, x => TeachingService.SectionLabel(x.GradeLevel, x.Strand, x.Name));

        var rows = students.Select(s =>
        {
            var visits = visitDays.GetValueOrDefault(s.UserId);
            var handouts = views.GetValueOrDefault(s.Id);
            var done = answers.GetValueOrDefault(s.Id);
            var score = LmsActivityRules.Score(visits, handouts, done);
            return new LmsActivityRow(s.Id, $"{s.LastName}, {s.FirstName}", s.Lrn, s.SectionId is { } sec ? labels.GetValueOrDefault(sec) : null, s.GradeLevel,
                LmsActivityRules.StateOf(s.LastLoginAtUtc is not null, score), score, visits, handouts, done, s.LastLoginAtUtc);
        }).OrderBy(r => r.State == LmsActivityRules.NeverLoggedIn ? 0 : r.State == LmsActivityRules.Inactive ? 1 : 2).ThenBy(r => r.Score).ThenBy(r => r.Name).ToList();

        var byGrade = rows.Where(r => r.GradeLevel is not null).GroupBy(r => r.GradeLevel!.Value).OrderBy(g => g.Key)
            .Select(g => new GradeActivity($"Grade {g.Key}", g.Count(r => r.State == LmsActivityRules.Active), g.Count(r => r.State == LmsActivityRules.Inactive), g.Count(r => r.State == LmsActivityRules.NeverLoggedIn))).ToList();
        return new LmsActivity(rows.Count(r => r.State == LmsActivityRules.Active), rows.Count(r => r.State == LmsActivityRules.Inactive), rows.Count(r => r.State == LmsActivityRules.NeverLoggedIn), rows, byGrade);
    }

    /// <summary>The students marked absent on each of the last <paramref name="days"/> days (today included), the newest day first. A day with nobody absent is left out.</summary>
    internal static async Task<IReadOnlyList<AbsentDay>> GetAbsentHistoryAsync(AppDbContext db, DateOnly today, int days, CancellationToken ct)
    {
        var from = today.AddDays(-(days - 1));
        var rows = await db.AttendanceRecords.AsNoTracking()
            .Where(a => a.Status == AttendanceStatus.Absent && a.AttendedOn >= from && a.AttendedOn <= today && a.Student.Status == StudentStatus.Active)
            .Select(a => new { a.AttendedOn, a.StudentId, a.Student.FirstName, a.Student.LastName, a.Student.Lrn, Subject = a.Assignment.Subject.Name })
            .ToListAsync(ct);
        return [.. rows.GroupBy(r => r.AttendedOn).OrderByDescending(g => g.Key)
            .Select(g => new AbsentDay(g.Key,
                [.. g.GroupBy(r => r.StudentId)
                    .Select(x => new AbsentStudent(x.Key, $"{x.First().LastName}, {x.First().FirstName}", x.First().Lrn, [.. x.Select(r => r.Subject).Distinct().OrderBy(n => n)]))
                    .OrderBy(x => x.Name)]))];
    }
}
