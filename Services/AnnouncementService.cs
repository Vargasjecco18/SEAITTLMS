using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Components.Shared;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>An announcement as it is shown: who wrote it, for whom, and until when.</summary>
public sealed record AnnouncementView(
    int Id, int AuthorUserId, string Title, string Body, string AuthorName, string AuthorRole, string Audience, int? SectionId, string? SectionLabel,
    DateTime CreatedAtUtc, DateOnly? ExpiresOn);

public sealed class AnnouncementInput
{
    public int? Id { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string Audience { get; set; } = AnnouncementAudiences.Everyone;

    /// <summary>A teacher picks the section the announcement is for. Others leave it empty.</summary>
    public int SectionId { get; set; }

    public DateOnly? ExpiresOn { get; set; }
}

public sealed record AnnouncementSectionChoice(int Id, string Label);

/// <summary>
/// Announcements. The administrator and the registrar write them for everybody (students and teachers), for the students or for the
/// teachers. A teacher writes them for the students of one section they teach. A student sees what is meant for them on the
/// dashboard; the people it is for are also told with a notification when it is posted.
/// </summary>
public sealed class AnnouncementService(IDbContextFactory<AppDbContext> dbFactory, NotificationService notifications, TimeProvider clock)
{
    public const int MaxTitleLength = 200;
    public const int MaxBodyLength = 2000;

    // ======================================================================= reading

    /// <summary>What a student is meant to read: for everybody or the students, for the whole school or for one of their sections. Newest first.</summary>
    public async Task<IReadOnlyList<AnnouncementView>> GetForStudentAsync(int studentId, int take = 5, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var today = PhilippineTime.Today(clock);

        var sectionIds = await db.Enrollments.AsNoTracking()
            .Where(e => e.StudentId == studentId && e.Status == EnrollmentStatus.Enrolled)
            .Select(e => e.SectionId).Distinct().ToListAsync(ct);

        var query = db.Announcements.AsNoTracking()
            .Where(a => (a.Audience == AnnouncementAudiences.Everyone || a.Audience == AnnouncementAudiences.Students)
                        && (a.SectionId == null || sectionIds.Contains(a.SectionId.Value))
                        && (a.ExpiresOn == null || a.ExpiresOn >= today));
        return await ViewsAsync(db, query, take, ct);
    }

    /// <summary>What a teacher is meant to read: for everybody or the teachers.</summary>
    public async Task<IReadOnlyList<AnnouncementView>> GetForTeachersAsync(int take = 5, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var today = PhilippineTime.Today(clock);
        var query = db.Announcements.AsNoTracking()
            .Where(a => (a.Audience == AnnouncementAudiences.Everyone || a.Audience == AnnouncementAudiences.Teachers)
                        && a.SectionId == null && (a.ExpiresOn == null || a.ExpiresOn >= today));
        return await ViewsAsync(db, query, take, ct);
    }

    /// <summary>The announcements a person manages: the administrator and the registrar see all of them, a teacher their own.</summary>
    public async Task<IReadOnlyList<AnnouncementView>> ListManagedAsync(CurrentUserInfo user, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var query = db.Announcements.AsNoTracking().AsQueryable();
        if (user.Role == AppRoles.Teacher)
        {
            query = query.Where(a => a.AuthorUserId == user.UserId);
        }

        return await ViewsAsync(db, query, 100, ct);
    }

    /// <summary>The sections a teacher can write to: the ones they teach a subject to.</summary>
    public async Task<IReadOnlyList<AnnouncementSectionChoice>> GetSectionChoicesAsync(int teacherUserId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await db.TeachingAssignments.AsNoTracking()
            .Where(a => a.Teacher.UserId == teacherUserId)
            .Select(a => new { a.SectionId, a.Section.GradeLevel, Strand = a.Section.Strand.Code, a.Section.Name })
            .Distinct().ToListAsync(ct);
        return rows.OrderBy(r => r.GradeLevel).ThenBy(r => r.Strand).ThenBy(r => r.Name)
            .Select(r => new AnnouncementSectionChoice(r.SectionId, TeachingService.SectionLabel(r.GradeLevel, r.Strand, r.Name))).ToList();
    }

    // ======================================================================= writing

