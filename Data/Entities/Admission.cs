namespace SEAITT_LMS.Data.Entities;

/// <summary>
/// The admission staff: collects the applications of the students who want to study at the school. An admission officer works for one grade level (11 or 12): they see the class sections and
/// the students of that grade level only. (The applications of the public form are for grade 11.)
/// </summary>
public class AdmissionOfficer : StaffContactProfile
{
    /// <summary>The grade level this officer works for: 11 or 12.</summary>
    public byte GradeLevel { get; set; } = 11;
}

/// <summary>Where an application is.</summary>
public static class ApplicationStates
{
    /// <summary>Sent by the student; the admission office has not decided yet.</summary>
    public const string Pending = "Pending";

    /// <summary>The admission office accepted it: the registrar sees the student under Accepted students and can enroll them.</summary>
    public const string Accepted = "Accepted";

    public const string Declined = "Declined";

    /// <summary>The registrar enrolled the student.</summary>
    public const string Enrolled = "Enrolled";

    public static readonly string[] All = [Pending, Accepted, Declined, Enrolled];

    public static string Display(string state) => state switch
    {
        Accepted => "Accepted",
        Declined => "Declined",
        Enrolled => "Enrolled",
        _ => "Pending",
    };
}

/// <summary>The papers a student uploads with the application.</summary>
public static class ApplicationFileKinds
{
    public const string Psa = "Psa";
    public const string ReportCard = "ReportCard";
    public const string GoodMoral = "GoodMoral";
    public const string Completion = "Completion";

    public static readonly string[] All = [Psa, ReportCard, GoodMoral, Completion];

    public static string Display(string kind) => kind switch
    {
        Psa => "PSA Birth Certificate",
        ReportCard => "Grade 10 Report Card (Form 138 / SF9)",
        GoodMoral => "Certificate of Good Moral Character",
        Completion => "Certificate of Completion",
        _ => kind,
    };
}

/// <summary>
/// An application to study at the school, sent by a student from the public home page. Grade 11 only. The admission office accepts or declines it; the registrar sees the accepted ones and
/// enrolls them (the form of the enrollment can be filled from it).
/// </summary>
public class StudentApplication : AuditableEntity
{
    /// <summary>What the student can quote at the school: APP-2026-0001. Unique.</summary>
    public string Reference { get; set; } = "";

    // ---- 1. Personal information
    public string FirstName { get; set; } = "";
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = "";
    public string? Suffix { get; set; }
    public DateOnly BirthDate { get; set; }

    /// <summary>"Male" or "Female".</summary>
    public string Sex { get; set; } = "";
    public string CivilStatus { get; set; } = "";
    public string Nationality { get; set; } = "";
    public string Religion { get; set; } = "";

    /// <summary>The 12-digit LRN, when the student has it already (the registrar needs it to enroll).</summary>
    public string? Lrn { get; set; }

    // ---- 2. Contact information
    public string Email { get; set; } = "";
    public string MobileNumber { get; set; } = "";
    public string Address { get; set; } = "";
    public string Province { get; set; } = "";
    public string City { get; set; } = "";
    public string Barangay { get; set; } = "";
    public string ZipCode { get; set; } = "";

    // ---- 3. Academic information
    public byte GradeLevel { get; set; } = 11;
    public string AcademicYear { get; set; } = "";

    /// <summary>The term the student applies for (1 for the 1st term).</summary>
    public byte TermNumber { get; set; } = 1;

    public int TrackId { get; set; }
    public Track Track { get; set; } = null!;
    public int StrandId { get; set; }
    public Strand Strand { get; set; } = null!;

    // ---- 4. Parent / guardian information
    public string? FatherName { get; set; }
    public string? FatherContact { get; set; }
    public string? MotherName { get; set; }
    public string? MotherContact { get; set; }
    public string GuardianName { get; set; } = "";
    public string GuardianRelationship { get; set; } = "";
    public string GuardianContact { get; set; } = "";
    public string GuardianAddress { get; set; } = "";

    /// <summary>The day and hour the student chose to come to the school to confirm the application. The admission office is told.</summary>
    public DateTime? VisitAtUtc { get; set; }

    /// <summary>One of <see cref="ApplicationStates"/>.</summary>
    public string Status { get; set; } = ApplicationStates.Pending;

    /// <summary>Why it was declined, or a word with it when it was accepted.</summary>
    public string? DecisionNote { get; set; }

    public int? DecidedByUserId { get; set; }
    public DateTime? DecidedAtUtc { get; set; }

    /// <summary>The student record made when the registrar enrolled the student.</summary>
    public int? EnrolledStudentId { get; set; }

    public List<ApplicationFile> Files { get; set; } = [];

    public string FullName => string.Join(' ', new[] { FirstName, MiddleName, LastName, Suffix }.Where(p => !string.IsNullOrWhiteSpace(p)));
}

/// <summary>One paper of an application (the birth certificate, the report card, ...).</summary>
public class ApplicationFile : AuditableEntity
{
    public int ApplicationId { get; set; }
    public StudentApplication Application { get; set; } = null!;

    /// <summary>One of <see cref="ApplicationFileKinds"/>.</summary>
    public string Kind { get; set; } = "";

    public string Name { get; set; } = "";
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }
    public byte[] Data { get; set; } = [];
}
