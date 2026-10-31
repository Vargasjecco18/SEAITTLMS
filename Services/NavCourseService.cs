using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SEAITT_LMS.Data;

namespace SEAITT_LMS.Services;

/// <summary>A course in the menu of the school: the name of the course and, for a teacher, the section.</summary>
public sealed record NavCourse(int AssignmentId, string Name, string? Section);

/// <summary>
/// The courses a teacher teaches and a student takes now, as short lines for the box that opens at Courses in the main menu. The menu is drawn on every page,
/// so the answer is kept for a few seconds per person instead of asking the database each time.
/// </summary>
public sealed class NavCourseService(IDbContextFactory<AppDbContext> dbFactory, IMemoryCache cache)
{
    private static readonly TimeSpan Keep = TimeSpan.FromSeconds(20);

    public async Task<IReadOnlyList<NavCourse>> ForTeacherAsync(int teacherId, CancellationToken ct = default) =>
        await cache.GetOrCreateAsync($"nav-courses:t:{teacherId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Keep;
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var rows = await db.TeachingAssignments.AsNoTracking()
                .Where(a => a.TeacherId == teacherId)
                .OrderBy(a => a.Section.GradeLevel).ThenBy(a => a.Section.Strand.Code).ThenBy(a => a.Section.Name).ThenBy(a => a.Subject.Name)
                .Select(a => new { a.Id, Subject = a.Subject.Name, a.Section.GradeLevel, Strand = a.Section.Strand.Code, Section = a.Section.Name })
                .ToListAsync(ct);
            return (IReadOnlyList<NavCourse>)[.. rows.Select(r => new NavCourse(r.Id, r.Subject, TeachingService.SectionLabel(r.GradeLevel, r.Strand, r.Section)))];
        }) ?? [];

    public async Task<IReadOnlyList<NavCourse>> ForStudentAsync(int studentId, CancellationToken ct = default) =>
        await cache.GetOrCreateAsync($"nav-courses:s:{studentId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Keep;
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var rows = await Roster.AssignmentsOf(db, studentId).AsNoTracking()
                .OrderBy(a => a.Subject.Name)
                .Select(a => new { a.Id, Subject = a.Subject.Name })
                .ToListAsync(ct);
            return (IReadOnlyList<NavCourse>)[.. rows.Select(r => new NavCourse(r.Id, r.Subject, null))];
        }) ?? [];
}
