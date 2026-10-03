namespace SEAITT_LMS.Data.Entities;

public enum Sex
{
    Male,
    Female,
}

/// <summary>Each role has its own profile table, one row per user account (user_id is unique).</summary>
public class Student : AuditableEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Learner Reference Number, exactly 12 digits. Unique.</summary>
    public string Lrn { get; set; } = "";

    public DateOnly? BirthDate { get; set; }
    public Sex? Sex { get; set; }
}

/// <summary>
/// Shared shape of the staff profile tables (teachers, registrars, guidance counselors, administrators).
/// Not an EF entity itself, every staff table is mapped separately.
/// </summary>
public abstract class StaffProfile : AuditableEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>The school's employee/ID number. Optional, unique when present.</summary>
    public string? EmployeeNumber { get; set; }
}

public class Teacher : StaffProfile;

public class Registrar : StaffProfile;

public class GuidanceCounselor : StaffProfile;

public class Administrator : StaffProfile;
