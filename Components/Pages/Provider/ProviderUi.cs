using SEAITT_LMS.Components.Shared;
using SEAITT_LMS.Services;

namespace SEAITT_LMS.Components.Pages.Provider;

/// <summary>Small helpers the pages of the academic provider share.</summary>
public static class ProviderUi
{
    public static Tone StateTone(string state) => state switch
    {
        DeliveryStates.NotOpened => Tone.Warning,
        DeliveryStates.Opened => Tone.Info,
        DeliveryStates.Posted => Tone.Success,
        _ => Tone.Neutral,
    };

    /// <summary>"3 hours ago", "yesterday", "5 days ago".</summary>
    public static string Ago(DateTime atUtc, DateTime nowUtc)
    {
        var span = nowUtc - atUtc;
        if (span.TotalMinutes < 1)
        {
            return "just now";
        }

        if (span.TotalHours < 1)
        {
            return $"{(int)span.TotalMinutes} min ago";
        }

        if (span.TotalHours < 24)
        {
            var hours = (int)span.TotalHours;
            return hours == 1 ? "1 hour ago" : $"{hours} hours ago";
        }

        var days = (int)span.TotalDays;
        return days == 1 ? "yesterday" : $"{days} days ago";
    }

    /// <summary>How long it took, from hours: "5 hours", "2 days".</summary>
    public static string Duration(double? hours)
    {
        if (hours is not { } h)
        {
            return "—";
        }

        if (h < 1)
        {
            return "< 1 hour";
        }

        if (h < 48)
        {
            var rounded = (int)Math.Round(h);
            return rounded == 1 ? "1 hour" : $"{rounded} hours";
        }

        return $"{(int)Math.Round(h / 24)} days";
    }

    public static string Plural(int count, string one, string many) => count == 1 ? $"1 {one}" : $"{count:N0} {many}";
}
