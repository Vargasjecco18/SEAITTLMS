using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Auth;

/// <summary>Where each role lands after signing in. The first page, "/", sends signed-in users here.</summary>
public static class RoleHome
{
    public const string Admin = "/admin";
    public const string Registrar = "/registrar";
    public const string Teacher = "/teacher";
    public const string Student = "/student";
    public const string Guidance = "/guidance";
    public const string Coordinator = "/coordinator";
    public const string Provider = "/provider";
    public const string Admission = "/admission";

    /// <summary>The first page of a person who has a role of their own that the administrator made (a role with the pages that were ticked).</summary>
    public const string Staff = "/staff";

    /// <summary>Anybody else (a role without a page of its own) has nowhere to go.</summary>
    public const string NoPage = "/access-denied";

    public static string For(string? role) => role switch
    {
        AppRoles.Admin => Admin,
        AppRoles.Registrar => Registrar,
        AppRoles.Teacher => Teacher,
        AppRoles.Student => Student,
        AppRoles.GuidanceCounselor => Guidance,
        AppRoles.AcademicCoordinator => Coordinator,
        AppRoles.AcademicProvider => Provider,
        AppRoles.Admission => Admission,
        _ => NoPage,
    };
}
