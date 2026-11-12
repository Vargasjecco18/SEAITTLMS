namespace SEAITT_LMS.Auth;

public static class AppClaimTypes
{
    public const string SecurityStamp = "seaitt:security_stamp";

    /// <summary>The number of the sign-in (see <see cref="Data.Entities.User.ActiveSessionId"/>).</summary>
    public const string SessionId = "seaitt:session";

    /// <summary>The name of the role the administrator made up (see <see cref="Data.Entities.CustomRole"/>), shown instead of the usual name of the role. Only on the accounts that have one.</summary>
    public const string RoleTitle = "seaitt:role_title";

    /// <summary>The pages ticked for the role the administrator made up (the keys of <c>StaffPages</c> separated by commas). Only on the accounts that have such a role.</summary>
    public const string Pages = "seaitt:pages";
}
