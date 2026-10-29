namespace SEAITT_LMS.Data.Entities;

/// <summary>A message from one person to another. The time it was sent is <see cref="AuditableEntity.CreatedAtUtc"/>.</summary>
public class Message : AuditableEntity
{
    public int SenderUserId { get; set; }
    public User Sender { get; set; } = null!;

    public int RecipientUserId { get; set; }
    public User Recipient { get; set; } = null!;

    public string Body { get; set; } = "";

    /// <summary>When the recipient opened it; empty while it is unread.</summary>
    public DateTime? ReadAtUtc { get; set; }

    /// <summary>The files sent with the message.</summary>
    public ICollection<Attachment> Attachments { get; set; } = [];
}

/// <summary>
/// Something the system wants a person to know: a new module, a grade, a deadline coming up, a referral.
/// It shows under the bell in the header and links to the page it is about.
/// </summary>
public class Notification : AuditableEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>What kind it is: "module", "activity", "grade", "due", "message", "referral", ... (used for the icon).</summary>
    public string Kind { get; set; } = "";

    public string Title { get; set; } = "";
    public string? Body { get; set; }

    /// <summary>The page it is about, as an address inside the app ("student/subjects/3"), or empty.</summary>
    public string? Link { get; set; }

    public bool IsRead { get; set; }
}

public static class AnnouncementAudiences
{
    public const string Everyone = "Everyone";
    public const string Students = "Students";
    public const string Teachers = "Teachers";

    public static readonly string[] All = [Everyone, Students, Teachers];
}

/// <summary>What an announcement is about. The coordinator can use all of them; a teacher's announcement for their class is an exam or other.</summary>
public static class AnnouncementCategories
{
    public const string Exam = "Exam";
    public const string Holiday = "Holiday";
    public const string SemesterBreak = "SemesterBreak";
    public const string TeachersMeeting = "TeachersMeeting";
    public const string Other = "Other";

    public static readonly string[] All = [Exam, Holiday, SemesterBreak, TeachersMeeting, Other];

    /// <summary>What a teacher can choose for an announcement to their class.</summary>
    public static readonly string[] ForClass = [Exam, Other];

    /// <summary>The name of one announcement ("Exam") and of the group of them ("Exams").</summary>
    public static string Display(string category) => category switch
    {
        Exam => "Exam",
        Holiday => "Holiday",
        SemesterBreak => "Semester break",
        TeachersMeeting => "Teachers' meeting",
        _ => "Other",
    };

    public static string DisplayPlural(string category) => category switch
    {
        Exam => "Exams",
        Holiday => "Holidays",
        SemesterBreak => "Semester break",
        TeachersMeeting => "Teachers' meetings",
        _ => "Others",
    };

    public static string Icon(string category) => category switch
    {
        Exam => "notebook-pen",
        Holiday => "sun",
        SemesterBreak => "calendar-range",
        TeachersMeeting => "presentation",
        _ => "megaphone",
    };
}

/// <summary>
/// A notice for a group of people. The academic coordinator writes them for everybody, for the students or for the
/// teachers (an exam, a holiday, the semester break, a teachers' meeting, anything else); a teacher writes them for the students of
/// one of their courses (their class). Students see them on the right of their dashboard and in the announcements button of the header.
/// </summary>
public class Announcement : AuditableEntity
{
    public int AuthorUserId { get; set; }
    public User Author { get; set; } = null!;

    public string Title { get; set; } = "";
    public string Body { get; set; } = "";

    /// <summary>One of <see cref="AnnouncementAudiences"/>.</summary>
    public string Audience { get; set; } = AnnouncementAudiences.Everyone;

    /// <summary>Only the students of this section (a teacher's announcement). Empty: the whole audience.</summary>
    public int? SectionId { get; set; }
    public Section? Section { get; set; }

    /// <summary>One of <see cref="AnnouncementCategories"/>.</summary>
    public string Category { get; set; } = AnnouncementCategories.Other;

    /// <summary>The course (subject and section) a teacher wrote it for. Empty for the notices of the coordinator.</summary>
    public int? AssignmentId { get; set; }
    public TeachingAssignment? Assignment { get; set; }

    /// <summary>The last day (a Philippine date) the announcement is shown. Empty: until it is deleted.</summary>
    public DateOnly? ExpiresOn { get; set; }
}
