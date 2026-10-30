using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Components.Shared;

/// <summary>Small helpers for how subjects, modules and their items look.</summary>
public static class SubjectStyle
{
    /// <summary>A color 0 to 7 for a subject's card, always the same one for the same subject, so it is easy to recognize.</summary>
    public static int ColorIndex(string name)
    {
        var hash = 17;
        foreach (var c in name)
        {
            hash = unchecked(hash * 31 + c);
        }

        return Math.Abs(hash % 8);
    }

    public static string KindIcon(string kind) => kind switch
    {
        ItemKinds.Quiz => "file-question",
        ItemKinds.TaskPerformance => "clipboard-check",
        ItemKinds.Seatwork => "clipboard-pen",
        ItemKinds.Recitation => "message-square-text",
        ItemKinds.Exam => "clipboard-list",
        _ => "notebook-pen",
    };

    /// <summary>The words for what a student sees of their own work: not started, handed in, graded ...</summary>
    public static (string Text, Tone Tone) StudentStatus(string status, bool isLate, bool isOverdue, bool isQuiz, bool scoreHidden, string? lateDecision = null) => status switch
    {
        SubmissionStatus.Graded when isQuiz && scoreHidden => ("Handed in", Tone.Info),
        SubmissionStatus.Graded => ("Graded", Tone.Success),
        SubmissionStatus.Submitted when isLate && lateDecision == LateDecisions.Pending => ("Late · waiting for your teacher", Tone.Warning),
        SubmissionStatus.Submitted when isLate && lateDecision == LateDecisions.Declined => ("Late · not accepted", Tone.Danger),
        SubmissionStatus.Submitted => (isLate ? "Handed in late" : "Handed in", Tone.Info),
        SubmissionStatus.InProgress => ("In progress", Tone.Warning),
        _ => isOverdue ? ("Missing", Tone.Danger) : ("To do", Tone.Neutral),
    };

    public static string Percent(decimal score, decimal max) => max <= 0 ? "" : $"{score / max * 100:0.#}%";
}
