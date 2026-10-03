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
