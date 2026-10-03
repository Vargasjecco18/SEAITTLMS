namespace SEAITT_LMS.Data.Entities;

/// <summary>
/// The login account. Everything a person needs to sign in lives here; role specific details
/// (LRN, employee number, ...) live in the separate profile tables (Student, Teacher, ...).
/// </summary>
public class User : AuditableEntity
{
    /// <summary>Always stored trimmed and lower-case. Unique.</summary>
    public string Email { get; set; } = "";

    /// <summary>PBKDF2 hash in the format written by <see cref="Auth.PasswordHasher"/>. Never the password.</summary>
    public string PasswordHash { get; set; } = "";

    /// <summary>E.164 (+639XXXXXXXXX). Kept for the future two-factor feature, not used for login yet.</summary>
    public string? PhoneNumber { get; set; }

    public string FirstName { get; set; } = "";
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = "";

    /// <summary>Jr., III, ...</summary>
    public string? NameExtension { get; set; }

    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;

    /// <summary>Deactivated accounts cannot sign in. Prefer this over deleting a user.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Changes whenever the account must be signed out everywhere (password change, deactivation, ...).</summary>
    public string SecurityStamp { get; set; } = "";

    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }

    public string FullName
    {
        get
        {
            var parts = new[] { FirstName, MiddleName, LastName, NameExtension };
            return string.Join(' ', parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        }
    }
}
