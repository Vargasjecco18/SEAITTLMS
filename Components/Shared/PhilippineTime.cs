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

    /// <summary>"Oct 3, 2026, 9:41 PM"</summary>
    public static string FormatDateTime(DateTime utc) =>
        FromUtc(utc).ToString("MMM d, yyyy, h:mm tt", CultureInfo.InvariantCulture);

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
}
