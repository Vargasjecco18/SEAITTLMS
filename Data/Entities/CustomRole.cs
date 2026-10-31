namespace SEAITT_LMS.Data.Entities;

/// <summary>
/// A role the administrator named in Add staff, like "Librarian". The people who have it sign in to the pages of one of the staff roles (<see cref="BaseRole"/>, the academic provider for the roles made now)
/// and get the same permissions, and the system shows them under the name of the custom role. Unlike the registrar, the guidance counselor and the academic coordinator, a custom role can have
/// as many people as the school needs.
/// </summary>
public class CustomRole : AuditableEntity
{
    public string Name { get; set; } = "";

    /// <summary>The staff role a new custom role is made on: the account needs one of the real roles to have pages to sign in to. The administrator does not choose it.</summary>
    public const string DefaultBaseRole = AppRoles.AcademicProvider;

    /// <summary>One of the staff roles in <see cref="AppRoles"/> (Registrar, GuidanceCounselor, AcademicCoordinator or AcademicProvider).</summary>
    public string BaseRole { get; set; } = DefaultBaseRole;
}
