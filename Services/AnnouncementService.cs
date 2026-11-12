using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Components.Shared;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>An announcement as it is shown: who wrote it, for whom, and from when (<paramref name="StartsOn"/>) until when (<paramref name="ExpiresOn"/>, its End).</summary>
public sealed record AnnouncementView(
    int Id, int AuthorUserId, string Title, string Body, string AuthorName, string AuthorRole, string Audience, int? SectionId, string? SectionLabel,
    DateTime CreatedAtUtc, DateOnly? ExpiresOn, string Category = AnnouncementCategories.Other, int? AssignmentId = null, string? CourseName = null, DateOnly? StartsOn = null);

public sealed class AnnouncementInput
{
    public int? Id { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string Audience { get; set; } = AnnouncementAudiences.Everyone;

    /// <summary>One of <see cref="AnnouncementCategories"/>.</summary>
    public string Category { get; set; } = AnnouncementCategories.Other;

    /// <summary>A teacher's announcement: the course (teaching assignment) it is for. The coordinator's notices have none.</summary>
    public int? AssignmentId { get; set; }

    /// <summary>Start of the schedule: the first day it is about. Empty: it is only about the End day. It is shown from the day it is posted.</summary>
    public DateOnly? StartsOn { get; set; }

    /// <summary>End of the schedule: the last day it is about and shown. Empty: until it is deleted.</summary>
    public DateOnly? ExpiresOn { get; set; }
}

/// <summary>
/// Announcements. The academic coordinator writes the notices of the school: an exam, a holiday, the semester break, a teachers' meeting or
/// anything else, for everybody (students and teachers), for the students or for the teachers. A teacher writes them for their own class only
/// (one of their courses: the students of that subject and section). A person reads what is meant for them on the dashboard and in the
/// announcements button of the header; the people it is for are also told with a notification when it is posted.
/// </summary>
public sealed class AnnouncementService(IDbContextFactory<AppDbContext> dbFactory, NotificationService notifications, TimeProvider clock)
{
    public const int MaxTitleLength = 200;
    public const int MaxBodyLength = 2000;

    // ======================================================================= reading

    /// <summary>What a student is meant to read: for everybody or the students, for the whole school or for one of their sections, until its End day. Newest first.</summary>
    public async Task<IReadOnlyList<AnnouncementView>> GetForStudentAsync(int studentId, int take = 5, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var today = PhilippineTime.Today(clock);
        return await ViewsAsync(db, Live(await ForStudentAsync(db, studentId, ct), today), take, ct);
    }

    /// <summary>The notices of the school for the teachers (for everybody or for the teachers), newest first.</summary>
    public async Task<IReadOnlyList<AnnouncementView>> GetForTeachersAsync(int take = 20, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var today = PhilippineTime.Today(clock);
        return await ViewsAsync(db, Live(ForTeachers(db), today), take, ct);
    }

    /// <summary>
    /// What the announcements button of the header shows: a student what is meant for them; a teacher the notices of the school for the teachers
    /// and their own announcements to their classes; the coordinator the notices they wrote; the other staff the notices for everybody.
    /// </summary>
    public async Task<IReadOnlyList<AnnouncementView>> GetForUserAsync(CurrentUserInfo user, int take = 12, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var today = PhilippineTime.Today(clock);

        if (user.Role == AppRoles.AcademicCoordinator)
        {
            // the coordinator sees every notice they manage, the ones that are still to start and the ones that ended too
            return await ViewsAsync(db, db.Announcements.AsNoTracking().Where(a => a.AssignmentId == null), take, ct);
        }

        return await ViewsAsync(db, Live(await MeantForAsync(db, user, ct), today), take, ct);
    }

    /// <summary>The announcements meant for the person whatever their dates: what the header, the dashboards and the calendar choose from.</summary>
    public static async Task<IQueryable<Announcement>> MeantForAsync(AppDbContext db, CurrentUserInfo user, CancellationToken ct = default)
    {
        switch (user.Role)
        {
            case AppRoles.Student:
                var studentId = await db.Students.AsNoTracking().Where(s => s.UserId == user.UserId).Select(s => (int?)s.Id).FirstOrDefaultAsync(ct);
                return studentId is null ? db.Announcements.AsNoTracking().Where(_ => false) : await ForStudentAsync(db, studentId.Value, ct);

            case AppRoles.Teacher:
                return db.Announcements.AsNoTracking()
                    .Where(a => a.AuthorUserId == user.UserId
                                || ((a.Audience == AnnouncementAudiences.Everyone || a.Audience == AnnouncementAudiences.Teachers || a.Audience == AnnouncementAudiences.StaffTeachers) && a.SectionId == null));

            case AppRoles.AcademicCoordinator:
                return db.Announcements.AsNoTracking().Where(a => a.AssignmentId == null);

            default:
                // the staff (and the administrator): what is for everybody, for the staff, or for the staff and the teachers
                return db.Announcements.AsNoTracking().Where(a => (a.Audience == AnnouncementAudiences.Everyone || a.Audience == AnnouncementAudiences.Staff || a.Audience == AnnouncementAudiences.StaffTeachers) && a.SectionId == null);
        }
    }

