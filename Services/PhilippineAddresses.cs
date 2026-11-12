namespace SEAITT_LMS.Services;

/// <summary>The choices of the lists that end with <see cref="Others"/> (the suffix of a name): the person picks one, or picks Others and types another one.</summary>
public static class PhilippineAddresses
{
    /// <summary>The last choice of such a list: a box opens to type what is not in the list.</summary>
    public const string Others = "Others";

    public static readonly string[] Suffixes = ["Jr.", "Sr.", "I", "II", "III", "IV", "V"];

    public static IReadOnlyList<string> SuffixChoices() => [.. Suffixes, Others];
}
