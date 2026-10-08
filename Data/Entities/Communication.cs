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

/// <summary>
/// A notice for a group of people. The academic coordinator writes them for everybody, for the students or for the
/// teachers; a teacher writes them for the students of one of their sections. Students see them on the right of their dashboard.
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

    /// <summary>The last day (a Philippine date) the announcement is shown. Empty: until it is deleted.</summary>
    public DateOnly? ExpiresOn { get; set; }
}