    private static async Task<IQueryable<Announcement>> ForStudentAsync(AppDbContext db, int studentId, CancellationToken ct)
    {
        var sectionIds = await db.Enrollments.AsNoTracking()
            .Where(e => e.StudentId == studentId && e.Status == EnrollmentStatus.Enrolled)
            .Select(e => e.SectionId).Distinct().ToListAsync(ct);

        var courseIds = await Roster.AssignmentsOf(db, studentId).Select(a => a.Id).ToListAsync(ct);

        return db.Announcements.AsNoTracking()
            .Where(a => (a.Audience == AnnouncementAudiences.Everyone || a.Audience == AnnouncementAudiences.Students)
                        && (a.SectionId == null || sectionIds.Contains(a.SectionId.Value))
                        && (a.AssignmentId == null || courseIds.Contains(a.AssignmentId.Value)));
    }

    private static IQueryable<Announcement> ForTeachers(AppDbContext db) =>
        db.Announcements.AsNoTracking().Where(a => (a.Audience == AnnouncementAudiences.Everyone || a.Audience == AnnouncementAudiences.Teachers || a.Audience == AnnouncementAudiences.StaffTeachers) && a.SectionId == null);

    /// <summary>What is still shown today: it has no End or its End is today or later. (An announcement is shown from the day it is posted, so people can plan for the days it is about.)</summary>
    private static IQueryable<Announcement> Live(IQueryable<Announcement> query, DateOnly today) =>
        query.Where(a => a.ExpiresOn == null || a.ExpiresOn >= today);

