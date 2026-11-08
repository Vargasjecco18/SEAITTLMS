using SEAITT_LMS.Auth;

namespace SEAITT_LMS.Services;

/// <summary>
/// The automatic email and password of a new teacher or member of the staff, made like the one of a student but from the employee number: last name Villanueva and employee number
/// T-001 give villanueva.001@legazpi.seaitt.edu.ph and the password villanueva001. The digits of the employee number are the ones used; when the password would be too short for the password
/// rule, zeros are put in front of the digits.
/// </summary>
public static class StaffCredentials
{
    /// <summary>The digits of an employee number (T-001 gives 001), empty when it has none.</summary>
    public static string DigitsOf(string? employeeNumber) => new((employeeNumber ?? "").Where(char.IsAsciiDigit).Take(12).ToArray());

    /// <summary>The email and the password, or null when the employee number has no digit to make them from.</summary>
    public static StudentLogin? Create(string? lastName, string? employeeNumber, string emailDomain)
    {
        var digits = DigitsOf(employeeNumber);
        if (digits.Length == 0)
        {
            return null;
        }

        var name = StudentCredentials.Slug(lastName);
        while (name.Length + digits.Length < PasswordPolicy.MinLength && digits.Length < 12)
        {
            digits = "0" + digits;
        }

        return new StudentLogin($"{name}.{digits}@{emailDomain}", name + digits);
    }
}
