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