    /// <summary>The announcements the person manages, newest first: the coordinator the notices of the school (all but what teachers wrote to their classes), a teacher the ones to their classes.</summary>
    public async Task<IReadOnlyList<AnnouncementView>> ListManagedAsync(CurrentUserInfo user, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return user.Role switch
        {
            AppRoles.AcademicCoordinator => await ViewsAsync(db, db.Announcements.AsNoTracking().Where(a => a.AssignmentId == null), 100, ct),
            AppRoles.Teacher => await ViewsAsync(db, db.Announcements.AsNoTracking().Where(a => a.AuthorUserId == user.UserId), 100, ct),
            _ => [],
        };
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

        if (user.Role is not (AppRoles.AcademicCoordinator or AppRoles.Teacher))
        {
            return ServiceResult.Fail("Only the academic coordinator and the teachers write announcements.");
        }

        var isTeacher = user.Role == AppRoles.Teacher;
        var category = input.Category;
        if (!(isTeacher ? AnnouncementCategories.ForClass : AnnouncementCategories.All).Contains(category))
        {
            errors.Add("Pick what the announcement is about.");
        }

        // A teachers' meeting is for the teachers only; a teacher's announcement is for the students of one of their courses.
        var audience = isTeacher ? AnnouncementAudiences.Students : category == AnnouncementCategories.TeachersMeeting ? AnnouncementAudiences.Teachers : input.Audience;
        if (!AnnouncementAudiences.All.Contains(audience))
        {
            errors.Add("Pick who the announcement is for.");
        }

        if (isTeacher && input.AssignmentId is null)
        {
            errors.Add("Pick the class the announcement is for.");
        }

        if (input.ExpiresOn is { } expires && expires < PhilippineTime.Today(clock))
        {
            errors.Add("The End day can't be in the past.");
        }

        if (input.StartsOn is { } starts && input.ExpiresOn is { } ends && ends < starts)
        {
            errors.Add("The End day can't be before the Start day.");
        }

        if (errors.Count > 0)
        {
            return ServiceResult.Fail(errors);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsActiveAsync(db, user, ct))
        {
            return ServiceResult.Fail("Please sign in again.");
        }

        // A teacher can only write for a course they teach.
        int? sectionId = null;
        if (isTeacher)
        {
            var course = await db.TeachingAssignments.AsNoTracking()
                .Where(a => a.Id == input.AssignmentId && a.Teacher.UserId == user.UserId)
                .Select(a => new { a.SectionId }).FirstOrDefaultAsync(ct);
            if (course is null)
            {
                return ServiceResult.Fail("You can only write to your own classes.");
            }

            sectionId = course.SectionId;
        }

        Announcement announcement;
        var isNew = input.Id is null;
        if (input.Id is { } id)
        {
            // The coordinator changes the notices of the school; what a teacher wrote to their class is the teacher's.
            var found = await db.Announcements.FirstOrDefaultAsync(a => a.Id == id && (isTeacher ? a.AuthorUserId == user.UserId : a.AssignmentId == null), ct);
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
        announcement.Category = category;
        announcement.AssignmentId = isTeacher ? input.AssignmentId : null;
        announcement.SectionId = isTeacher ? sectionId : announcement.SectionId;
        announcement.StartsOn = input.StartsOn;
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
        if (user.Role is not (AppRoles.AcademicCoordinator or AppRoles.Teacher) || !await IsActiveAsync(db, user, ct))
        {
            return ServiceResult.Fail("Only the academic coordinator and the teachers delete announcements.");
        }

        // The coordinator deletes the notices of the school; a teacher only their own.
        var isTeacher = user.Role == AppRoles.Teacher;
        var deleted = await db.Announcements.Where(a => a.Id == id && (isTeacher ? a.AuthorUserId == user.UserId : a.AssignmentId == null)).ExecuteDeleteAsync(ct);
        return deleted == 0 ? ServiceResult.Fail("This announcement was already deleted.") : ServiceResult.Ok("The announcement was deleted.");
    }

    // ======================================================================= helpers

    /// <summary>The coordinator and the teachers (still active) write, change and delete announcements.</summary>
    private static async Task<bool> IsActiveAsync(AppDbContext db, CurrentUserInfo user, CancellationToken ct)
    {
        var roleId = user.Role == AppRoles.Teacher ? AppRoles.TeacherId : AppRoles.AcademicCoordinatorId;
        return await db.Users.AsNoTracking().AnyAsync(u => u.Id == user.UserId && u.IsActive && u.RoleId == roleId, ct);
    }

    /// <summary>Tells the people it is for. (The author is not told about their own announcement.)</summary>
    private async Task NotifyAsync(AppDbContext db, Announcement announcement, CancellationToken ct)
    {
        var students = Array.Empty<int>();
        var teachers = Array.Empty<int>();

        var forStudents = announcement.Audience is AnnouncementAudiences.Everyone or AnnouncementAudiences.Students;
        var forTeachers = announcement.Audience is AnnouncementAudiences.Everyone or AnnouncementAudiences.Teachers or AnnouncementAudiences.StaffTeachers;
        var forStaff = announcement.Audience is AnnouncementAudiences.Everyone or AnnouncementAudiences.Staff or AnnouncementAudiences.StaffTeachers;

        if (forStudents)
        {
            var query = db.Students.AsNoTracking().Where(s => s.Status == StudentStatus.Active && s.User.IsActive);
            if (announcement.AssignmentId is { } courseId)
            {
                var course = await db.TeachingAssignments.AsNoTracking().Where(a => a.Id == courseId).Select(a => new { a.SectionId, a.SubjectId }).FirstOrDefaultAsync(ct);
                query = course is null ? query.Where(_ => false) : query.Where(s => Roster.Enrollments(db, course.SectionId, course.SubjectId).Any(e => e.StudentId == s.Id));
            }
            else if (announcement.SectionId is { } sectionId)
            {
                query = query.Where(s => db.Enrollments.Any(e => e.StudentId == s.Id && e.SectionId == sectionId && e.Status == EnrollmentStatus.Enrolled));
            }

            students = (await query.Select(s => s.UserId).ToListAsync(ct)).Where(id => id != announcement.AuthorUserId).ToArray();
        }

        if (forTeachers && announcement.SectionId is null)
        {
            teachers = (await db.Teachers.AsNoTracking().Where(t => t.User.IsActive).Select(t => t.UserId).ToListAsync(ct)).Where(id => id != announcement.AuthorUserId).ToArray();
        }

        // the staff who do not teach: the registrar, the guidance counselor, the academic coordinator and the academic provider (the author is not told)
        var staff = !forStaff || announcement.SectionId is not null ? [] : (await db.Users.AsNoTracking()
            .Where(u => u.IsActive && (u.RoleId == AppRoles.RegistrarId || u.RoleId == AppRoles.GuidanceCounselorId || u.RoleId == AppRoles.AcademicCoordinatorId || u.RoleId == AppRoles.AcademicProviderId || u.RoleId == AppRoles.AdmissionId))
            .Select(u => u.Id).ToListAsync(ct)).Where(id => id != announcement.AuthorUserId).ToArray();

        var snippet = announcement.Body.Length > 140 ? announcement.Body[..139] + "…" : announcement.Body;
        await notifications.AddForManyAsync(students, "announcement", announcement.Title, snippet, "student", ct);
        await notifications.AddForManyAsync(teachers, "announcement", announcement.Title, snippet, "teacher", ct);
        await notifications.AddForManyAsync(staff, "announcement", announcement.Title, snippet, "calendar", ct);
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
            .Include(a => a.Assignment).ThenInclude(x => x!.Subject)
            .ToListAsync(ct);

        return rows.Select(a => new AnnouncementView(
            a.Id, a.AuthorUserId, a.Title, a.Body, a.Author.DisplayName, AppRoles.DisplayName(a.Author.Role.Name), a.Audience, a.SectionId,
            a.Section is null ? null : TeachingService.SectionLabel(a.Section.GradeLevel, a.Section.Strand.Code, a.Section.Name),
            a.CreatedAtUtc, a.ExpiresOn, a.Category, a.AssignmentId, a.Assignment?.Subject.Name, a.StartsOn)).ToList();
    }
}
