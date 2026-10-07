namespace SEAITT_LMS.Data.Entities;

public enum Sex
{
    Male,
    Female,
}

/// <summary>Whether a staff member is currently working at the school.</summary>
public enum StaffStatus
{
    Active,
    Inactive,
}

/// <summary>
/// Who a person is. Every role has its own table (students, teachers, administrators, ...) with these columns, one row
/// per user account (user_id is unique). This class is not a table itself, it is the shared shape of those tables.
/// </summary>
public abstract class PersonProfile : AuditableEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public string FirstName { get; set; } = "";
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = "";

    /// <summary>Jr., III, ...</summary>
    public string? NameExtension { get; set; }

    public string FullName
    {
        get
        {
            var parts = new[] { FirstName, MiddleName, LastName, NameExtension };
            return string.Join(' ', parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        }
    }
}

/// <summary>
/// Where a student is, or what a teacher mainly handles: grade level, track, strand, subject and section.
/// Every value is optional. Together they are a quick summary kept on the person's row: for a student the full
/// record is their enrollments (term by term).
/// The database makes sure the values agree with each other: a section's strand and grade level must be the section's
/// own, and a strand's track must be the strand's own.
/// </summary>
public interface IHasPlacement
{
    /// <summary>11 or 12.</summary>
    byte? GradeLevel { get; set; }

    int? TrackId { get; set; }
    int? StrandId { get; set; }

    /// <summary>A student's main subject, or the subject a teacher mainly teaches.</summary>
    int? SubjectId { get; set; }

    /// <summary>A student's section, or the section a teacher mainly handles.</summary>
    int? SectionId { get; set; }
}

public class Student : PersonProfile, IHasPlacement
{
    /// <summary>Learner Reference Number, exactly 12 digits. Unique.</summary>
    public string Lrn { get; set; } = "";

    public DateOnly? BirthDate { get; set; }
    public Sex? Sex { get; set; }

    public byte? GradeLevel { get; set; }
    public int? TrackId { get; set; }
    public int? StrandId { get; set; }
    public int? SubjectId { get; set; }
    public int? SectionId { get; set; }

    /// <summary>One of <see cref="StudentStatus"/>. A graduated student is archived.</summary>
    public string Status { get; set; } = StudentStatus.Active;

    public DateTime? StatusChangedAtUtc { get; set; }

    /// <summary>Why: "Graduated with the class of 2027" ...</summary>
    public string? StatusNote { get; set; }
}

/// <summary>Shared shape of the staff tables (teachers, registrars, guidance counselors, administrators).</summary>
public abstract class StaffProfile : PersonProfile
{
    /// <summary>The school's employee/ID number. Optional, unique when present.</summary>
    public string? EmployeeNumber { get; set; }
}

/// <summary>Administrators, guidance counselors and registrars also keep a contact number and a status.</summary>
public abstract class StaffContactProfile : StaffProfile
{
    /// <summary>The person's contact number (E.164). Separate from the sign-in number in users.phone_number.</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>Active or Inactive. Setting it to Inactive should also deactivate the login (users.is_active).</summary>
    public StaffStatus Status { get; set; } = StaffStatus.Active;
}

public class Teacher : StaffProfile, IHasPlacement
{
    public byte? GradeLevel { get; set; }
    public int? TrackId { get; set; }
    public int? StrandId { get; set; }
    public int? SubjectId { get; set; }
    public int? SectionId { get; set; }
}

public class Registrar : StaffContactProfile;

public class GuidanceCounselor : StaffContactProfile;

public class Administrator : StaffContactProfile;
