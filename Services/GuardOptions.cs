namespace SEAITT_LMS.Services;

/// <summary>
/// Round 46: the extra anti-cheating settings of a quiz or a summative exam, kept as a short list of names in one column (<c>guard_flags</c>) of the item. The watch itself (leaving the tab,
/// copy, paste, print, screenshot, the warnings allowed) is the old switch; these are the new ones that go with it (full screen, one question at a time, no going back).
/// </summary>
public static class GuardOptions
{
    /// <summary>The quiz is taken in full screen: it starts with a button, and leaving full screen is a warning.</summary>
    public const string Fullscreen = "fullscreen";

    /// <summary>One question on the screen at a time, with Previous and Next.</summary>
    public const string OneAtATime = "one";

    /// <summary>Only forward: a student can't go back to a question that was left (needs one question at a time).</summary>
    public const string NoBack = "noback";

    public static readonly (string Key, string Title, string Hint)[] All =
    [
        (Fullscreen, "Full screen only", "The quiz starts in full screen. Leaving full screen is a warning."),
        (OneAtATime, "One question at a time", "The student sees one question on the screen, with Previous and Next."),
        (NoBack, "No going back", "A question that was left can't be opened again (needs one question at a time)."),
    ];

    public static bool Has(string? flags, string key) => (flags ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains(key);

    public static string With(string? flags, string key, bool on)
    {
        var list = (flags ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(f => f != key).ToList();
        if (on)
        {
            list.Add(key);
        }

        return Clean(string.Join(',', list));
    }

    /// <summary>Only the names that exist, each once; "no going back" needs "one question at a time".</summary>
    public static string Clean(string? flags)
    {
        var known = All.Select(a => a.Key).ToHashSet();
        var list = (flags ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(known.Contains).Distinct().ToList();
        if (!list.Contains(OneAtATime))
        {
            list.Remove(NoBack);
        }

        return string.Join(',', All.Select(a => a.Key).Where(list.Contains));
    }
}
