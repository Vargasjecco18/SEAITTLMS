namespace SEAITT_LMS.Auth;

public static class AppClaimTypes
{
    public const string SecurityStamp = "seaitt:security_stamp";

    /// <summary>The number of the sign-in (see <see cref="Data.Entities.User.ActiveSessionId"/>).</summary>
    public const string SessionId = "seaitt:session";
}
