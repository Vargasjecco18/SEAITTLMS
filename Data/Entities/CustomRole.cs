namespace SEAITT_LMS.Data.Entities;

/// <summary>
/// A role the administrator named in Add staff, like "Assistant Registrar". It works like one of the staff roles (<see cref="BaseRole"/>): the people who have it sign in to the pages of that
/// role and get the same permissions, and the system shows them under the name of the custom role. Unlike the registrar, the guidance counselor and the academic coordinator, a custom role can have
/// as many people as the school needs.
/// </summary>
public class CustomRole : AuditableEntity
{
    public string Name { get; set; } = "";

    /// <summary>One of the staff roles in <see cref="AppRoles"/> (Registrar, GuidanceCounselor, AcademicCoordinator or AcademicProvider).</summary>
    public string BaseRole { get; set; } = AppRoles.AcademicProvider;
}
