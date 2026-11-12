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

    /// <summary>The name of the role the administrator chose in Add staff (a role of their own that works like <see cref="Role"/>). Empty for the usual roles.</summary>
    public int? CustomRoleId { get; set; }
    public CustomRole? CustomRole { get; set; }

    /// <summary>Deactivated accounts cannot sign in. Prefer this over deleting a user.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Changes whenever the account must be signed out everywhere (password change, deactivation, ...).</summary>
    public string SecurityStamp { get; set; } = "";

    /// <summary>
    /// The number of the sign-in that is open now. An account has one sign-in open at a time (<see cref="Auth.AuthOptions.SingleSession"/>): while another browser or device is using it, a new sign-in is refused.
    /// Empty: nobody is signed in with this account.
    /// </summary>
    public string? ActiveSessionId { get; set; }

    /// <summary>The last time the open sign-in was seen (every page that is open says so now and then). After a few minutes without it the sign-in is taken as ended.</summary>
    public DateTime? ActiveSessionSeenUtc { get; set; }

    /// <summary>
    /// The sound of a new message or notification the person chose in Settings: "chime", "bell", "pop", "knock", "tones", "school" (the sound the administrator put) or "custom" (the file the person
    /// added). Empty: the sound of the school when there is one, else two short tones. See <see cref="NotificationSound"/>.
    /// </summary>
    public string? NotificationSoundChoice { get; set; }

    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }

    // The profile row of this user. Only the one that matches the role exists, and it is only filled in when the
    // query uses Include.
    public Administrator? Administrator { get; set; }
    public GuidanceCounselor? GuidanceCounselor { get; set; }
    public AcademicCoordinator? AcademicCoordinator { get; set; }
    public AcademicProvider? AcademicProvider { get; set; }
    public Registrar? Registrar { get; set; }
    public Teacher? Teacher { get; set; }
    public Student? Student { get; set; }

    public PersonProfile? Profile
    {
        get
        {
            PersonProfile?[] all = [Administrator, GuidanceCounselor, AcademicCoordinator, AcademicProvider, Registrar, Teacher, Student];
            return all.FirstOrDefault(p => p is not null);
        }
    }

    /// <summary>The person's full name for screens, or the email when the profile was not loaded.</summary>
    public string DisplayName => Profile?.FullName ?? Email;
}