    public async Task<ServiceResult> SaveAsync(CurrentUserInfo user, AnnouncementInput input, CancellationToken ct = default)
    {
        var errors = new List<string>();
        var title = (input.Title ?? "").Trim();
        var body = (input.Body ?? "").Trim();
        if (title.Length is 0 or > MaxTitleLength)
        {
            errors.Add($"Write a title (up to {MaxTitleLength} characters).");
        }

        if (body.Length is 0 or > MaxBodyLength)
        {
            errors.Add($"Write the announcement (up to {MaxBodyLength} characters).");
        }

        var isTeacher = user.Role == AppRoles.Teacher;
        if (!isTeacher && user.Role is not (AppRoles.Admin or AppRoles.Registrar))
        {
            return ServiceResult.Fail("Only the administrator, the registrar and teachers write announcements.");
        }

        var audience = isTeacher ? AnnouncementAudiences.Students : input.Audience;
        if (!AnnouncementAudiences.All.Contains(audience))
        {
            errors.Add("Pick who the announcement is for.");
        }

        if (input.ExpiresOn is { } expires && expires < PhilippineTime.Today(clock))
        {
            errors.Add("The last day to show it can't be in the past.");
        }

        if (errors.Count > 0)
        {
            return ServiceResult.Fail(errors);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await db.Users.AsNoTracking().AnyAsync(u => u.Id == user.UserId && u.IsActive, ct))
        {
            return ServiceResult.Fail("Please sign in again.");
        }

        // A teacher writes to one of the sections they teach.
        int? sectionId = null;
        if (isTeacher)
        {
            if (!await db.TeachingAssignments.AnyAsync(a => a.Teacher.UserId == user.UserId && a.SectionId == input.SectionId, ct))
            {
                return ServiceResult.Fail("Pick one of the sections you teach.");
            }

            sectionId = input.SectionId;
        }

        Announcement announcement;
        var isNew = input.Id is null;
        if (input.Id is { } id)
        {
            var isAdmin = await IsAdminAsync(db, user, ct);
            var found = await db.Announcements.FirstOrDefaultAsync(a => a.Id == id && (isAdmin || a.AuthorUserId == user.UserId), ct);
            if (found is null)
            {
                return ServiceResult.Fail("You can only change your own announcements.");
            }

            announcement = found;
        }
        else
        {
            announcement = new Announcement { AuthorUserId = user.UserId };
            db.Announcements.Add(announcement);
        }

        announcement.Title = title;
        announcement.Body = body;
        announcement.Audience = audience;
        announcement.SectionId = isNew || isTeacher ? sectionId : announcement.SectionId;
        announcement.ExpiresOn = input.ExpiresOn;
        await db.SaveChangesAsync(ct);

        if (isNew)
        {
            await NotifyAsync(db, announcement, ct);
        }

        return ServiceResult.Ok(isNew ? "The announcement was posted." : "The announcement was saved.", announcement.Id);
    }

    public async Task<ServiceResult> DeleteAsync(CurrentUserInfo user, int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var isAdmin = await IsAdminAsync(db, user, ct);
        var deleted = await db.Announcements.Where(a => a.Id == id && (isAdmin || a.AuthorUserId == user.UserId)).ExecuteDeleteAsync(ct);
        return deleted == 0 ? ServiceResult.Fail("This announcement was already deleted, or it is not yours.") : ServiceResult.Ok("The announcement was deleted.");
    }

    // ======================================================================= helpers

    /// <summary>The administrator may change and delete every announcement; everybody else only their own.</summary>
    private static async Task<bool> IsAdminAsync(AppDbContext db, CurrentUserInfo user, CancellationToken ct) =>
        user.Role == AppRoles.Admin && await db.Users.AsNoTracking().AnyAsync(u => u.Id == user.UserId && u.IsActive && u.RoleId == AppRoles.AdminId, ct);

    /// <summary>Tells the people it is for. (The author is not told about their own announcement.)</summary>
    private async Task NotifyAsync(AppDbContext db, Announcement announcement, CancellationToken ct)
    {
        var students = Array.Empty<int>();
        var teachers = Array.Empty<int>();

        var forStudents = announcement.Audience is AnnouncementAudiences.Everyone or AnnouncementAudiences.Students;
        var forTeachers = announcement.Audience is AnnouncementAudiences.Everyone or AnnouncementAudiences.Teachers;

        if (forStudents)
        {
            var query = db.Students.AsNoTracking().Where(s => s.Status == StudentStatus.Active && s.User.IsActive);
            if (announcement.SectionId is { } sectionId)
            {
                query = query.Where(s => db.Enrollments.Any(e => e.StudentId == s.Id && e.SectionId == sectionId && e.Status == EnrollmentStatus.Enrolled));
            }

            students = (await query.Select(s => s.UserId).ToListAsync(ct)).Where(id => id != announcement.AuthorUserId).ToArray();
        }

        if (forTeachers && announcement.SectionId is null)
        {
            teachers = (await db.Teachers.AsNoTracking().Where(t => t.User.IsActive).Select(t => t.UserId).ToListAsync(ct)).Where(id => id != announcement.AuthorUserId).ToArray();
        }

        var snippet = announcement.Body.Length > 140 ? announcement.Body[..139] + "…" : announcement.Body;
        await notifications.AddForManyAsync(students, "announcement", announcement.Title, snippet, "student", ct);
        await notifications.AddForManyAsync(teachers, "announcement", announcement.Title, snippet, "announcements", ct);
    }

    private static async Task<IReadOnlyList<AnnouncementView>> ViewsAsync(AppDbContext db, IQueryable<Announcement> query, int take, CancellationToken ct)
    {
        var rows = await query
            .OrderByDescending(a => a.CreatedAtUtc).ThenByDescending(a => a.Id)
            .Take(Math.Clamp(take, 1, 200))
            .Include(a => a.Author).ThenInclude(u => u.Role)
            .Include(a => a.Author).ThenInclude(u => u.Administrator)
            .Include(a => a.Author).ThenInclude(u => u.Registrar)
            .Include(a => a.Author).ThenInclude(u => u.Teacher)
            .Include(a => a.Section).ThenInclude(s => s!.Strand)
            .ToListAsync(ct);

        return rows.Select(a => new AnnouncementView(
            a.Id, a.AuthorUserId, a.Title, a.Body, a.Author.DisplayName, AppRoles.DisplayName(a.Author.Role.Name), a.Audience, a.SectionId,
            a.Section is null ? null : TeachingService.SectionLabel(a.Section.GradeLevel, a.Section.Strand.Code, a.Section.Name),
            a.CreatedAtUtc, a.ExpiresOn)).ToList();
    }
}
