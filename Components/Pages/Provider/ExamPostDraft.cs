using SEAITT_LMS.Components.Shared;
using SEAITT_LMS.Services;

namespace SEAITT_LMS.Components.Pages.Provider;

/// <summary>What is typed in <see cref="ExamPostForm"/> before the summative exam is posted to the students: the settings of the exam and when it opens and closes.</summary>
public sealed class ExamPostDraft
{
    public int? TimeLimitMinutes { get; set; }
    public bool AntiCheat { get; set; } = true;
    public int MaxViolations { get; set; } = 3;
    public bool ShuffleQuestions { get; set; } = true;
    public bool ShuffleOptions { get; set; } = true;
    public bool ShowResult { get; set; } = true;

    /// <summary>The two boxes of the date (they hold the text of a datetime-local input, in Philippine time).</summary>
    public string OpensText { get; set; } = "";
    public string DueText { get; set; } = "";

    /// <summary>The settings to post with, or the sentence that says what is missing.</summary>
    public (ExamPostSettings? Settings, string? Problem) Read()
    {
        var opens = PhilippineTime.FromInputValue(OpensText);
        var closes = PhilippineTime.FromInputValue(DueText);
        if (opens is null || closes is null)
        {
            return (null, "Set when the exam opens and when it closes.");
        }

        var settings = new ExamPostSettings(opens.Value, closes.Value, TimeLimitMinutes, AntiCheat, MaxViolations, ShuffleQuestions, ShuffleOptions, ShowResult);
        return settings.Problems() is { Count: > 0 } problems ? (null, string.Join(" ", problems)) : (settings, null);
    }
}
