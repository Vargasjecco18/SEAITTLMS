using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Components.Shared;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>
/// The school years and their terms keep themselves: there is always the school year that is running and the two after it, and every one of them has
/// three terms. When a school year is over the next one is already there (with its terms), and a new one is added at the end. Nothing is ever deleted:
/// a school year that is over is only put away in the setup page (its students, grades and attendance stay).
/// </summary>
public static class SchoolCalendar
{
    /// <summary>The school year that is running and this many after it.</summary>
    public const int YearsAhead = 2;

    public const int TermsPerYear = 3;

    /// <summary>"1st Term", "2nd Term", "3rd Term".</summary>
    public static string TermName(int number) => number switch
    {
        1 => "1st Term",
        2 => "2nd Term",
        3 => "3rd Term",
        _ => $"{number}th Term",
    };

    /// <summary>"2026-2027" for a school year that starts in 2026 and ends in 2027.</summary>
    public static string YearName(DateOnly start, DateOnly end) => $"{start.Year}-{end.Year}";

    /// <summary>The first and the last day of the term that would be number <paramref name="number"/> of three equal parts of the school year.</summary>
    public static (DateOnly Start, DateOnly End) Part(DateOnly start, DateOnly end, int number, int parts = TermsPerYear)
    {
        var days = end.DayNumber - start.DayNumber + 1;
        var length = days / parts;
        var first = start.AddDays((number - 1) * length);
        var last = number == parts ? end : first.AddDays(length - 1);
        return (first, last);
    }

    /// <summary>The school year that comes after this one: the same days of the year, one year later.</summary>
    public static (string Name, DateOnly Start, DateOnly End) NextYear(DateOnly start, DateOnly end)
    {
        var nextStart = start.AddYears(1);
        var nextEnd = end.AddYears(1);
        return (YearName(nextStart, nextEnd), nextStart, nextEnd);
    }

    /// <summary>Makes sure the school year of today and the next two exist, each with its three terms. Safe to call as often as wanted.</summary>
    public static async Task EnsureAsync(IDbContextFactory<AppDbContext> dbFactory, TimeProvider clock, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var today = PhilippineTime.Today(clock);
        var changed = false;

        var years = await db.SchoolYears.AsNoTracking().OrderBy(y => y.StartDate).Select(y => new { y.Id, y.Name, y.StartDate, y.EndDate }).ToListAsync(ct);
        if (years.Count == 0)
        {
            // A school with no school year starts with the one of today (June to March, like the school's own).
            var startYear = today.Month >= 6 ? today.Year : today.Year - 1;
            var start = new DateOnly(startYear, 6, 8);
            var end = new DateOnly(startYear + 1, 3, 31);
            db.SchoolYears.Add(new SchoolYear { Name = YearName(start, end), StartDate = start, EndDate = end });
            await db.SaveChangesAsync(ct);
            changed = true;
            years = await db.SchoolYears.AsNoTracking().OrderBy(y => y.StartDate).Select(y => new { y.Id, y.Name, y.StartDate, y.EndDate }).ToListAsync(ct);
        }

        // The years that are not over yet must be three (the running one and two more): the next one is made like the last one, one year later.
        for (var guard = 0; guard < 12 && years.Count(y => y.EndDate >= today) < YearsAhead + 1; guard++)
        {
            var last = years[^1];
            var (name, start, end) = NextYear(last.StartDate, last.EndDate);
            if (years.Any(y => y.Name == name))
            {
                break;
            }

            db.SchoolYears.Add(new SchoolYear { Name = name, StartDate = start, EndDate = end });
            await db.SaveChangesAsync(ct);
            changed = true;
            years = await db.SchoolYears.AsNoTracking().OrderBy(y => y.StartDate).Select(y => new { y.Id, y.Name, y.StartDate, y.EndDate }).ToListAsync(ct);
        }

        foreach (var year in years.Where(y => y.EndDate >= today))
        {
            var terms = await db.Terms.AsNoTracking().Where(t => t.SchoolYearId == year.Id).Select(t => new { t.TermNumber, t.StartDate, t.EndDate }).ToListAsync(ct);
            var missing = Enumerable.Range(1, TermsPerYear).Where(n => terms.All(t => t.TermNumber != n)).ToList();
            if (missing.Count == 0)
            {
                continue;
            }

            foreach (var (number, start, end) in PlaceMissing(year.StartDate, year.EndDate, terms.Select(t => (t.StartDate, t.EndDate)).ToList(), missing))
            {
                db.Terms.Add(new Term { SchoolYearId = year.Id, TermNumber = (byte)number, Name = TermName(number), StartDate = start, EndDate = end, IsCurrent = false });
                changed = true;
            }
        }

        if (changed)
        {
            await db.SaveChangesAsync(ct);
        }

        await TermClock.SyncAsync(dbFactory, clock, ct);
    }

    /// <summary>
    /// Where the terms that are missing go: with no term at all the school year is cut in three equal parts; with some terms already there, the free days of the school year
    /// (the days no term covers) are shared by the missing ones, the earlier numbers first. Nothing is placed over a term that exists.
    /// </summary>
    public static IReadOnlyList<(int Number, DateOnly Start, DateOnly End)> PlaceMissing(DateOnly yearStart, DateOnly yearEnd, IReadOnlyList<(DateOnly Start, DateOnly End)> existing, IReadOnlyList<int> missing)
    {
        if (missing.Count == 0)
        {
            return [];
        }

        if (existing.Count == 0)
        {
            return [.. missing.Select(n => { var (s, e) = Part(yearStart, yearEnd, n); return (n, s, e); })];
        }

        // The free stretches of the school year.
        var gaps = new List<(DateOnly Start, DateOnly End)>();
        var cursor = yearStart;
        foreach (var term in existing.OrderBy(t => t.Start))
        {
            if (term.Start > cursor)
            {
                gaps.Add((cursor, term.Start.AddDays(-1)));
            }

            if (term.End.AddDays(1) > cursor)
            {
                cursor = term.End.AddDays(1);
            }
        }

        if (cursor <= yearEnd)
        {
            gaps.Add((cursor, yearEnd));
        }

        gaps = [.. gaps.Where(g => g.End.DayNumber - g.Start.DayNumber >= 6)];   // a stretch shorter than a week is no term
        if (gaps.Count == 0)
        {
            return [];
        }

        // Fewer stretches than terms: the longest stretch is cut in as many parts as needed.
        var wanted = missing.Order().ToList();
        while (gaps.Count < wanted.Count)
        {
            var longest = gaps.OrderByDescending(g => g.End.DayNumber - g.Start.DayNumber).First();
            if (longest.End.DayNumber - longest.Start.DayNumber < 13)
            {
                break;
            }

            gaps.Remove(longest);
            var middle = longest.Start.AddDays((longest.End.DayNumber - longest.Start.DayNumber) / 2);
            gaps.Add((longest.Start, middle));
            gaps.Add((middle.AddDays(1), longest.End));
        }

        gaps = [.. gaps.OrderBy(g => g.Start)];
        var result = new List<(int, DateOnly, DateOnly)>();
        for (var i = 0; i < Math.Min(gaps.Count, wanted.Count); i++)
        {
            result.Add((wanted[i], gaps[i].Start, gaps[i].End));
        }

        return result;
    }
}
