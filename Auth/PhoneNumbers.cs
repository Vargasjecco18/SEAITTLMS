using System.Text.RegularExpressions;

namespace SEAITT_LMS.Auth;

/// <summary>
/// Stores phone numbers in E.164 (+639171234567) so they can be used for SMS two-factor codes later.
/// Philippine numbers may be typed as 09171234567, 9171234567, 639171234567 or +639171234567.
/// </summary>
public static partial class PhoneNumbers
{
    [GeneratedRegex(@"^\+[1-9]\d{7,14}$")]
    private static partial Regex E164();

    /// <summary>Returns the E.164 number, or null when the input can't be understood.</summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var cleaned = new string(input.Where(c => !char.IsWhiteSpace(c) && c is not ('-' or '(' or ')' or '.')).ToArray());

        if (cleaned.StartsWith("09", StringComparison.Ordinal) && cleaned.Length == 11 && cleaned.All(char.IsAsciiDigit))
        {
            cleaned = "+63" + cleaned[1..];
        }
        else if (cleaned.StartsWith("639", StringComparison.Ordinal) && cleaned.Length == 12 && cleaned.All(char.IsAsciiDigit))
        {
            cleaned = "+" + cleaned;
        }
        else if (cleaned.StartsWith('9') && cleaned.Length == 10 && cleaned.All(char.IsAsciiDigit))
        {
            cleaned = "+63" + cleaned;
        }

        return E164().IsMatch(cleaned) ? cleaned : null;
    }
}
