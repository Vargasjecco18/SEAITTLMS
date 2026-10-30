using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Components.Shared;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

public sealed record ReferralRow(
    int Id, int StudentId, string StudentName, string Lrn, string? Placement, int? AssignmentId, string? SubjectName, string? TeacherName,
    string Reason, string Detail, decimal? Metric, string Status, string? Intervention, string? Notes, string? Outcome, DateTime CreatedAtUtc, DateTime? ResolvedAtUtc,
    byte? GradeLevel = null, string? Strand = null);

public sealed record ReferralPage(IReadOnlyList<ReferralRow> Items, int Total, int Page, int PageCount);

/// <summary>One student with all their referrals (a student can have a problem in several subjects). The list shows one row for each student.</summary>
public sealed record ReferralStudentRow(
    int StudentId, string StudentName, string Lrn, byte? GradeLevel, string? Strand, string? Placement, IReadOnlyList<ReferralRow> Referrals)
{
    /// <summary>Since when the student has a concern (the first one), or nothing when there is none.</summary>
    public DateTime? SinceUtc => Referrals.Count == 0 ? null : Referrals.Min(r => r.CreatedAtUtc);

    /// <summary>The different concerns: violations first, then attendance.</summary>
    public IReadOnlyList<string> Reasons => Referrals.Select(r => r.Reason).Distinct()
        .OrderBy(x => x == ReferralReasons.Violation ? 0 : 1).ToList();
}

public sealed record ReferralStudentPage(IReadOnlyList<ReferralStudentRow> Items, int Total, int Referrals, int Page, int PageCount);

/// <summary>A student the system found with a problem now (whether or not a referral exists yet).</summary>
public sealed record AtRiskRow(
    int StudentId, string StudentName, string Lrn, string? Placement, int AssignmentId, string SubjectName, string TeacherName,
    string Reason, decimal? Metric, string Detail, int? ReferralId, string? ReferralStatus);

/// <param name="Attendance">Students with an attendance concern. <paramref name="Violations"/>: students with a violation. <paramref name="Students"/>: students with any concern.</param>
public sealed record GuidanceOverview(int Attendance, int Violations, int Students, IReadOnlyList<ReferralRow> Newest);

public static class InterventionTypes
{
    public static readonly string[] All =
    [
        "Talked with the student",
        "Called the parents",
        "Talked with the teacher",
        "Other",
    ];
}

/// <summary>
/// The guidance counselor's concerns are attendance and violations only (failing grades and students at risk are the teacher's). The system looks at every student by itself
/// (every few minutes): an attendance rate under 80% over at least 5 marked days becomes one attendance referral, once. The counselor
/// can also record a concern (attendance or a violation) for any student of the school.
/// </summary>
public sealed class ReferralService(IDbContextFactory<AppDbContext> dbFactory, NotificationService notifications, TimeProvider clock)
{
    /// <summary>Marked days needed before the attendance rate counts.</summary>
    public const int MinAttendanceDays = 5;

    /// <summary>Missed works that put a student at risk.</summary>
    public const int MissingForRisk = 3;

    /// <summary>After a referral is resolved the same problem is not sent again for this long.</summary>
    public const int CoolDownDays = 14;

    private const int PageSize = 15;

    private const int StudentPageSize = 10;

    internal sealed record Finding(int StudentId, int AssignmentId, string SubjectName, string Reason, decimal? Metric, string Detail);

