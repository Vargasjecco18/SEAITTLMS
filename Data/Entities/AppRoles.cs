namespace SEAITT_LMS.Data.Entities;

/// <summary>
/// The fixed set of roles. The names are what end up in the login cookie and in
/// [Authorize(Roles = ...)], the ids are what is stored in users.role_id.
/// </summary>
public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Teacher = "Teacher";
    public const string Student = "Student";
    public const string Registrar = "Registrar";
    public const string GuidanceCounselor = "GuidanceCounselor";

    public const int AdminId = 1;
    public const int TeacherId = 2;
    public const int StudentId = 3;
    public const int RegistrarId = 4;
    public const int GuidanceCounselorId = 5;

    public static readonly IReadOnlyDictionary<string, int> Ids = new Dictionary<string, int>
    {
        [Admin] = AdminId,
        [Teacher] = TeacherId,
        [Student] = StudentId,
        [Registrar] = RegistrarId,
        [GuidanceCounselor] = GuidanceCounselorId,
    };

    /// <summary>The name to show on screen ("Guidance Counselor" instead of "GuidanceCounselor").</summary>
    public static string DisplayName(string? role) => role == GuidanceCounselor ? "Guidance Counselor" : role ?? "";
}
