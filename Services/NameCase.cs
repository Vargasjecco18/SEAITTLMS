using System.Text;

namespace SEAITT_LMS.Services;

/// <summary>
/// Writes a name with capital letters the way a person would: "juan dela cruz" and "JUAN DELA CRUZ" both become "Juan Dela Cruz". A word that already has
/// both small and capital letters ("McDonald", "DeLeon") is left as it was typed, because the person meant it. Roman numerals (II, III) stay capital and
/// the name extensions are written like Jr. and Sr.
/// </summary>
public static class NameCase
{
    public static string Title(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(' ', words.Select(Word));
    }

    private static string Word(string word)
    {
        // "Jr", "SR." ... and the numerals of a name: II, III, IV
        var bare = word.TrimEnd('.');
        if (bare.Equals("jr", StringComparison.OrdinalIgnoreCase) || bare.Equals("sr", StringComparison.OrdinalIgnoreCase))
        {
            return char.ToUpperInvariant(bare[0]) + bare[1..].ToLowerInvariant() + word[bare.Length..];
        }

        if (IsRoman(bare))
        {
            return bare.ToUpperInvariant() + word[bare.Length..];
        }

        // Pieces joined by a dash or an apostrophe are words of their own: "anne-marie" -> "Anne-Marie", "o'neil" -> "O'Neil".
        var builder = new StringBuilder(word.Length);
        var piece = new StringBuilder();
        void Flush()
        {
            if (piece.Length > 0)
            {
                builder.Append(Piece(piece.ToString()));
                piece.Clear();
            }
        }

        foreach (var ch in word)
        {
            if (ch is '-' or '\'' or '’')
            {
                Flush();
                builder.Append(ch);
            }
            else
            {
                piece.Append(ch);
            }
        }

        Flush();
        return builder.ToString();
    }

    private static string Piece(string piece)
    {
        var letters = piece.Where(char.IsLetter).ToList();
        if (letters.Count == 0)
        {
            return piece;
        }

        // Both kinds of letters in one piece (McDonald, DeLeon): leave it.
        var hasLower = letters.Any(char.IsLower);
        var hasUpper = letters.Any(char.IsUpper);
        if (hasLower && hasUpper)
        {
            return piece;
        }

        var lower = piece.ToLowerInvariant();
        var first = lower.ToList().FindIndex(char.IsLetter);
        return lower[..first] + char.ToUpperInvariant(lower[first]) + lower[(first + 1)..];
    }

    private static readonly HashSet<string> Numerals = new(StringComparer.OrdinalIgnoreCase) { "II", "III", "IV", "VI", "VII", "VIII", "IX" };

    private static bool IsRoman(string text) => Numerals.Contains(text) && (text.All(char.IsUpper) || text.All(char.IsLower));
}
