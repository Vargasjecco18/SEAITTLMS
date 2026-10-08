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

    public DateOnly? ExpiresOn { get; set; }
}

/// <summary>
/// Announcements. Only the academic coordinator writes them: for everybody (students and teachers), for the students or for the teachers.
/// A student sees what is meant for them on the dashboard; the people it is for are also told with a notification when it is posted.
/// (Announcements that a registrar or a teacher wrote before this rule stay in the database and are still shown to their students.)
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

    /// <summary>Every announcement, newest first, for the academic coordinator who manages them.</summary>
    public async Task<IReadOnlyList<AnnouncementView>> ListManagedAsync(CurrentUserInfo user, CancellationToken ct = default)
    {
        if (user.Role != AppRoles.AcademicCoordinator)
        {
            return [];
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await ViewsAsync(db, db.Announcements.AsNoTracking(), 100, ct);
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

        if (user.Role != AppRoles.AcademicCoordinator)
        {
            return ServiceResult.Fail("Only the academic coordinator writes announcements.");
        }

        var audience = input.Audience;
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
        if (!await IsCoordinatorAsync(db, user, ct))
        {
            return ServiceResult.Fail("Please sign in again.");
        }

        Announcement announcement;
        var isNew = input.Id is null;
        if (input.Id is { } id)
        {
            var found = await db.Announcements.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (found is null)
            {
                return ServiceResult.Fail("This announcement was deleted.");
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
        if (!await IsCoordinatorAsync(db, user, ct))
        {
            return ServiceResult.Fail("Only the academic coordinator deletes announcements.");
        }

        var deleted = await db.Announcements.Where(a => a.Id == id).ExecuteDeleteAsync(ct);
        return deleted == 0 ? ServiceResult.Fail("This announcement was already deleted.") : ServiceResult.Ok("The announcement was deleted.");
    }

    // ======================================================================= helpers

    /// <summary>Only the academic coordinator (still active) writes, changes and deletes announcements.</summary>
    private static async Task<bool> IsCoordinatorAsync(AppDbContext db, CurrentUserInfo user, CancellationToken ct) =>
        user.Role == AppRoles.AcademicCoordinator && await db.Users.AsNoTracking().AnyAsync(u => u.Id == user.UserId && u.IsActive && u.RoleId == AppRoles.AcademicCoordinatorId, ct);

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
        await notifications.AddForManyAsync(teachers, "announcement", announcement.Title, snippet, "teacher", ct);
    }

    private static async Task<IReadOnlyList<AnnouncementView>> ViewsAsync(AppDbContext db, IQueryable<Announcement> query, int take, CancellationToken ct)
    {
        var rows = await query
            .OrderByDescending(a => a.CreatedAtUtc).ThenByDescending(a => a.Id)
            .Take(Math.Clamp(take, 1, 200))
            .Include(a => a.Author).ThenInclude(u => u.Role)
            .Include(a => a.Author).ThenInclude(u => u.Administrator)
            .Include(a => a.Author).ThenInclude(u => u.Registrar)
            .Include(a => a.Author).ThenInclude(u => u.AcademicCoordinator)
            .Include(a => a.Author).ThenInclude(u => u.Teacher)
            .Include(a => a.Section).ThenInclude(s => s!.Strand)
            .ToListAsync(ct);

        return rows.Select(a => new AnnouncementView(
            a.Id, a.AuthorUserId, a.Title, a.Body, a.Author.DisplayName, AppRoles.DisplayName(a.Author.Role.Name), a.Audience, a.SectionId,
            a.Section is null ? null : TeachingService.SectionLabel(a.Section.GradeLevel, a.Section.Strand.Code, a.Section.Name),
            a.CreatedAtUtc, a.ExpiresOn)).ToList();
    }
}