    internal static List<Finding> FindingsOf(IEnumerable<PairMetric> metrics)
    {
        var list = new List<Finding>();
        foreach (var m in metrics)
        {
            if (m.Percent is { } p && p < GradeRules.PassingPercent)
            {
                list.Add(new Finding(m.StudentId, m.AssignmentId, m.SubjectName, ReferralReasons.FailingGrades, p, $"{m.SubjectName}: {p:0.#}% (passing is {GradeRules.PassingPercent:0})"));
            }
            else if ((m.Percent is { } near && near < GradeRules.AtRiskUntil) || m.Missing >= MissingForRisk)
            {
                var parts = new List<string>();
                if (m.Percent is { } q && q < GradeRules.AtRiskUntil)
                {
                    parts.Add($"{q:0.#}%, close to failing");
                }

                if (m.Missing >= MissingForRisk)
                {
                    parts.Add($"{m.Missing} assignments missing");
                }

                list.Add(new Finding(m.StudentId, m.AssignmentId, m.SubjectName, ReferralReasons.AtRisk, m.Percent, $"{m.SubjectName}: {string.Join(", ", parts)}"));
            }

            if (m.Days >= MinAttendanceDays && m.AttendanceRate is { } rate && rate < AttendanceService.LowAttendancePercent)
            {
                list.Add(new Finding(m.StudentId, m.AssignmentId, m.SubjectName, ReferralReasons.LowAttendance, rate, $"{m.SubjectName}: attendance {rate:0.#}% ({m.Present + m.Late} of {m.Days} days)"));
            }
        }

        return list;
    }

    // ======================================================================= the system makes referrals

    /// <summary>Makes a referral for every problem that has none yet. Returns how many were made. Open referrals get their numbers brought up to date.</summary>
    public async Task<int> EvaluateAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        // Failing grades and students at risk are for the teachers; the guidance counselor gets the attendance problems.
        var findings = FindingsOf(await StudentMetrics.LoadAsync(db, now, null, null, ct)).Where(f => f.Reason == ReferralReasons.LowAttendance).ToList();

        var students = findings.Select(f => f.StudentId).Distinct().ToList();
        var coolDownStart = now.AddDays(-CoolDownDays);
        var open = await db.Referrals.Where(r => students.Contains(r.StudentId) && (r.Status != ReferralStatus.Resolved || r.ResolvedAtUtc >= coolDownStart)).ToListAsync(ct);
        var byKey = open.Where(r => r.AssignmentId is not null).GroupBy(r => (r.StudentId, r.AssignmentId!.Value, r.Reason)).ToDictionary(g => g.Key, g => g.First());

        var created = new List<(int StudentId, string Detail)>();
        foreach (var f in findings)
        {
            if (byKey.TryGetValue((f.StudentId, f.AssignmentId, f.Reason), out var existing))
            {
                if (existing.Status != ReferralStatus.Resolved)
                {
                    existing.Detail = f.Detail;
                    existing.Metric = f.Metric;
                }

                continue;
            }

            db.Referrals.Add(new Referral { StudentId = f.StudentId, AssignmentId = f.AssignmentId, Reason = f.Reason, Detail = f.Detail, Metric = f.Metric });
            created.Add((f.StudentId, f.Detail));
        }

        await db.SaveChangesAsync(ct);

        if (created.Count > 0)
        {
            var counselors = await db.Users.AsNoTracking().Where(u => u.IsActive && u.RoleId == AppRoles.GuidanceCounselorId).Select(u => u.Id).ToListAsync(ct);
            var names = await db.Students.AsNoTracking().Where(s => created.Select(c => c.StudentId).Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.LastName + ", " + s.FirstName, ct);
            if (created.Count == 1)
            {
                var (studentId, detail) = created[0];
                await notifications.AddForManyAsync(counselors, "referral", $"New referral: {names[studentId]}", detail, "guidance/referrals", ct);
            }
            else
            {
                var sample = string.Join("; ", created.Select(c => names[c.StudentId]).Distinct().Take(3));
                await notifications.AddForManyAsync(counselors, "referral", $"{created.Count} new referrals", sample + (created.Select(c => c.StudentId).Distinct().Count() > 3 ? " and more" : ""), "guidance/referrals", ct);
            }
        }

