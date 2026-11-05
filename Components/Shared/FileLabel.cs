using System.Text.RegularExpressions;

namespace SEAITT_LMS.Components.Shared;

/// <summary>
/// A file name as a list shows it. A long name (Handout 1 - Introduction to Accounting Principles and Concepts.pdf) is shortened to the handout it belongs to
/// (Handout 1); a long name that says nothing about a handout is cut with an ellipsis. A short name stays as it is. The full name is kept for the tooltip.
/// </summary>
public static partial class FileLabel
{
    /// <summary>A name with more letters than this (the extension does not count) is shortened.</summary>
    public const int LongName = 24;

    [GeneratedRegex(@"^\s*(Handout|Module|Lesson|Week|Unit|Chapter)\s*[-_#]?\s*(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex LeadingNumber();

    /// <summary>The short name of a file. <paramref name="moduleLabel"/> is the handout the file belongs to ("Handout 1"), when the page knows it; <paramref name="n"/> is the place of the file (1, 2 ...) so two long names of one handout stay apart.</summary>
    public static string Short(string name, string? moduleLabel = null, int n = 1)
    {
        var stem = Path.GetFileNameWithoutExtension(name);
        if (stem.Length <= LongName)
        {
            return name;
        }

        var label = LeadingNumber().Match(name) is { Success: true } m ? $"{char.ToUpperInvariant(m.Groups[1].Value[0])}{m.Groups[1].Value[1..].ToLowerInvariant()} {m.Groups[2].Value}" : moduleLabel;
        if (string.IsNullOrWhiteSpace(label))
        {
            return $"{stem[..(LongName - 3)].TrimEnd()}…{Path.GetExtension(name)}";
        }

        return n <= 1 ? label : $"{label} ({n})";
    }

    /// <summary>The "Handout 1" of a handout title like "Handout 1: Basic bookkeeping", or null when the title does not start that way.</summary>
    public static string? OfModule(string? title) =>
        title is not null && LeadingNumber().Match(title) is { Success: true } m ? $"{char.ToUpperInvariant(m.Groups[1].Value[0])}{m.Groups[1].Value[1..].ToLowerInvariant()} {m.Groups[2].Value}" : null;
}
