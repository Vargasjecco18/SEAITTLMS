namespace SEAITT_LMS.Services;

/// <summary>One holiday of the Philippines on the calendar.</summary>
/// <param name="Kind">"Regular holiday", "Special non-working day" or "Special working day".</param>
/// <param name="Estimated">The date is worked out from the moon (Chinese New Year, Eid'l Fitr, Eid'l Adha): the government sets the day by proclamation, so it can be a day off.</param>
public sealed record Holiday(DateOnly Day, string Name, string Kind, bool Estimated = false);

/// <summary>
/// The holidays of the Philippines (the regular holidays, the special non-working days and the special working day of EDSA): the ones with a fixed day, National Heroes Day (the
/// last Monday of August), Maundy Thursday, Good Friday and Black Saturday (worked out from Easter), and the three that follow the moon (Chinese New Year, Eid'l Fitr, Eid'l Adha;
/// known for 2024 to 2030). They are worked out in code when the calendar is opened, so nothing is stored in the database and nothing is read for a year nobody looks at.
/// A day the government adds or moves by proclamation (a day off for an election, a typhoon) is not here.
/// </summary>
public static class PhilippineHolidays
{
    public const string Regular = "Regular holiday";
    public const string SpecialNonWorking = "Special non-working day";
    public const string SpecialWorking = "Special working day";

    private static readonly Dictionary<int, IReadOnlyList<Holiday>> Cache = [];

    // The days that follow the moon (Chinese New Year; Eid'l Fitr and Eid'l Adha are estimates that the proclamation can move by a day).
    private static readonly Dictionary<int, DateOnly> ChineseNewYear = new()
    {
        [2024] = new(2024, 2, 10), [2025] = new(2025, 1, 29), [2026] = new(2026, 2, 17), [2027] = new(2027, 2, 6), [2028] = new(2028, 1, 26), [2029] = new(2029, 2, 13), [2030] = new(2030, 2, 3),
    };

    private static readonly Dictionary<int, DateOnly> EidFitr = new()
    {
        [2024] = new(2024, 4, 10), [2025] = new(2025, 3, 31), [2026] = new(2026, 3, 20), [2027] = new(2027, 3, 10), [2028] = new(2028, 2, 27), [2029] = new(2029, 2, 14), [2030] = new(2030, 2, 4),
    };

    private static readonly Dictionary<int, DateOnly> EidAdha = new()
    {
        [2024] = new(2024, 6, 17), [2025] = new(2025, 6, 6), [2026] = new(2026, 5, 27), [2027] = new(2027, 5, 17), [2028] = new(2028, 5, 5), [2029] = new(2029, 4, 24), [2030] = new(2030, 4, 13),
    };

    /// <summary>The holidays of one year, oldest first.</summary>
    public static IReadOnlyList<Holiday> ForYear(int year)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(year, out var known))
            {
                return known;
            }

            var easter = Easter(year);
            var list = new List<Holiday>
            {
                new(new(year, 1, 1), "New Year's Day", Regular),
                new(new(year, 2, 25), "EDSA People Power Revolution Anniversary", SpecialWorking),
                new(easter.AddDays(-3), "Maundy Thursday", Regular),
                new(easter.AddDays(-2), "Good Friday", Regular),
                new(easter.AddDays(-1), "Black Saturday", SpecialNonWorking),
                new(new(year, 4, 9), "Araw ng Kagitingan", Regular),
                new(new(year, 5, 1), "Labor Day", Regular),
                new(new(year, 6, 12), "Independence Day", Regular),
                new(new(year, 8, 21), "Ninoy Aquino Day", SpecialNonWorking),
                new(LastMonday(year, 8), "National Heroes Day", Regular),
                new(new(year, 11, 1), "All Saints' Day", SpecialNonWorking),
                new(new(year, 11, 30), "Bonifacio Day", Regular),
                new(new(year, 12, 8), "Feast of the Immaculate Conception", SpecialNonWorking),
                new(new(year, 12, 24), "Christmas Eve", SpecialNonWorking),
                new(new(year, 12, 25), "Christmas Day", Regular),
                new(new(year, 12, 30), "Rizal Day", Regular),
                new(new(year, 12, 31), "Last Day of the Year", SpecialNonWorking),
            };

            if (ChineseNewYear.TryGetValue(year, out var chinese))
            {
                list.Add(new(chinese, "Chinese New Year", SpecialNonWorking, true));
            }

            if (EidFitr.TryGetValue(year, out var fitr))
            {
                list.Add(new(fitr, "Eid'l Fitr", Regular, true));
            }

            if (EidAdha.TryGetValue(year, out var adha))
            {
                list.Add(new(adha, "Eid'l Adha", Regular, true));
            }

            var sorted = list.OrderBy(h => h.Day).ThenBy(h => h.Name).ToList();
            Cache[year] = sorted;
            return sorted;
        }
    }

    /// <summary>The holidays from one day to another, both included.</summary>
    public static IEnumerable<Holiday> Between(DateOnly from, DateOnly to)
    {
        for (var year = from.Year; year <= to.Year; year++)
        {
            foreach (var holiday in ForYear(year))
            {
                if (holiday.Day >= from && holiday.Day <= to)
                {
                    yield return holiday;
                }
            }
        }
    }

    /// <summary>The Easter Sunday of a year (the Gregorian rule of Meeus, Jones and Butcher).</summary>
    public static DateOnly Easter(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var month = (h + l - 7 * m + 114) / 31;
        var day = (h + l - 7 * m + 114) % 31 + 1;
        return new DateOnly(year, month, day);
    }

    private static DateOnly LastMonday(int year, int month)
    {
        var day = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        while (day.DayOfWeek != DayOfWeek.Monday)
        {
            day = day.AddDays(-1);
        }

        return day;
    }
}
