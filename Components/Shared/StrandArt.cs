namespace SEAITT_LMS.Components.Shared;

/// <summary>
/// The picture on the top of a course card: one photo for each strand (the files are in <c>wwwroot/images/strands</c>, one .jpg for each strand). A strand with no picture
/// (a new one the administrator added) has none, and its cards keep the colored banner.
/// </summary>
public static class StrandArt
{
    // The code of the strand is the first way in; the name is the second, in case the administrator changed the code.
    private static readonly (string Code, Func<string, bool> NameMatches, string File)[] Pictures =
    [
        ("BUSSENT", n => n.Contains("business"), "business-entrepreneurship"),
        ("EDUC", n => n.Contains("education"), "education"),
        ("ENG", n => n.Contains("engineering"), "engineering"),
        ("MED", n => n.Contains("medical"), "medical-health-sciences"),
        ("CD1", n => n.Contains("video game") || n.Contains("computer system"), "css-vgd"),
        ("HT1", n => n.StartsWith("kitchen operations"), "ko-fbs"),
        ("HT2", n => n.StartsWith("food and beverage"), "fbs-ko"),
        ("USP", n => n.Contains("university studies"), "university-studies-prep"),
    ];

    /// <summary>The address of the picture of the strand (relative to the site), or null when it has none.</summary>
    public static string? For(string? code, string? name)
    {
        var trimmedCode = code?.Trim();
        foreach (var (c, _, file) in Pictures)
        {
            if (string.Equals(c, trimmedCode, StringComparison.OrdinalIgnoreCase))
            {
                return $"images/strands/{file}.jpg";
            }
        }

        var lower = name?.Trim().ToLowerInvariant() ?? "";
        foreach (var (_, matches, file) in Pictures)
        {
            if (lower.Length > 0 && matches(lower))
            {
                return $"images/strands/{file}.jpg";
            }
        }

        return null;
    }
}
