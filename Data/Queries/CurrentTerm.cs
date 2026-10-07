using Microsoft.EntityFrameworkCore;

namespace SEAITT_LMS.Data.Queries;

/// <summary>The term the school is running right now (terms.is_current), for the home page.</summary>
public sealed record CurrentTerm(string TermName, string SchoolYearName, DateOnly StartDate, DateOnly EndDate)
{
    /// <summary>The current term, or null when no term is marked as current yet.</summary>
    public static Task<CurrentTerm?> LoadAsync(AppDbContext db, CancellationToken ct = default) =>
        db.Terms
            .AsNoTracking()
            .Where(t => t.IsCurrent)
            .Select(t => new CurrentTerm(t.Name, t.SchoolYear.Name, t.StartDate, t.EndDate))
            .FirstOrDefaultAsync(ct);
}

/// <summary>A term as a short reference: "2nd Term of 2026-2027".</summary>
public sealed record TermRef(int Id, string Name, string SchoolYear)
{
    public string Label => $"{Name} · {SchoolYear}";
}

/// <summary>Which term is running, which one comes next, and which one the "not enrolled" lists are about.</summary>
public static class TermLookup
{
    public static async Task<TermRef?> CurrentAsync(AppDbContext db, CancellationToken ct = default) =>
        await db.Terms.AsNoTracking().Where(t => t.IsCurrent).Select(t => new TermRef(t.Id, t.Name, t.SchoolYear.Name)).FirstOrDefaultAsync(ct);

    /// <summary>
    /// The term after the current one: the next term of the same school year, or the first term of the next school year.
    /// Null when no term is marked as current or nothing comes after it.
    /// </summary>
    public static async Task<TermRef?> NextAsync(AppDbContext db, CancellationToken ct = default)
    {
        var ordered = await db.Terms.AsNoTracking()
            .OrderBy(t => t.SchoolYear.StartDate).ThenBy(t => t.TermNumber)
            .Select(t => new { t.Id, t.Name, SchoolYear = t.SchoolYear.Name, t.IsCurrent })
            .ToListAsync(ct);

        var index = ordered.FindIndex(t => t.IsCurrent);
        if (index < 0 || index + 1 >= ordered.Count)
        {
            return null;
        }

        var next = ordered[index + 1];
        return new TermRef(next.Id, next.Name, next.SchoolYear);
    }

    /// <summary>
    /// The term the "not enrolled" lists are about: the next term when there is one (the registrar enrolls students ahead of
    /// it), otherwise the current term. Null when there is neither.
    /// </summary>
    public static async Task<TermRef?> TargetAsync(AppDbContext db, CancellationToken ct = default) =>
        await NextAsync(db, ct) ?? await CurrentAsync(db, ct);
}
