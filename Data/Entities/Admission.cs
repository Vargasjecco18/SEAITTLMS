namespace SEAITT_LMS.Data.Entities;

/// <summary>
/// Round 46: the information of a student that the Admission staff of Grade 11 or Grade 12 record before the registrar enrolls the student: the name, the LRN, the contact, the parents and the guardian.
/// The registrar chooses the student from these records (the LRN is shown, not typed) and the printable enrollment form fills in the parents and the guardian by itself.
/// There is no application form for students on the home page: the Admission staff write the record.
/// </summary>
public class AdmissionRecord : AuditableEntity
{
    /// <summary>11 or 12: the Admission staff of that grade level record and see it.</summary>
    public byte GradeLevel { get; set; }

    /// <summary>Learner Reference Number, 12 digits. Unique.</summary>
    public string Lrn { get; set; } = "";

    public string FirstName { get; set; } = "";
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = "";
    public string? NameExtension { get; set; }
    public Sex? Sex { get; set; }

    /// <summary>The student's mobile number (E.164).</summary>
    public string? PhoneNumber { get; set; }

    public string? Address { get; set; }
    public string? PreviousSchool { get; set; }

    public string? FatherName { get; set; }
    public string? FatherPhone { get; set; }
    public string? MotherName { get; set; }
    public string? MotherPhone { get; set; }
    public string? GuardianName { get; set; }
    public string? GuardianRelationship { get; set; }
    public string? GuardianPhone { get; set; }
    public string? GuardianAddress { get; set; }

    /// <summary>Round 47: the strand the student wants (the Admission staff choose it, the registrar can change it when enrolling). Empty until somebody chooses it.</summary>
    public int? StrandId { get; set; }
    public Strand? Strand { get; set; }

    public int? RecordedByUserId { get; set; }

    /// <summary>When the registrar enrolled the student from this record (empty until then).</summary>
    public DateTime? EnrolledAtUtc { get; set; }

    public List<AdmissionDocument> Documents { get; set; } = [];
}

/// <summary>A document of the student that the Admission staff received (a copy of the birth certificate, the report card ...): the kind, and the file when there is a copy.</summary>
public class AdmissionDocument : AuditableEntity
{
    public int RecordId { get; set; }
    public AdmissionRecord Record { get; set; } = null!;

    /// <summary>One of <see cref="AdmissionDocumentKinds"/>.</summary>
    public string Kind { get; set; } = "";

    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public long SizeBytes { get; set; }
    public byte[]? Data { get; set; }

    public DateTime ReceivedAtUtc { get; set; }
    public int? ReceivedByUserId { get; set; }
}

public static class AdmissionDocumentKinds
{
    public const string BirthCertificate = "PSA birth certificate";
    public const string ReportCard = "Form 138 (report card)";

    /// <summary>Round 47: the permanent record of the school the student comes from.</summary>
    public const string Form137 = "Form 137";
    public const string CertificateOfCompletion = "Certificate of completion (Grade 10)";
    public const string GoodMoral = "Good moral certificate";
    public const string Photo = "2x2 photo";
    public const string Other = "Other document";

    public static readonly IReadOnlyList<string> All = [BirthCertificate, ReportCard, Form137, CertificateOfCompletion, GoodMoral, Photo, Other];
}
