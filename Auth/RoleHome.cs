using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Auth;

/// <summary>Where each role lands after signing in. The first page, "/", sends signed-in users here.</summary>
public static class RoleHome
{
    public const string Admin = "/admin";
    public const string Registrar = "/registrar";

    /// <summary>Teachers, students and guidance counselors have no dashboard yet, so they land on their account page.</summary>
    public const string Account = "/account";

    public static string For(string? role) => role switch
    {
        AppRoles.Admin => Admin,
        AppRoles.Registrar => Registrar,
        _ => Account,
    };
}
