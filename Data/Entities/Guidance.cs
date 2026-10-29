namespace SEAITT_LMS.Data.Entities;

/// <summary>Where a student is in their schooling. Graduates are archived: their records stay, their login is switched off.</summary>
public static class StudentStatus
{
    public const string Active = "Active";
    public const string Graduated = "Graduated";

    /// <summary>The student left school before finishing. Like a graduate the student is archived: the account is switched off and the records stay.</summary>
    public const string DroppedOut = "DroppedOut";

    public static readonly string[] All = [Active, Graduated, DroppedOut];

    public static string Display(string? status) => status switch
    {
        Active => "Active",
        Graduated => "Graduated",
        DroppedOut => "Dropped out",
        _ => status ?? "",
    };
}

/// <summary>Why a student was referred to the guidance counselor.</summary>
public static class ReferralReasons
{
    public const string FailingGrades = "FailingGrades";
    public const string AtRisk = "AtRisk";
    public const string LowAttendance = "LowAttendance";

    public static readonly string[] All = [FailingGrades, AtRisk, LowAttendance];

    public static string Display(string reason) => reason switch
    {
        FailingGrades => "Failing grades",
        AtRisk => "At risk",
        LowAttendance => "Not attending class",
        _ => reason,
    };
}

public static class ReferralStatus
{
    public const string Open = "Open";
    public const string InProgress = "InProgress";
    public const string Resolved = "Resolved";

    public static readonly string[] All = [Open, InProgress, Resolved];

    public static string Display(string status) => status == InProgress ? "In progress" : status;
}

/// <summary>How a referral ended, so the guidance counselor can see which help works.</summary>
public static class ReferralOutcomes
{
    public const string Improved = "Improved";
    public const string NoChange = "NoChange";

    public static readonly string[] All = [Improved, NoChange];

    public static string Display(string outcome) => outcome == NoChange ? "No change" : outcome;
}

/// <summary>
/// A student the system sent to the guidance counselor because their grade in a subject is below 70 (failing), close to it (at risk),
/// or they miss class (low attendance). The system makes referrals by itself; the counselor works them: Open, In progress, Resolved.
/// </summary>
public class Referral : AuditableEntity
{
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    /// <summary>The subject the problem is in (the link is dropped if the teacher's subject is removed).</summary>
    public int? AssignmentId { get; set; }
    public TeachingAssignment? Assignment { get; set; }

    /// <summary>One of <see cref="ReferralReasons"/>.</summary>
    public string Reason { get; set; } = ReferralReasons.FailingGrades;

    /// <summary>What was found, in words: "BUSINESS ACCOUNTING: 61.5% (passing is 70)".</summary>
    public string Detail { get; set; } = "";

    /// <summary>The number behind it: the grade or the attendance rate in percent.</summary>
    public decimal? Metric { get; set; }

    /// <summary>One of <see cref="ReferralStatus"/>.</summary>
    public string Status { get; set; } = ReferralStatus.Open;

    /// <summary>What the counselor did: "Talked with the student", "Parents called", "Tutoring" ...</summary>
    public string? Intervention { get; set; }

    public string? Notes { get; set; }

    /// <summary>One of <see cref="ReferralOutcomes"/>, set when the referral is resolved.</summary>
    public string? Outcome { get; set; }

    public DateTime? ResolvedAtUtc { get; set; }

    public int? HandledByUserId { get; set; }
    public User? HandledBy { get; set; }
}
