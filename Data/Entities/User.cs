namespace SEAITT_LMS.Data.Entities;

/// <summary>
/// The login account: email, password and sign-in security. Who the person is (name, LRN, employee number, ...) lives in
/// the profile table of their role (students, teachers, administrators, ...). Every user has exactly one profile row.
/// </summary>
public class User : AuditableEntity
{
    /// <summary>Always stored trimmed and lower-case. Unique.</summary>
    public string Email { get; set; } = "";

    /// <summary>PBKDF2 hash in the format written by <see cref="Auth.PasswordHasher"/>. Never the password.</summary>
    public string PasswordHash { get; set; } = "";

    /// <summary>
    /// E.164 (+639XXXXXXXXX). The number for the future two-factor sign-in, not used for login yet.
    /// Administrators, guidance counselors and registrars also have their own contact number in their profile table.
    /// </summary>
    public string? PhoneNumber { get; set; }

    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;

    /// <summary>Deactivated accounts cannot sign in. Prefer this over deleting a user.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Changes whenever the account must be signed out everywhere (password change, deactivation, ...).</summary>
    public string SecurityStamp { get; set; } = "";

    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }

    // The profile row of this user. Only the one that matches the role exists, and it is only filled in when the
    // query uses Include.
    public Administrator? Administrator { get; set; }
    public GuidanceCounselor? GuidanceCounselor { get; set; }
    public Registrar? Registrar { get; set; }
    public Teacher? Teacher { get; set; }
    public Student? Student { get; set; }

    public PersonProfile? Profile
    {
        get
        {
            PersonProfile?[] all = [Administrator, GuidanceCounselor, Registrar, Teacher, Student];
            return all.FirstOrDefault(p => p is not null);
        }
    }

    /// <summary>The person's full name for screens, or the email when the profile was not loaded.</summary>
    public string DisplayName => Profile?.FullName ?? Email;
}