        return created.Count;
    }

    // ======================================================================= what the counselor sees

    public async Task<GuidanceOverview> GetOverviewAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var mine = db.Referrals.AsNoTracking().Where(r => r.Reason == ReferralReasons.LowAttendance || r.Reason == ReferralReasons.Violation);
        var newest = await RowsAsync(db, mine.OrderByDescending(r => r.CreatedAtUtc).ThenByDescending(r => r.Id).Take(6), ct);
        return new GuidanceOverview(
            await mine.Where(r => r.Reason == ReferralReasons.LowAttendance).Select(r => r.StudentId).Distinct().CountAsync(ct),
            await mine.Where(r => r.Reason == ReferralReasons.Violation).Select(r => r.StudentId).Distinct().CountAsync(ct),
            await mine.Select(r => r.StudentId).Distinct().CountAsync(ct),
            newest);
    }

    private static async Task<IQueryable<Referral>> FilteredAsync(AppDbContext db, string? status, string? reason, string? search, int? gradeLevel, CancellationToken ct)
    {
        // Only attendance and violations are the counselor's (older referrals about grades are not listed).
        var query = db.Referrals.AsNoTracking().Where(r => r.Reason == ReferralReasons.LowAttendance || r.Reason == ReferralReasons.Violation);
        if (gradeLevel is 11 or 12)
        {
            query = query.Where(r => r.Student.GradeLevel == gradeLevel);
        }

        if (status is not null && ReferralStatus.All.Contains(status))
        {
            query = query.Where(r => r.Status == status);
        }

        if (reason is not null && ReferralReasons.All.Contains(reason))
        {
            query = query.Where(r => r.Reason == reason);
        }

        // Every word typed must match something of the referral: the student (name, LRN), the grade level, the section, the course (strand),
        // the subject, the teacher of the subject or the problem (the sentence that says what was found is not searched: "failing" finds the failing grades,
        // not the at-risk ones that say "close to failing").
        var tokens = (search ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(6).ToList();
        if (tokens.Count > 0)
        {
            var strands = await db.Strands.AsNoTracking().Select(x => new { x.Id, x.Code, x.Name }).ToListAsync(ct);
            var sections = (await db.Sections.AsNoTracking().Select(x => new { x.Id, x.GradeLevel, Strand = x.Strand.Code, x.Name }).ToListAsync(ct))
                .Select(x => new { x.Id, x.Name, Label = TeachingService.SectionLabel(x.GradeLevel, x.Strand, x.Name) }).ToList();

            foreach (var token in tokens)
            {
                var t = token;
                var strandIds = strands.Where(x => x.Code.Contains(t, StringComparison.OrdinalIgnoreCase) || (t.Length >= 3 && x.Name.Contains(t, StringComparison.OrdinalIgnoreCase))).Select(x => x.Id).ToList();
                var sectionIds = sections.Where(x => string.Equals(x.Name, t, StringComparison.OrdinalIgnoreCase) || (t.Length >= 3 && x.Label.Contains(t, StringComparison.OrdinalIgnoreCase))).Select(x => x.Id).ToList();
                var reasons = ReferralReasons.All.Where(x => t.Length >= 2 && ReferralReasons.Display(x).Contains(t, StringComparison.OrdinalIgnoreCase)).ToList();
                byte? level = t is "11" or "12" ? byte.Parse(t) : null;
                var gradeWord = t.Length >= 3 && "grade".StartsWith(t, StringComparison.OrdinalIgnoreCase);

                // "11" or "12" alone is the grade level (it would also be found inside an LRN, which is not what somebody who types 12 means).
                if (level != null)
                {
                    query = query.Where(r => r.Student.GradeLevel == level);
                    continue;
                }

                query = query.Where(r => r.Student.FirstName.Contains(t) || r.Student.LastName.Contains(t) || r.Student.Lrn.Contains(t)
                    || (r.Assignment != null && (r.Assignment.Subject.Name.Contains(t) || r.Assignment.Teacher.FirstName.Contains(t) || r.Assignment.Teacher.LastName.Contains(t)))
                    || (r.Student.StrandId != null && strandIds.Contains(r.Student.StrandId.Value))
                    || (r.Student.SectionId != null && sectionIds.Contains(r.Student.SectionId.Value))
                    || reasons.Contains(r.Reason)
                    || gradeWord);
            }
        }

        return query;
    }

    public async Task<ReferralPage> ListAsync(string? status, string? reason, string? search, int page, int? gradeLevel = null, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var query = await FilteredAsync(db, status, reason, search, gradeLevel, ct);

        var total = await query.CountAsync(ct);
        var pageCount = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Clamp(page, 1, pageCount);
        var rows = await RowsAsync(db, query.OrderBy(r => r.Status == ReferralStatus.Resolved).ThenByDescending(r => r.CreatedAtUtc).ThenByDescending(r => r.Id).Skip((page - 1) * PageSize).Take(PageSize), ct);
        return new ReferralPage(rows, total, page, pageCount);
    }

    /// <summary>The referrals grouped by student: one row for each student, with all their referrals inside. A page has <see cref="StudentPageSize"/> students.</summary>
    public async Task<ReferralStudentPage> ListByStudentAsync(string? status, string? reason, string? search, int page, int? gradeLevel = null, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var query = await FilteredAsync(db, status, reason, search, gradeLevel, ct);

        var total = await query.Select(r => r.StudentId).Distinct().CountAsync(ct);
        var referralCount = await query.CountAsync(ct);
        var pageCount = Math.Max(1, (int)Math.Ceiling(total / (double)StudentPageSize));
        page = Math.Clamp(page, 1, pageCount);

        // The newest first.
        var studentIds = await query.GroupBy(r => r.StudentId)
            .Select(g => new { StudentId = g.Key, Newest = g.Max(r => r.CreatedAtUtc) })
            .OrderByDescending(g => g.Newest).ThenByDescending(g => g.StudentId)
            .Skip((page - 1) * StudentPageSize).Take(StudentPageSize)
            .Select(g => g.StudentId).ToListAsync(ct);

        var rows = await RowsAsync(db, query.Where(r => studentIds.Contains(r.StudentId)).OrderByDescending(r => r.CreatedAtUtc).ThenByDescending(r => r.Id), ct);
        var items = studentIds
            .Select(id => rows.Where(r => r.StudentId == id).ToList())
            .Where(list => list.Count > 0)
            .Select(list => new ReferralStudentRow(list[0].StudentId, list[0].StudentName, list[0].Lrn, list[0].GradeLevel, list[0].Strand, list[0].Placement, list))
            .ToList();
        return new ReferralStudentPage(items, total, referralCount, page, pageCount);
    }

    /// <summary>
    /// Every student of the school (not only the ones with a concern), by name, with their concerns inside when they have any. The counselor can search by name, LRN, grade level,
    /// course or section and record a concern for anybody.
    /// </summary>
    public async Task<ReferralStudentPage> ListAllStudentsAsync(string? search, int page, int? gradeLevel = null, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var query = db.Students.AsNoTracking().Where(s => s.Status == StudentStatus.Active && s.User.IsActive);
        if (gradeLevel is 11 or 12)
        {
            query = query.Where(s => s.GradeLevel == gradeLevel);
        }

        var tokens = (search ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(6).ToList();
        if (tokens.Count > 0)
        {
            var strands = await db.Strands.AsNoTracking().Select(x => new { x.Id, x.Code }).ToListAsync(ct);
            var sections = (await db.Sections.AsNoTracking().Select(x => new { x.Id, x.GradeLevel, Strand = x.Strand.Code, x.Name }).ToListAsync(ct))
                .Select(x => new { x.Id, x.Name, Label = TeachingService.SectionLabel(x.GradeLevel, x.Strand, x.Name) }).ToList();
            foreach (var token in tokens)
            {
                var t = token;
                var strandIds = strands.Where(x => x.Code.Contains(t, StringComparison.OrdinalIgnoreCase)).Select(x => x.Id).ToList();
                var sectionIds = sections.Where(x => string.Equals(x.Name, t, StringComparison.OrdinalIgnoreCase) || (t.Length >= 3 && x.Label.Contains(t, StringComparison.OrdinalIgnoreCase))).Select(x => x.Id).ToList();
                if (t is "11" or "12")
                {
                    var level = byte.Parse(t);
                    query = query.Where(s => s.GradeLevel == level);
                    continue;
                }

                query = query.Where(s => s.FirstName.Contains(t) || s.LastName.Contains(t) || s.Lrn.Contains(t)
                    || (s.StrandId != null && strandIds.Contains(s.StrandId.Value))
                    || (s.SectionId != null && sectionIds.Contains(s.SectionId.Value)));
            }
        }

        var total = await query.CountAsync(ct);
        var pageCount = Math.Max(1, (int)Math.Ceiling(total / (double)StudentPageSize));
        page = Math.Clamp(page, 1, pageCount);
        var students = await query.OrderBy(s => s.LastName).ThenBy(s => s.FirstName).ThenBy(s => s.Id)
            .Skip((page - 1) * StudentPageSize).Take(StudentPageSize)
            .Select(s => new { s.Id, s.FirstName, s.LastName, s.Lrn, s.SectionId, s.GradeLevel, s.StrandId })
            .ToListAsync(ct);

        var ids = students.Select(s => s.Id).ToList();
        var rows = await RowsAsync(db, db.Referrals.AsNoTracking()
            .Where(r => ids.Contains(r.StudentId) && (r.Reason == ReferralReasons.LowAttendance || r.Reason == ReferralReasons.Violation))
            .OrderByDescending(r => r.CreatedAtUtc).ThenByDescending(r => r.Id), ct);
        var sectionLabels = await SectionLabelsAsync(db, ct);
        var strandCodes = await db.Strands.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Code, ct);
        var items = students.Select(s => new ReferralStudentRow(
            s.Id, $"{s.LastName}, {s.FirstName}", s.Lrn, s.GradeLevel, s.StrandId is { } sid ? strandCodes.GetValueOrDefault(sid) : null,
            s.SectionId is { } sec ? sectionLabels.GetValueOrDefault(sec) : null, rows.Where(r => r.StudentId == s.Id).ToList())).ToList();
        return new ReferralStudentPage(items, total, rows.Count, page, pageCount);
    }

    /// <summary>The counselor records a concern about a student: attendance, or a violation of the school rules (what happened, up to 500 characters).</summary>
    public async Task<ServiceResult> AddConcernAsync(int studentId, string reason, string? detail, int actingUserId, CancellationToken ct = default)
    {
        detail = string.IsNullOrWhiteSpace(detail) ? null : detail.Trim();
        if (!ReferralReasons.All.Contains(reason))
        {
            return ServiceResult.Fail("Choose attendance or violation.");
        }

        if (detail is null)
        {
            return ServiceResult.Fail("Write what happened.");
        }

        if (detail.Length > 500)
        {
            return ServiceResult.Fail("What happened can have at most 500 characters.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await db.Users.AnyAsync(u => u.Id == actingUserId && u.IsActive && u.RoleId == AppRoles.GuidanceCounselorId, ct))
        {
            return ServiceResult.Fail("Only the guidance counselor can record a concern.");
        }

        var student = await db.Students.AsNoTracking().Where(s => s.Id == studentId && s.Status == StudentStatus.Active).Select(s => new { s.Id, s.LastName, s.FirstName }).FirstOrDefaultAsync(ct);
        if (student is null)
        {
            return ServiceResult.Fail("This student was not found.");
        }

        var referral = new Referral { StudentId = studentId, Reason = reason, Detail = detail, Status = ReferralStatus.Open, HandledByUserId = actingUserId };
        db.Referrals.Add(referral);
        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok($"{student.LastName}, {student.FirstName}: the {ReferralReasons.Display(reason).ToLowerInvariant()} concern was recorded.", referral.Id);
    }

    public async Task<ReferralRow?> GetAsync(int referralId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return (await RowsAsync(db, db.Referrals.AsNoTracking().Where(r => r.Id == referralId), ct)).FirstOrDefault();
    }

    /// <summary>The students who have a problem right now (failing, at risk, or missing class), with their referral if there is one.</summary>
    public async Task<IReadOnlyList<AtRiskRow>> GetAtRiskAsync(string? reason, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var findings = FindingsOf(await StudentMetrics.LoadAsync(db, now, null, null, ct)).Where(f => reason is null || f.Reason == reason).ToList();
        if (findings.Count == 0)
        {
            return [];
        }

        var studentIds = findings.Select(f => f.StudentId).Distinct().ToList();
        var students = await db.Students.AsNoTracking().Where(s => studentIds.Contains(s.Id)).Select(s => new { s.Id, s.FirstName, s.LastName, s.Lrn, s.SectionId }).ToDictionaryAsync(s => s.Id, ct);
        var sections = await SectionLabelsAsync(db, ct);
        var assignmentIds = findings.Select(f => f.AssignmentId).Distinct().ToList();
        var teachers = await db.TeachingAssignments.AsNoTracking().Where(a => assignmentIds.Contains(a.Id)).Select(a => new { a.Id, Name = a.Teacher.FirstName + " " + a.Teacher.LastName }).ToDictionaryAsync(a => a.Id, a => a.Name, ct);
        var referrals = await db.Referrals.AsNoTracking().Where(r => studentIds.Contains(r.StudentId) && r.AssignmentId != null)
            .OrderByDescending(r => r.Id).Select(r => new { r.Id, r.StudentId, r.AssignmentId, r.Reason, r.Status }).ToListAsync(ct);

        return findings
            .Select(f =>
            {
                var s = students[f.StudentId];
                var referral = referrals.FirstOrDefault(r => r.StudentId == f.StudentId && r.AssignmentId == f.AssignmentId && r.Reason == f.Reason);
                return new AtRiskRow(f.StudentId, $"{s.LastName}, {s.FirstName}", s.Lrn, s.SectionId is { } sec ? sections.GetValueOrDefault(sec) : null, f.AssignmentId, f.SubjectName,
                    teachers.GetValueOrDefault(f.AssignmentId, ""), f.Reason, f.Metric, f.Detail, referral?.Id, referral?.Status);
            })
            .OrderBy(r => r.Reason == ReferralReasons.FailingGrades ? 0 : r.Reason == ReferralReasons.AtRisk ? 1 : 2).ThenBy(r => r.Metric ?? 100).ThenBy(r => r.StudentName)
            .ToList();
    }

    // ======================================================================= the counselor works a referral

    public async Task<ServiceResult> UpdateAsync(int referralId, string status, string? intervention, string? notes, string? outcome, int actingUserId, CancellationToken ct = default)
    {
        intervention = string.IsNullOrWhiteSpace(intervention) ? null : intervention.Trim();
        notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        if (!ReferralStatus.All.Contains(status))
        {
            return ServiceResult.Fail("Choose a status.");
        }

        if (intervention is { Length: > 100 })
        {
            return ServiceResult.Fail("The help given can have at most 100 characters.");
        }

        if (notes is { Length: > 1000 })
        {
            return ServiceResult.Fail("The notes can have at most 1000 characters.");
        }

        if (status == ReferralStatus.Resolved && (outcome is null || !ReferralOutcomes.All.Contains(outcome)))
        {
            return ServiceResult.Fail("Say how it ended: improved or no change.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await db.Users.AnyAsync(u => u.Id == actingUserId && u.IsActive && u.RoleId == AppRoles.GuidanceCounselorId, ct))
        {
            return ServiceResult.Fail("Only the guidance counselor can work on referrals.");
        }

        var referral = await db.Referrals.Include(r => r.Student).Include(r => r.Assignment!.Subject).FirstOrDefaultAsync(r => r.Id == referralId, ct);
        if (referral is null)
        {
            return ServiceResult.Fail("This referral was not found.");
        }

        var wasOpen = referral.Status == ReferralStatus.Open;
        var changed = wasOpen || referral.Status != status || referral.Intervention != intervention || referral.Notes != notes
                      || (status == ReferralStatus.Resolved && referral.Outcome != outcome);
        referral.Status = status;
        referral.Intervention = intervention;
        referral.Notes = notes;
        referral.Outcome = status == ReferralStatus.Resolved ? outcome : null;
        referral.ResolvedAtUtc = status == ReferralStatus.Resolved ? (referral.ResolvedAtUtc ?? clock.GetUtcNow().UtcDateTime) : null;
        referral.HandledByUserId = actingUserId;
        await db.SaveChangesAsync(ct);

        // The student is told every time the counselor saves something new about them: a box in the middle of their screen (like an achievement) says it.
        if (changed)
        {
            var about = referral.Assignment is null ? "" : $"About {referral.Assignment.Subject.Name}. ";
            if (status == ReferralStatus.Resolved)
            {
                await notifications.AddAsync(referral.Student.UserId, "referral", "Your guidance counselor closed your case",
                    about + "They think things are better now. You can always go to them again.", null, ct);
            }
            else if (status == ReferralStatus.InProgress)
            {
                var help = string.IsNullOrWhiteSpace(intervention) ? "They will talk with you soon." : $"They plan this for you: {intervention.ToLowerInvariant()}.";
                await notifications.AddAsync(referral.Student.UserId, "referral", wasOpen ? "Your guidance counselor wants to help" : "News from your guidance counselor",
                    about + help, null, ct);
            }
        }

        return ServiceResult.Ok(status == ReferralStatus.Resolved ? "The referral is resolved." : "Saved.");
    }

    // ======================================================================= helpers

    private static async Task<Dictionary<int, string>> SectionLabelsAsync(AppDbContext db, CancellationToken ct) =>
        (await db.Sections.AsNoTracking().Select(s => new { s.Id, s.GradeLevel, Strand = s.Strand.Code, s.Name }).ToListAsync(ct))
            .ToDictionary(s => s.Id, s => TeachingService.SectionLabel(s.GradeLevel, s.Strand, s.Name));

    private static async Task<List<ReferralRow>> RowsAsync(AppDbContext db, IQueryable<Referral> query, CancellationToken ct)
    {
        var rows = await query.Select(r => new
        {
            r.Id, r.StudentId, r.Student.FirstName, r.Student.LastName, r.Student.Lrn, r.Student.SectionId, r.Student.GradeLevel, r.Student.StrandId, r.AssignmentId,
            Subject = r.Assignment != null ? r.Assignment.Subject.Name : null,
            Teacher = r.Assignment != null ? r.Assignment.Teacher.FirstName + " " + r.Assignment.Teacher.LastName : null,
            r.Reason, r.Detail, r.Metric, r.Status, r.Intervention, r.Notes, r.Outcome, r.CreatedAtUtc, r.ResolvedAtUtc,
        }).ToListAsync(ct);
        var sections = await SectionLabelsAsync(db, ct);
        var strands = await db.Strands.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Code, ct);
        return rows.Select(r => new ReferralRow(r.Id, r.StudentId, $"{r.LastName}, {r.FirstName}", r.Lrn, r.SectionId is { } s ? sections.GetValueOrDefault(s) : null, r.AssignmentId, r.Subject, r.Teacher,
            r.Reason, r.Detail, r.Metric, r.Status, r.Intervention, r.Notes, r.Outcome, r.CreatedAtUtc, r.ResolvedAtUtc, r.GradeLevel, r.StrandId is { } strandId ? strands.GetValueOrDefault(strandId) : null)).ToList();
    }
}
