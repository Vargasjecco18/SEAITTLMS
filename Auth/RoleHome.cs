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

    /// <summary>Anybody else (a role without a page of its own) lands on their account page.</summary>
    public const string Account = "/account";

    public static string For(string? role) => role switch
    {
        AppRoles.Admin => Admin,
        AppRoles.Registrar => Registrar,
        AppRoles.Teacher => Teacher,
        AppRoles.Student => Student,
        AppRoles.GuidanceCounselor => Guidance,
        _ => Account,
    };
}
