using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Components.Shared;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

public sealed record GraduateCandidate(int StudentId, string Name, string Lrn, string? Placement);

public sealed record ArchivedStudent(int StudentId, string Name, string Lrn, string Status, string? Note, DateTime? ChangedAtUtc, string? Placement);

public sealed record StudentSummary(int StudentId, string Name, string Lrn, string Status, string? Placement);

/// <summary>
/// How a student's schooling ends. The students who graduate are archived: the records stay, the enrollments are closed (Completed), the
/// sign-in is switched off and open referrals are closed. It happens by itself when the last term of a school year ends
/// (<see cref="ArchiveFinishedAsync"/>), and an administrator can also archive students earlier. An archived student can be restored.
/// </summary>
public sealed class StudentLifecycleService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider clock, SecurityLogService? securityLog = null)
{
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

    /// <summary>
    /// The automatic archive: when the last term of a school year has ended (its end date is before today, Philippine date), the grade 12 students
    /// who were enrolled in it graduate. Nobody has to tick them. It is safe to run any number of times: a student is archived once.
    /// </summary>
    /// <returns>How many students were archived by this run.</returns>
    public async Task<int> ArchiveFinishedAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var today = PhilippineTime.Today(clock);

        // The last term of each school year (the one that ends last), when it is over.
        var terms = await db.Terms.AsNoTracking().Select(t => new { t.Id, t.SchoolYearId, t.Name, t.EndDate }).ToListAsync(ct);
        var finished = terms.GroupBy(t => t.SchoolYearId).Select(g => g.OrderByDescending(t => t.EndDate).First()).Where(t => t.EndDate < today).ToList();
        var archived = 0;
        foreach (var term in finished)
        {
            var termId = term.Id;
            var students = await db.Students.Include(s => s.User)
                .Where(s => s.Status == StudentStatus.Active && s.GradeLevel == 12
                            && db.Enrollments.Any(e => e.StudentId == s.Id && e.TermId == termId && e.Status != EnrollmentStatus.Dropped))
                .ToListAsync(ct);
            foreach (var student in students)
            {
                await ApplyAsync(db, student, $"Graduated: {term.Name} ended (automatic)", clock, ct);
            }

            if (students.Count > 0)
            {
                await db.SaveChangesAsync(ct);
                archived += students.Count;
                if (securityLog is not null)
                {
                    await securityLog.AddAsync(SecurityEvents.StudentsArchived, details: $"{students.Count} grade 12 {(students.Count == 1 ? "student was" : "students were")} archived as graduated by themselves: {term.Name} ended.", actorName: "The system", ct: ct);
                }
            }
        }

        return archived;
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
            await ApplyAsync(db, student, note ?? "Graduated", clock, ct);
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

    // ======================================================================= what archiving does

    /// <summary>Closes the student's schooling as a graduate. The caller saves.</summary>
    private static async Task ApplyAsync(AppDbContext db, Student student, string? note, TimeProvider clock, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        student.Status = StudentStatus.Graduated;
        student.StatusChangedAtUtc = now;
        student.StatusNote = note is { Length: > 300 } ? note[..300] : note;
        student.User.IsActive = false;
        student.User.SecurityStamp = Guid.NewGuid().ToString("N");

        await db.Enrollments.Where(e => e.StudentId == student.Id && e.Status == EnrollmentStatus.Enrolled)
            .ExecuteUpdateAsync(set => set.SetProperty(e => e.Status, EnrollmentStatus.Completed), ct);

        // Open referrals end with the schooling.
        await db.Referrals.Where(r => r.StudentId == student.Id && r.Status != ReferralStatus.Resolved)
            .ExecuteUpdateAsync(set => set.SetProperty(r => r.Status, ReferralStatus.Resolved).SetProperty(r => r.ResolvedAtUtc, now), ct);
    }

    private static async Task<Dictionary<int, string>> SectionLabelsAsync(AppDbContext db, CancellationToken ct) =>
        (await db.Sections.AsNoTracking().Select(s => new { s.Id, s.GradeLevel, Strand = s.Strand.Code, s.Name }).ToListAsync(ct))
            .ToDictionary(s => s.Id, s => TeachingService.SectionLabel(s.GradeLevel, s.Strand, s.Name));
}
