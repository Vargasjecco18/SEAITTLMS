using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Data.Queries;

/// <summary>A student who is at school (not archived) but has no enrollment in a term, with the last place they were enrolled in.</summary>
public sealed record NotEnrolledRow(int StudentId, string Name, string Lrn, string? LastPlacement, string? LastTerm);

public sealed record NotEnrolledPage(TermRef? Term, IReadOnlyList<NotEnrolledRow> Items, int Total, int Page, int PageSize)
{
    public int PageCount => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

/// <summary>
/// The students who are not enrolled for a term: active students with no enrollment (status Enrolled) in it. The term is the
/// next term when there is one, otherwise the current term (see <see cref="TermLookup.TargetAsync"/>). The registrar's
/// dashboard and the administrator's school setup both show this list; it is read-only.
/// </summary>
public static class NotEnrolledList
{
    public static async Task<NotEnrolledPage> LoadAsync(AppDbContext db, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var term = await TermLookup.TargetAsync(db, ct);
        if (term is null)
        {
            return new NotEnrolledPage(null, [], 0, 1, pageSize);
        }

        var termId = term.Id;
        var query = db.Students.AsNoTracking()
            .Where(s => s.Status == StudentStatus.Active
                        && !db.Enrollments.Any(e => e.StudentId == s.Id && e.TermId == termId && e.Status == EnrollmentStatus.Enrolled));

        foreach (var word in (search ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(6))
        {
            var w = word;
            query = query.Where(s => s.FirstName.Contains(w) || (s.MiddleName != null && s.MiddleName.Contains(w)) || s.LastName.Contains(w) || s.Lrn.Contains(w));
        }

        var total = await query.CountAsync(ct);
        page = Math.Min(page, Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)));

        var rows = await query.OrderBy(s => s.LastName).ThenBy(s => s.FirstName).ThenBy(s => s.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new { s.Id, s.FirstName, s.MiddleName, s.LastName, s.Lrn })
            .ToListAsync(ct);

        // Where each of them was enrolled last (the newest term first).
        var ids = rows.Select(r => r.Id).ToList();
        var last = await db.Enrollments.AsNoTracking()
            .Where(e => ids.Contains(e.StudentId))
            .OrderByDescending(e => e.Term.SchoolYear.StartDate).ThenByDescending(e => e.Term.TermNumber)
            .Select(e => new { e.StudentId, e.GradeLevel, Strand = e.Strand.Code, Section = e.Section.Name, Term = e.Term.Name, Year = e.Term.SchoolYear.Name })
            .ToListAsync(ct);

        var items = rows.Select(r =>
        {
            var l = last.FirstOrDefault(x => x.StudentId == r.Id);
            return new NotEnrolledRow(
                r.Id,
                string.Join(' ', new[] { r.FirstName, r.MiddleName, r.LastName }.Where(p => !string.IsNullOrWhiteSpace(p))),
                r.Lrn,
                l is null ? null : $"{l.GradeLevel}-{l.Strand}-{l.Section}",
                l is null ? null : $"{l.Term} · {l.Year}");
        }).ToList();

        return new NotEnrolledPage(term, items, total, page, pageSize);
    }

    /// <summary>How many students are not enrolled for the term the list is about.</summary>
    public static async Task<(TermRef? Term, int Count)> CountAsync(AppDbContext db, CancellationToken ct = default)
    {
        var term = await TermLookup.TargetAsync(db, ct);
        if (term is null)
        {
            return (null, 0);
        }

        var termId = term.Id;
        var count = await db.Students.CountAsync(
            s => s.Status == StudentStatus.Active
                 && !db.Enrollments.Any(e => e.StudentId == s.Id && e.TermId == termId && e.Status == EnrollmentStatus.Enrolled), ct);
        return (term, count);
    }
}
