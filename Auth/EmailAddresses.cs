using System.Net.Mail;

namespace SEAITT_LMS.Auth;

public static class EmailAddresses
{
    public const int MaxLength = 254;

    /// <summary>Trimmed, lower-case email, or null when it isn't a plausible single email address.</summary>
    public static string? Normalize(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var value = email.Trim().ToLowerInvariant();
        if (value.Length > MaxLength)
        {
            return null;
        }

        // MailAddress also accepts "Name <a@b.c>", so require that it parses back to exactly the input.
        return MailAddress.TryCreate(value, out var parsed) && parsed.Address == value && value.Contains('@')
            ? value
            : null;
    }
}
