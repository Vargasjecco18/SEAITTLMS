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

    /// <summary>The whole address in one line (street, city, province, postal code), made from the parts below: what the printable form and the lists show.</summary>
    public string? Address { get; set; }
    public string? PreviousSchool { get; set; }

    /// <summary>Round 49: the email address of the student (the person's own, apart from the sign-in email the system makes).</summary>
    public string? Email { get; set; }

    // Round 49: the address in four parts: the street address, the city or municipality (a list of Albay with its own Add address), the state or province and the postal / zip code.
    public string? AddressStreet { get; set; }
    public string? AddressCity { get; set; }
    public string? AddressProvince { get; set; }
    public string? AddressPostalCode { get; set; }

    public string? FatherName { get; set; }
    public string? FatherPhone { get; set; }
    public string? FatherOccupation { get; set; }
    public string? FatherEmail { get; set; }
    public string? MotherName { get; set; }
    public string? MotherPhone { get; set; }
    public string? MotherOccupation { get; set; }
    public string? MotherEmail { get; set; }

    /// <summary>Round 49: where the parents live (one address for the father and the mother): the whole line and its four parts.</summary>
    public string? ParentsAddress { get; set; }
    public string? ParentsStreet { get; set; }
    public string? ParentsCity { get; set; }
    public string? ParentsProvince { get; set; }
    public string? ParentsPostalCode { get; set; }

    public string? GuardianName { get; set; }
    public string? GuardianRelationship { get; set; }
    public string? GuardianPhone { get; set; }
    public string? GuardianOccupation { get; set; }
    public string? GuardianEmail { get; set; }
    public string? GuardianAddress { get; set; }
    public string? GuardianStreet { get; set; }
    public string? GuardianCity { get; set; }
    public string? GuardianProvince { get; set; }
    public string? GuardianPostalCode { get; set; }

    /// <summary>Round 47: the strand the student wants (the Admission staff choose it, the registrar can change it when enrolling). Empty until somebody chooses it.</summary>
    public int? StrandId { get; set; }
    public Strand? Strand { get; set; }

    /// <summary>Round 49: the track the student wants (ACAD or TECH-PRO): the strands to choose from are the ones of the track. It is kept even when no strand was chosen yet.</summary>
    public int? TrackId { get; set; }
    public Track? Track { get; set; }

    public int? RecordedByUserId { get; set; }

    /// <summary>When the registrar enrolled the student from this record (empty until then).</summary>
    public DateTime? EnrolledAtUtc { get; set; }

    public List<AdmissionDocument> Documents { get; set; } = [];
}

/// <summary>
/// Round 49: a name the Admission staff added to a list of the record form with its "Add school" or "Add address" button: a school of Albay (<see cref="AdmissionOptionKinds.School"/>) or a city or a
/// municipality (<see cref="AdmissionOptionKinds.City"/>). The lists start with the schools and the places of Albay.
/// </summary>
public class AdmissionOption : AuditableEntity
{
    public string Kind { get; set; } = "";
    public string Name { get; set; } = "";
}

public static class AdmissionOptionKinds
{
    public const string School = "School";
    public const string City = "City";
    public const int MaxNameLength = 120;
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

    /// <summary>
    /// Round 48: the five kinds every student has a line for. There is no "2x2 photo" and no "Other document" any more: the Admission staff add any other document by typing its name
    /// (a document of a name that is not one of these five is an "other" document; its name is the kind).
    /// </summary>
    public static readonly IReadOnlyList<string> All = [BirthCertificate, ReportCard, Form137, CertificateOfCompletion, GoodMoral];

    /// <summary>The longest name of an other document (the column <c>admission_documents.kind</c> has 60 characters).</summary>
    public const int MaxNameLength = 60;

    public static bool IsFixed(string? kind) => kind is not null && All.Contains(kind);
}
