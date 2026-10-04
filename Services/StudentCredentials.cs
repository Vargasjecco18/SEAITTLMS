using System.Globalization;
using System.Text;
using SEAITT_LMS.Auth;

namespace SEAITT_LMS.Services;

/// <summary>The sign-in details made for a newly enrolled student.</summary>
public sealed record StudentLogin(string Email, string Password);

/// <summary>
/// The automatic email and password of a new student, made from the last name and the end of the LRN:
/// last name Vargas and LRN 111111111111 give vargas.111111@legazpi.seaitt.edu.ph and the password vargas111111.
///
/// The last name is written in lower-case letters and digits only (spaces, dots, dashes and accents are dropped:
/// "Dela Cruz" becomes delacruz, "Peñaflor" becomes penaflor). Normally the last 6 digits of the LRN are used. When a
/// password made this way would be too short, or the email already belongs to someone else, more digits are used
/// (up to all 12), for the email and the password alike, so the two always match.
/// </summary>
public static class StudentCredentials
{
    public const int DefaultDigits = 6;
    public const int MaxDigits = 12;

    // An email address has room for 64 characters before the @, and the digits need 7 of them.
    private const int MaxNameLength = 30;

    public static StudentLogin Create(string? lastName, string lrn, string emailDomain, int digits = DefaultDigits)
    {
        var name = Slug(lastName);
        var count = Math.Clamp(digits, DefaultDigits, Math.Min(MaxDigits, lrn.Length));

        // A one-letter last name would give a 7-character password: use one more digit instead of failing the policy.
        while (name.Length + count < PasswordPolicy.MinLength && count < Math.Min(MaxDigits, lrn.Length))
        {
            count++;
        }

        var tail = lrn[^count..];
        return new StudentLogin($"{name}.{tail}@{emailDomain}", name + tail);
    }

    /// <summary>Lower-case letters a-z and digits only, at most 30 of them; "student" when nothing is left.</summary>
    public static string Slug(string? text)
    {
        var builder = new StringBuilder();
        foreach (var ch in (text ?? "").Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue; // the accent of é, ñ, ...
            }

            var lower = char.ToLowerInvariant(ch);
            if (lower is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                builder.Append(lower);
            }
        }

        var slug = builder.Length > MaxNameLength ? builder.ToString(0, MaxNameLength) : builder.ToString();
        return slug.Length == 0 ? "student" : slug;
    }
}
