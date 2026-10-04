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
public sealed class SearchService(IDbContextFactory<AppDbContext> dbFactory)
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

        return groups;
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
