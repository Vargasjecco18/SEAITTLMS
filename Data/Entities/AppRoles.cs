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
    public const string AcademicCoordinator = "AcademicCoordinator";
    public const string AcademicProvider = "AcademicProvider";

    public const int AdminId = 1;
    public const int TeacherId = 2;
    public const int StudentId = 3;
    public const int RegistrarId = 4;
    public const int GuidanceCounselorId = 5;
    public const int AcademicCoordinatorId = 6;
    public const int AcademicProviderId = 7;

    public static readonly IReadOnlyDictionary<string, int> Ids = new Dictionary<string, int>
    {
        [Admin] = AdminId,
        [Teacher] = TeacherId,
        [Student] = StudentId,
        [Registrar] = RegistrarId,
        [GuidanceCounselor] = GuidanceCounselorId,
        [AcademicCoordinator] = AcademicCoordinatorId,
        [AcademicProvider] = AcademicProviderId,
    };

    /// <summary>The name to show on screen ("Guidance Counselor" instead of "GuidanceCounselor").</summary>
    public static string DisplayName(string? role) => role switch
    {
        GuidanceCounselor => "Guidance Counselor",
        AcademicCoordinator => "Academic Coordinator",
        AcademicProvider => "Academic Provider",
        _ => role ?? "",
    };

    /// <summary>The school has exactly one administrator, one registrar, one guidance counselor, one academic coordinator and one academic provider, so no more can be added.</summary>
    public static bool OnlyOneAllowed(string? role) => role is Admin or Registrar or GuidanceCounselor or AcademicCoordinator or AcademicProvider;
}
