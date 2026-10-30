using System.Text.RegularExpressions;

namespace SEAITT_LMS.Components.Shared;

/// <summary>
/// A file name as a list shows it. A long name (Module 1 - Introduction to Accounting Principles and Concepts.pdf) is shortened to the module it belongs to
/// (Module 1); a long name that says nothing about a module is cut with an ellipsis. A short name stays as it is. The full name is kept for the tooltip.
/// </summary>
public static partial class FileLabel
{
    /// <summary>A name with more letters than this (the extension does not count) is shortened.</summary>
    public const int LongName = 24;

    [GeneratedRegex(@"^\s*(Module|Lesson|Week|Unit|Chapter)\s*[-_#]?\s*(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex LeadingNumber();

    /// <summary>The short name of a file. <paramref name="moduleLabel"/> is the module the file belongs to ("Module 1"), when the page knows it; <paramref name="n"/> is the place of the file (1, 2 ...) so two long names of one module stay apart.</summary>
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

    /// <summary>The "Module 1" of a module title like "Module 1: Basic bookkeeping", or null when the title does not start that way.</summary>
    public static string? OfModule(string? title) =>
        title is not null && LeadingNumber().Match(title) is { Success: true } m ? $"{char.ToUpperInvariant(m.Groups[1].Value[0])}{m.Groups[1].Value[1..].ToLowerInvariant()} {m.Groups[2].Value}" : null;
}
