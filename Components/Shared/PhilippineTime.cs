using System.Globalization;

namespace SEAITT_LMS.Components.Shared;

/// <summary>
/// Dates are stored in UTC. The school is in the Philippines, which is UTC+8 all year (no daylight saving), so screens
/// add 8 hours instead of depending on the time zone of the computer the app runs on. The invariant culture keeps the
/// text the same whatever language that computer uses.
/// </summary>
public static class PhilippineTime
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(8);

    public static DateTimeOffset Now(TimeProvider clock) => clock.GetUtcNow().ToOffset(Offset);

    public static DateTimeOffset FromUtc(DateTime utc) =>
        new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToOffset(Offset);

    /// <summary>A date and time typed by a person in Philippine time (a "datetime-local" box) as the UTC moment to store.</summary>
    public static DateTime ToUtc(DateTime philippineLocal) =>
        new DateTimeOffset(DateTime.SpecifyKind(philippineLocal, DateTimeKind.Unspecified), Offset).UtcDateTime;

    /// <summary>A stored UTC moment as the Philippine date and time for a "datetime-local" box (no time zone).</summary>
    public static DateTime ToLocal(DateTime utc) => FromUtc(utc).DateTime;

    /// <summary>A stored UTC moment as the text a "datetime-local" box wants ("2026-10-05T14:30"), or empty.</summary>
    public static string ToInputValue(DateTime? utc) =>
        utc is { } moment ? ToLocal(moment).ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture) : "";

    /// <summary>The text of a "datetime-local" box (Philippine time) as the UTC moment to store. Empty or unreadable text is no date.</summary>
    public static DateTime? FromInputValue(string? text) =>
        DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local) ? ToUtc(local) : null;

    /// <summary>Today's date in the Philippines.</summary>
    public static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(Now(clock).DateTime);

    /// <summary>The Philippine calendar day of a stored UTC moment.</summary>
    public static DateOnly DayOf(DateTime utc) => DateOnly.FromDateTime(FromUtc(utc).DateTime);

    /// <summary>"due today at 5:00 PM", "due tomorrow", "due in 3 days", "1 day overdue" ...</summary>
    public static string DueText(DateTime dueUtc, DateTimeOffset now)
    {
        var due = FromUtc(dueUtc);
        var days = due.Date.DayNumberFrom() - now.Date.DayNumberFrom();
        if (due < now)
        {
            var overdue = Math.Max(1, -days);
            return days == 0 ? "overdue since " + due.ToString("h:mm tt", CultureInfo.InvariantCulture) : $"{overdue} day{(overdue == 1 ? "" : "s")} overdue";
        }

        return days switch
        {
            0 => "due today at " + due.ToString("h:mm tt", CultureInfo.InvariantCulture),
            1 => "due tomorrow at " + due.ToString("h:mm tt", CultureInfo.InvariantCulture),
            < 7 => $"due in {days} days",
            _ => "due " + due.ToString("MMM d", CultureInfo.InvariantCulture),
        };
    }

    private static int DayNumberFrom(this DateTime day) => DateOnly.FromDateTime(day).DayNumber;

    /// <summary>"Oct 3, 2026, 9:41 PM"</summary>
    public static string FormatDateTime(DateTime utc) =>
        FromUtc(utc).ToString("MMM d, yyyy, h:mm tt", CultureInfo.InvariantCulture);

    /// <summary>"Oct 3, 2026" for a UTC moment, shown in Philippine time.</summary>
    public static string FormatDate(DateTime utc) => FromUtc(utc).ToString("MMM d, yyyy", CultureInfo.InvariantCulture);

    /// <summary>"9:41 PM" for a UTC moment, shown in Philippine time.</summary>
    public static string FormatTime(DateTime utc) => FromUtc(utc).ToString("h:mm tt", CultureInfo.InvariantCulture);

    /// <summary>"Oct 3, 2026"</summary>
    public static string FormatDate(DateOnly date) => date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);

    /// <summary>"Saturday, October 3, 2026"</summary>
    public static string FormatLongDate(DateTimeOffset moment) =>
        moment.ToString("dddd, MMMM d, yyyy", CultureInfo.InvariantCulture);

    /// <summary>"Good morning", "Good afternoon" or "Good evening".</summary>
    public static string Greeting(DateTimeOffset moment) => moment.Hour switch
    {
        < 12 => "Good morning",
        < 18 => "Good afternoon",
        _ => "Good evening",
    };

    /// <summary>"7:00 PM" for 19.</summary>
    public static string HourText(int hour) => $"{(hour % 12 == 0 ? 12 : hour % 12)}:00 {(hour < 12 ? "AM" : "PM")}";
}
