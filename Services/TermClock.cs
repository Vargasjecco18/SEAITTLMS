using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Components.Shared;
using SEAITT_LMS.Data;

namespace SEAITT_LMS.Services;

/// <summary>
/// Which term the school is in is decided by the calendar, not by a switch: the term whose first day and last day include today. Between two terms (after one ended and before the
/// next starts) it is the term that starts next; after the last term it is the last one. A term that is over is no longer offered when students are enrolled.
/// </summary>
public static class TermClock
{
    /// <summary>The current term of these terms (id, first day, last day) on this day, or null when there is no term at all.</summary>
    public static int? CurrentId(IEnumerable<(int Id, DateOnly Start, DateOnly End)> terms, DateOnly today)
    {
        var list = terms.OrderBy(t => t.Start).ThenBy(t => t.Id).ToList();
        if (list.Count == 0)
        {
            return null;
        }

        foreach (var t in list)
        {
            if (today >= t.Start && today <= t.End)
            {
                return t.Id;
            }
        }

        var next = list.FirstOrDefault(t => t.Start > today);
        return next.Id != 0 ? next.Id : list[^1].Id;
    }

    /// <summary>A term is over once its last day has passed.</summary>
    public static bool IsEnded(DateOnly end, DateOnly today) => end < today;

    /// <summary>Makes the term of today the current one in the database (the other terms are not). Called when the app starts, every few minutes and when a term is saved.</summary>
    public static async Task SyncAsync(IDbContextFactory<AppDbContext> dbFactory, TimeProvider clock, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var terms = await db.Terms.AsNoTracking().Select(t => new { t.Id, t.StartDate, t.EndDate, t.IsCurrent }).ToListAsync(ct);
        if (CurrentId(terms.Select(t => (t.Id, t.StartDate, t.EndDate)), PhilippineTime.Today(clock)) is not { } id)
        {
            return;
        }

        if (terms.Count(t => t.IsCurrent) == 1 && terms.First(t => t.IsCurrent).Id == id)
        {
            return;
        }

        await db.Terms.Where(t => t.Id != id && t.IsCurrent).ExecuteUpdateAsync(set => set.SetProperty(t => t.IsCurrent, false), ct);
        await db.Terms.Where(t => t.Id == id && !t.IsCurrent).ExecuteUpdateAsync(set => set.SetProperty(t => t.IsCurrent, true), ct);
    }
}
