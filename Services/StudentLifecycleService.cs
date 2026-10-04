using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

public sealed record GraduateCandidate(int StudentId, string Name, string Lrn, string? Placement);

public sealed record ArchivedStudent(int StudentId, string Name, string Lrn, string Status, string? Note, DateTime? ChangedAtUtc, string? Placement);

public sealed record StudentSummary(int StudentId, string Name, string Lrn, string Status, string? Placement);

public sealed record WithdrawalRow(int Id, int StudentId, string StudentName, string Lrn, string? Placement, string Reason, string Status, DateTime RequestedAtUtc, DateTime? DecidedAtUtc, string? DecisionNote);

/// <summary>
/// How a student's schooling ends. An administrator archives the students who graduate; a student can ask to withdraw and the registrar
/// approves or declines it (or withdraws a student directly). Either way the student is archived: the records stay, the enrollments are
/// closed (Completed for a graduate, Dropped for a withdrawal), the sign-in is switched off and open referrals are closed.
/// </summary>
public sealed class StudentLifecycleService(IDbContextFactory<AppDbContext> dbFactory, NotificationService notifications, TimeProvider clock)
{
    public const int MinReasonLength = 10;

    // ======================================================================= the administrator: graduation

    /// <summary>Students still at school who are in grade 12: the ones who will graduate.</summary>
    public async Task<IReadOnlyList<GraduateCandidate>> GetGraduatingAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var sections = await SectionLabelsAsync(db, ct);
        var rows = await db.Students.AsNoTracking().Where(s => s.Status == StudentStatus.Active && s.GradeLevel == 12)
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .Select(s => new { s.Id, s.FirstName, s.LastName, s.Lrn, s.SectionId }).ToListAsync(ct);
        return rows.Select(s => new GraduateCandidate(s.Id, $"{s.LastName}, {s.FirstName}", s.Lrn, s.SectionId is { } sec ? sections.GetValueOrDefault(sec) : null)).ToList();
    }

    public async Task<ServiceResult> ArchiveAsync(IReadOnlyCollection<int> studentIds, string? note, int actingUserId, CancellationToken ct = default)
    {
        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (studentIds.Count == 0)
        {
            return ServiceResult.Fail("Pick at least one student.");
        }

        if (note is { Length: > 300 })
        {
            return ServiceResult.Fail("The note can have at most 300 characters.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await db.Users.AnyAsync(u => u.Id == actingUserId && u.IsActive && u.RoleId == AppRoles.AdminId, ct))
        {
            return ServiceResult.Fail("Only an administrator can archive students.");
        }

        var students = await db.Students.Include(s => s.User).Where(s => studentIds.Contains(s.Id) && s.Status == StudentStatus.Active).ToListAsync(ct);
        if (students.Count == 0)
        {
            return ServiceResult.Fail("These students are already archived.");
        }

        foreach (var student in students)
        {
            await ApplyAsync(db, student, StudentStatus.Graduated, note ?? "Graduated", ct);
        }

        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok($"{students.Count} {(students.Count == 1 ? "student was" : "students were")} archived as graduated.");
    }

    /// <summary>Takes a student out of the archive: the account works again. The student has to be enrolled again to be in a class.</summary>
    public async Task<ServiceResult> RestoreAsync(int studentId, int actingUserId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await db.Users.AnyAsync(u => u.Id == actingUserId && u.IsActive && u.RoleId == AppRoles.AdminId, ct))
        {
            return ServiceResult.Fail("Only an administrator can restore students.");
        }

        var student = await db.Students.Include(s => s.User).FirstOrDefaultAsync(s => s.Id == studentId && s.Status != StudentStatus.Active, ct);
        if (student is null)
        {
            return ServiceResult.Fail("This student is not in the archive.");
        }

        student.Status = StudentStatus.Active;
        student.StatusChangedAtUtc = clock.GetUtcNow().UtcDateTime;
        student.StatusNote = null;
        student.User.IsActive = true;
        student.User.SecurityStamp = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok("The student was restored. The account can sign in again; the registrar can enroll the student for the next term.");
    }

    public async Task<IReadOnlyList<ArchivedStudent>> ListArchivedAsync(string? search, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var query = db.Students.AsNoTracking().Where(s => s.Status != StudentStatus.Active);
        foreach (var token in (search ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(4))
        {
            var t = token;
            query = query.Where(s => s.FirstName.Contains(t) || s.LastName.Contains(t) || s.Lrn.Contains(t));
        }

        var sections = await SectionLabelsAsync(db, ct);
        var rows = await query.OrderByDescending(s => s.StatusChangedAtUtc).ThenBy(s => s.LastName).Take(200)
            .Select(s => new { s.Id, s.FirstName, s.LastName, s.Lrn, s.Status, s.StatusNote, s.StatusChangedAtUtc, s.SectionId }).ToListAsync(ct);
        return rows.Select(s => new ArchivedStudent(s.Id, $"{s.LastName}, {s.FirstName}", s.Lrn, s.Status, s.StatusNote, s.StatusChangedAtUtc, s.SectionId is { } sec ? sections.GetValueOrDefault(sec) : null)).ToList();
    }

    /// <summary>Finds a student by LRN (12 digits), whatever their status.</summary>
    public async Task<StudentSummary?> FindStudentAsync(string? lrn, CancellationToken ct = default)
    {
        lrn = lrn?.Trim();
        if (lrn is not { Length: 12 } || !lrn.All(char.IsAsciiDigit))
        {
            return null;
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var sections = await SectionLabelsAsync(db, ct);
        var s = await db.Students.AsNoTracking().Where(x => x.Lrn == lrn).Select(x => new { x.Id, x.FirstName, x.LastName, x.Lrn, x.Status, x.SectionId }).FirstOrDefaultAsync(ct);
        return s is null ? null : new StudentSummary(s.Id, $"{s.LastName}, {s.FirstName}", s.Lrn, s.Status, s.SectionId is { } sec ? sections.GetValueOrDefault(sec) : null);
    }

    // ======================================================================= withdrawal: the student asks

    public async Task<ServiceResult> RequestWithdrawalAsync(int studentId, string? reason, CancellationToken ct = default)
    {
        reason = reason?.Trim();
        if (string.IsNullOrEmpty(reason) || reason.Length < MinReasonLength)
        {
            return ServiceResult.Fail($"Tell the registrar why, in at least {MinReasonLength} characters.");
        }

        if (reason.Length > 500)
        {
            return ServiceResult.Fail("The reason can have at most 500 characters.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var student = await db.Students.AsNoTracking().Where(s => s.Id == studentId).Select(s => new { s.Id, s.Status, s.FirstName, s.LastName }).FirstOrDefaultAsync(ct);
        if (student is null || student.Status != StudentStatus.Active)
        {
            return ServiceResult.Fail("Only a student who is at school can ask to withdraw.");
        }

        if (await db.WithdrawalRequests.AnyAsync(w => w.StudentId == studentId && w.Status == WithdrawalStatus.Pending, ct))
        {
            return ServiceResult.Fail("You already asked. The registrar has not answered yet.");
        }

        db.WithdrawalRequests.Add(new WithdrawalRequest { StudentId = studentId, Reason = reason });
        await db.SaveChangesAsync(ct);

        var staff = await db.Users.AsNoTracking().Where(u => u.IsActive && (u.RoleId == AppRoles.RegistrarId || u.RoleId == AppRoles.GuidanceCounselorId)).Select(u => u.Id).ToListAsync(ct);
        await notifications.AddForManyAsync(staff, "withdrawal", $"{student.LastName}, {student.FirstName} asked to withdraw", reason.Length > 150 ? reason[..149] + "…" : reason, "registrar/withdrawals", ct);
        return ServiceResult.Ok("Your request was sent to the registrar. They will answer it.");
    }

    public async Task<IReadOnlyList<WithdrawalRow>> GetStudentRequestsAsync(int studentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await RowsAsync(db, db.WithdrawalRequests.AsNoTracking().Where(w => w.StudentId == studentId).OrderByDescending(w => w.Id), ct);
    }

    public async Task<ServiceResult> CancelRequestAsync(int studentId, int requestId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var changed = await db.WithdrawalRequests.Where(w => w.Id == requestId && w.StudentId == studentId && w.Status == WithdrawalStatus.Pending)
            .ExecuteUpdateAsync(set => set.SetProperty(w => w.Status, WithdrawalStatus.Cancelled).SetProperty(w => w.DecidedAtUtc, now), ct);
        return changed == 0 ? ServiceResult.Fail("This request can't be cancelled.") : ServiceResult.Ok("Your request was cancelled.");
    }

    // ======================================================================= withdrawal: the registrar decides

    public async Task<IReadOnlyList<WithdrawalRow>> ListRequestsAsync(bool pendingOnly, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var query = db.WithdrawalRequests.AsNoTracking().Where(w => w.Status != WithdrawalStatus.Cancelled);
        if (pendingOnly)
        {
            query = query.Where(w => w.Status == WithdrawalStatus.Pending);
        }

        return await RowsAsync(db, query.OrderBy(w => w.Status != WithdrawalStatus.Pending).ThenByDescending(w => w.Id).Take(100), ct);
    }

    public async Task<int> CountPendingAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.WithdrawalRequests.CountAsync(w => w.Status == WithdrawalStatus.Pending, ct);
    }

    public async Task<ServiceResult> DecideAsync(int requestId, bool approve, string? note, int actingUserId, CancellationToken ct = default)
    {
        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (!approve && note is null)
        {
            return ServiceResult.Fail("Say why the request is declined.");
        }

        if (note is { Length: > 500 })
        {
            return ServiceResult.Fail("The note can have at most 500 characters.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsRegistrarAsync(db, actingUserId, ct))
        {
            return ServiceResult.Fail("Only the registrar can answer withdrawal requests.");
        }

        var request = await db.WithdrawalRequests.Include(w => w.Student).ThenInclude(s => s.User).FirstOrDefaultAsync(w => w.Id == requestId && w.Status == WithdrawalStatus.Pending, ct);
        if (request is null)
        {
            return ServiceResult.Fail("This request was already answered.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        request.DecidedAtUtc = now;
        request.DecidedByUserId = actingUserId;
        request.DecisionNote = note;

        if (!approve)
        {
            request.Status = WithdrawalStatus.Declined;
            await db.SaveChangesAsync(ct);
            await notifications.AddAsync(request.Student.UserId, "withdrawal", "Your withdrawal request was declined", note, null, ct);
            return ServiceResult.Ok("The request was declined. The student was told.");
        }

        request.Status = WithdrawalStatus.Approved;
        await ApplyAsync(db, request.Student, StudentStatus.Withdrawn, request.Reason, ct);
        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok($"{request.Student.LastName}, {request.Student.FirstName} was withdrawn and archived.");
    }

    /// <summary>The registrar withdraws a student directly (the student told the registrar in person, for example).</summary>
    public async Task<ServiceResult> WithdrawAsync(int studentId, string? reason, int actingUserId, CancellationToken ct = default)
    {
        reason = reason?.Trim();
        if (string.IsNullOrEmpty(reason) || reason.Length < MinReasonLength)
        {
            return ServiceResult.Fail($"Write the reason, in at least {MinReasonLength} characters.");
        }

        if (reason.Length > 300)
        {
            return ServiceResult.Fail("The reason can have at most 300 characters here.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsRegistrarAsync(db, actingUserId, ct))
        {
            return ServiceResult.Fail("Only the registrar can withdraw a student.");
        }

        var student = await db.Students.Include(s => s.User).FirstOrDefaultAsync(s => s.Id == studentId && s.Status == StudentStatus.Active, ct);
        if (student is null)
        {
            return ServiceResult.Fail("This student is not at school, or was not found.");
        }

        await ApplyAsync(db, student, StudentStatus.Withdrawn, reason, ct);
        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok($"{student.LastName}, {student.FirstName} was withdrawn and archived.");
    }

    // ======================================================================= what archiving does

    /// <summary>Closes the student's schooling. The caller saves.</summary>
    private async Task ApplyAsync(AppDbContext db, Student student, string status, string? note, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        student.Status = status;
        student.StatusChangedAtUtc = now;
        student.StatusNote = note is { Length: > 300 } ? note[..300] : note;
        student.User.IsActive = false;
        student.User.SecurityStamp = Guid.NewGuid().ToString("N");

        var enrollmentStatus = status == StudentStatus.Graduated ? EnrollmentStatus.Completed : EnrollmentStatus.Dropped;
        await db.Enrollments.Where(e => e.StudentId == student.Id && e.Status == EnrollmentStatus.Enrolled).ExecuteUpdateAsync(set => set.SetProperty(e => e.Status, enrollmentStatus), ct);

        // Open referrals end with the schooling.
        await db.Referrals.Where(r => r.StudentId == student.Id && r.Status != ReferralStatus.Resolved)
            .ExecuteUpdateAsync(set => set.SetProperty(r => r.Status, ReferralStatus.Resolved).SetProperty(r => r.ResolvedAtUtc, now), ct);

        // Other requests of the student are answered by what happened.
        await db.WithdrawalRequests.Where(w => w.StudentId == student.Id && w.Status == WithdrawalStatus.Pending)
            .ExecuteUpdateAsync(set => set.SetProperty(w => w.Status, status == StudentStatus.Withdrawn ? WithdrawalStatus.Approved : WithdrawalStatus.Cancelled).SetProperty(w => w.DecidedAtUtc, now), ct);

        if (status == StudentStatus.Withdrawn)
        {
            var counselors = await db.Users.AsNoTracking().Where(u => u.IsActive && u.RoleId == AppRoles.GuidanceCounselorId).Select(u => u.Id).ToListAsync(ct);
            await notifications.AddForManyAsync(counselors, "withdrawal", $"{student.LastName}, {student.FirstName} left school", student.StatusNote, null, ct);
        }
    }

    private static Task<bool> IsRegistrarAsync(AppDbContext db, int userId, CancellationToken ct) =>
        db.Users.AnyAsync(u => u.Id == userId && u.IsActive && u.RoleId == AppRoles.RegistrarId, ct);

    private static async Task<Dictionary<int, string>> SectionLabelsAsync(AppDbContext db, CancellationToken ct) =>
        (await db.Sections.AsNoTracking().Select(s => new { s.Id, s.GradeLevel, Strand = s.Strand.Code, s.Name }).ToListAsync(ct))
            .ToDictionary(s => s.Id, s => TeachingService.SectionLabel(s.GradeLevel, s.Strand, s.Name));

    private static async Task<List<WithdrawalRow>> RowsAsync(AppDbContext db, IQueryable<WithdrawalRequest> query, CancellationToken ct)
    {
        var sections = await SectionLabelsAsync(db, ct);
        var rows = await query.Select(w => new
        {
            w.Id, w.StudentId, w.Student.FirstName, w.Student.LastName, w.Student.Lrn, w.Student.SectionId, w.Reason, w.Status, w.CreatedAtUtc, w.DecidedAtUtc, w.DecisionNote,
        }).ToListAsync(ct);
        return rows.Select(w => new WithdrawalRow(w.Id, w.StudentId, $"{w.LastName}, {w.FirstName}", w.Lrn, w.SectionId is { } s ? sections.GetValueOrDefault(s) : null, w.Reason, w.Status, w.CreatedAtUtc, w.DecidedAtUtc, w.DecisionNote)).ToList();
    }
}
