using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

public sealed record SearchHit(string Title, string? Subtitle, string Href, string Icon);

public sealed record SearchGroup(string Title, IReadOnlyList<SearchHit> Hits);

/// <summary>
/// The search box in the header. It only finds what the person may see: their own pages and, depending on the role,
/// the subjects they take or teach and the people they work with.
/// </summary>
public sealed class SearchService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider clock)
{
    private const int PerGroup = 8;

    public async Task<IReadOnlyList<SearchGroup>> SearchAsync(CurrentUserInfo user, string? query, CancellationToken ct = default)
    {
        var tokens = (query ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(5).ToArray();
        var groups = new List<SearchGroup>();
        if (tokens.Length == 0)
        {
            return groups;
        }

        var pages = PageDirectory.For(user.Role)
            .Where(p => tokens.All(t => p.Title.Contains(t, StringComparison.OrdinalIgnoreCase) || p.Description.Contains(t, StringComparison.OrdinalIgnoreCase)))
            .Select(p => new SearchHit(p.Title, p.Description, p.Href, p.Icon))
            .ToList();
        if (pages.Count > 0)
        {
            groups.Add(new SearchGroup("Pages", pages));
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        if (user.Role == AppRoles.Admin)
        {
            var people = await FindUsersAsync(db, tokens, ct);
            if (people.Count > 0)
            {
                groups.Add(new SearchGroup("People", people));
            }
        }
        else if (user.Role == AppRoles.Registrar)
        {
            var students = await FindStudentsAsync(db, tokens, ct);
            if (students.Count > 0)
            {
                groups.Add(new SearchGroup("Students", students));
            }
        }
        else if (user.Role == AppRoles.Teacher)
        {
            var subjects = await FindTeacherSubjectsAsync(db, user.UserId, tokens, ct);
            if (subjects.Count > 0)
            {
                groups.Add(new SearchGroup("Subjects", subjects));
            }

            var students = await FindTeacherStudentsAsync(db, user.UserId, tokens, ct);
            if (students.Count > 0)
            {
                groups.Add(new SearchGroup("My students", students));
            }
        }
        else if (user.Role == AppRoles.GuidanceCounselor)
        {
            var students = await ByName(db.Students.AsNoTracking().Where(s => s.Status == StudentStatus.Active), tokens).OrderBy(x => x.LastName).ThenBy(x => x.FirstName).Take(PerGroup * 2)
                .Select(x => new { x.FirstName, x.MiddleName, x.LastName, x.Lrn }).ToListAsync(ct);
            if (students.Count > 0)
            {
                groups.Add(new SearchGroup("Students", students.Select(s => new SearchHit(
                    UserDirectory.FullName(s.FirstName, s.MiddleName, s.LastName), $"LRN {s.Lrn} · see their referrals", $"guidance/referrals?q={Uri.EscapeDataString(s.Lrn)}", "graduation-cap")).ToList()));
            }
        }
        else if (user.Role == AppRoles.Student)
        {
            var (subjects, work) = await FindStudentWorkAsync(db, user.UserId, tokens, ct);
            if (subjects.Count > 0)
            {
                groups.Add(new SearchGroup("Subjects", subjects));
            }

            if (work.Count > 0)
            {
                groups.Add(new SearchGroup("Modules and work", work));
            }
        }

        return groups;
    }

    /// <summary>A teacher finds the subjects they teach by the name of the subject or the section.</summary>
    private static async Task<List<SearchHit>> FindTeacherSubjectsAsync(AppDbContext db, int userId, string[] tokens, CancellationToken ct)
    {
        var query = db.TeachingAssignments.AsNoTracking().Where(a => a.Teacher.UserId == userId);
        foreach (var token in tokens)
        {
            var t = token;
            query = query.Where(a => a.Subject.Name.Contains(t) || a.Section.Name.Contains(t) || a.Section.Strand.Code.Contains(t));
        }

        var rows = await query.OrderBy(a => a.Subject.Name).Take(PerGroup)
            .Select(a => new { a.Id, Subject = a.Subject.Name, a.Section.GradeLevel, Strand = a.Section.Strand.Code, Section = a.Section.Name })
            .ToListAsync(ct);
        return rows.Select(r => new SearchHit(r.Subject, TeachingService.SectionLabel(r.GradeLevel, r.Strand, r.Section), $"teacher/subjects/{r.Id}", "book-open")).ToList();
    }

    /// <summary>A teacher finds the students of their subjects by name or LRN; each hit opens the gradebook of the subject.</summary>
    private static async Task<List<SearchHit>> FindTeacherStudentsAsync(AppDbContext db, int userId, string[] tokens, CancellationToken ct)
    {
        var pairs = await db.TeachingAssignments.AsNoTracking().Where(a => a.Teacher.UserId == userId)
            .Select(a => new { a.Id, a.SectionId, a.SubjectId, Subject = a.Subject.Name }).ToListAsync(ct);
        var hits = new List<SearchHit>();
        foreach (var a in pairs)
        {
            var ids = Roster.StudentIds(db, a.SectionId, a.SubjectId);
            var query = db.Students.AsNoTracking().Where(s => ids.Contains(s.Id));
            foreach (var s in await ByName(query, tokens).OrderBy(x => x.LastName).ThenBy(x => x.FirstName).Take(PerGroup)
                         .Select(x => new { x.FirstName, x.MiddleName, x.LastName, x.Lrn }).ToListAsync(ct))
            {
                hits.Add(new SearchHit(UserDirectory.FullName(s.FirstName, s.MiddleName, s.LastName), $"{a.Subject} · LRN {s.Lrn}", $"teacher/gradebook?subject={a.Id}", "graduation-cap"));
            }
        }

        return hits.Take(PerGroup * 2).ToList();
    }

    /// <summary>A student finds their subjects, and the modules and work that are posted in them, by title.</summary>
    private async Task<(List<SearchHit> Subjects, List<SearchHit> Work)> FindStudentWorkAsync(AppDbContext db, int userId, string[] tokens, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var mine = db.EnrollmentSubjects.Where(x => x.Enrollment.Student.UserId == userId && x.Enrollment.Status == EnrollmentStatus.Enrolled)
            .Select(x => new { x.Enrollment.SectionId, x.SubjectId });
        var assignments = db.TeachingAssignments.AsNoTracking().Where(a => mine.Any(m => m.SectionId == a.SectionId && m.SubjectId == a.SubjectId));

        var subjectQuery = assignments;
        foreach (var token in tokens)
        {
            var t = token;
            subjectQuery = subjectQuery.Where(a => a.Subject.Name.Contains(t) || a.Teacher.FirstName.Contains(t) || a.Teacher.LastName.Contains(t));
        }

        var subjects = (await subjectQuery.OrderBy(a => a.Subject.Name).Take(PerGroup)
                .Select(a => new { a.Id, Subject = a.Subject.Name, Teacher = a.Teacher.FirstName + " " + a.Teacher.LastName }).ToListAsync(ct))
            .Select(r => new SearchHit(r.Subject, r.Teacher, $"student/subjects/{r.Id}", "book-open")).ToList();

        var work = new List<SearchHit>();

        var moduleQuery = db.Modules.AsNoTracking().Where(m => m.VisibleFromUtc != null && m.VisibleFromUtc <= now && assignments.Any(a => a.Id == m.AssignmentId));
        foreach (var token in tokens)
        {
            var t = token;
            moduleQuery = moduleQuery.Where(m => m.Title.Contains(t));
        }

        foreach (var m in await moduleQuery.OrderBy(m => m.Title).Take(PerGroup).Select(m => new { m.Id, m.Title, Subject = m.Assignment.Subject.Name }).ToListAsync(ct))
        {
            work.Add(new SearchHit(m.Title, $"Module · {m.Subject}", $"student/modules/{m.Id}", "layers"));
        }

        var itemQuery = db.ModuleItems.AsNoTracking().Where(i => (i.VisibleFromUtc == null || i.VisibleFromUtc <= now)
                                                                  && i.Module.VisibleFromUtc != null && i.Module.VisibleFromUtc <= now
                                                                  && assignments.Any(a => a.Id == i.Module.AssignmentId));
        foreach (var token in tokens)
        {
            var t = token;
            itemQuery = itemQuery.Where(i => i.Title.Contains(t));
        }

        foreach (var i in await itemQuery.OrderBy(i => i.Title).Take(PerGroup).Select(i => new { i.Id, i.Title, i.Kind, Subject = i.Module.Assignment.Subject.Name }).ToListAsync(ct))
        {
            work.Add(new SearchHit(i.Title, $"{ItemKinds.Display(i.Kind)} · {i.Subject}", $"student/items/{i.Id}", i.Kind == ItemKinds.Quiz ? "file-question" : i.Kind == ItemKinds.TaskPerformance ? "clipboard-check" : "notebook-pen"));
        }

        return (subjects, work);
    }

    /// <summary>Administrators find everybody; each hit opens the list of that role.</summary>
    private static async Task<List<SearchHit>> FindUsersAsync(AppDbContext db, string[] tokens, CancellationToken ct)
    {
        var hits = new List<SearchHit>();

        foreach (var s in await ByName(db.Students.AsNoTracking(), tokens).OrderBy(x => x.LastName).Take(PerGroup)
                     .Select(x => new { x.FirstName, x.MiddleName, x.LastName, x.Lrn }).ToListAsync(ct))
        {
            hits.Add(new(UserDirectory.FullName(s.FirstName, s.MiddleName, s.LastName), $"Student · LRN {s.Lrn}", $"admin/users/students?q={Uri.EscapeDataString(s.Lrn)}", "graduation-cap"));
        }

        foreach (var s in await ByName(db.Teachers.AsNoTracking(), tokens).OrderBy(x => x.LastName).Take(PerGroup)
                     .Select(x => new { x.FirstName, x.MiddleName, x.LastName, x.User.Email }).ToListAsync(ct))
        {
            hits.Add(new(UserDirectory.FullName(s.FirstName, s.MiddleName, s.LastName), $"Teacher · {s.Email}", $"admin/users/teachers?q={Uri.EscapeDataString(s.Email)}", "presentation"));
        }

        foreach (var s in await ByName(db.Registrars.AsNoTracking(), tokens).OrderBy(x => x.LastName).Take(PerGroup)
                     .Select(x => new { x.FirstName, x.MiddleName, x.LastName, x.User.Email }).ToListAsync(ct))
        {
            hits.Add(new(UserDirectory.FullName(s.FirstName, s.MiddleName, s.LastName), $"Registrar · {s.Email}", "admin/users/registrars", "clipboard-check"));
        }

        foreach (var s in await ByName(db.GuidanceCounselors.AsNoTracking(), tokens).OrderBy(x => x.LastName).Take(PerGroup)
                     .Select(x => new { x.FirstName, x.MiddleName, x.LastName, x.User.Email }).ToListAsync(ct))
        {
            hits.Add(new(UserDirectory.FullName(s.FirstName, s.MiddleName, s.LastName), $"Guidance counselor · {s.Email}", "admin/users/guidance", "heart-handshake"));
        }

        foreach (var s in await ByName(db.Administrators.AsNoTracking(), tokens).OrderBy(x => x.LastName).Take(PerGroup)
                     .Select(x => new { x.FirstName, x.MiddleName, x.LastName, x.User.Email }).ToListAsync(ct))
        {
            hits.Add(new(UserDirectory.FullName(s.FirstName, s.MiddleName, s.LastName), $"Administrator · {s.Email}", "admin/users/administrators", "shield-check"));
        }

        return hits;
    }

    private static async Task<List<SearchHit>> FindStudentsAsync(AppDbContext db, string[] tokens, CancellationToken ct)
    {
        var rows = await ByName(db.Students.AsNoTracking(), tokens).OrderBy(x => x.LastName).ThenBy(x => x.FirstName).Take(PerGroup * 2)
            .Select(x => new { x.FirstName, x.MiddleName, x.LastName, x.Lrn }).ToListAsync(ct);

        return rows.Select(s => new SearchHit(
            UserDirectory.FullName(s.FirstName, s.MiddleName, s.LastName), $"LRN {s.Lrn}", $"registrar/student-accounts?q={Uri.EscapeDataString(s.Lrn)}", "graduation-cap")).ToList();
    }

    /// <summary>Every word must be somewhere in the name or the email (and the LRN, for students).</summary>
    private static IQueryable<T> ByName<T>(IQueryable<T> source, string[] tokens) where T : PersonProfile
    {
        foreach (var token in tokens)
        {
            var t = token;
            source = source.Where(p => p.FirstName.Contains(t) || (p.MiddleName != null && p.MiddleName.Contains(t)) || p.LastName.Contains(t) || p.User.Email.Contains(t));
        }

        return source;
    }

    /// <summary>The same for students, who also have an LRN.</summary>
    private static IQueryable<Student> ByName(IQueryable<Student> source, string[] tokens)
    {
        foreach (var token in tokens)
        {
            var t = token;
            source = source.Where(p => p.FirstName.Contains(t) || (p.MiddleName != null && p.MiddleName.Contains(t)) || p.LastName.Contains(t) || p.User.Email.Contains(t) || p.Lrn.Contains(t));
        }

        return source;
    }
}
