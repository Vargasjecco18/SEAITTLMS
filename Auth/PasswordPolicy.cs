namespace SEAITT_LMS.Auth;

/// <summary>Length rules only (as recommended by NIST SP 800-63B), no "must contain a symbol" rules.</summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;

    /// <summary>Upper bound so nobody can post a megabyte-long "password".</summary>
    public const int MaxLength = 128;

    /// <summary>Returns an error message, or null when the password is acceptable.</summary>
    public static string? Validate(string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return "Password is required.";
        }

        if (password.Length < MinLength)
        {
            return $"Password must be at least {MinLength} characters.";
        }

        if (password.Length > MaxLength)
        {
            return $"Password must be at most {MaxLength} characters.";
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            return "Password can't be only spaces.";
        }

        return null;
    }
}
