namespace SEAITT_LMS.Data.Entities;

/// <summary>
/// A one-time code that was sent to a person who typed the right password (round 42: administrators, when OTP is on in System Settings). The code itself is never kept, only a hash
/// of it, and the page holds a random token whose hash is <see cref="ChallengeHash"/>, so reading this table does not give anybody a way in. A code lives a few minutes,
/// takes a few wrong tries and can be sent again after a short wait.
/// </summary>
public class LoginOtp : AuditableEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>SHA-256 (hex) of the random token the browser keeps in a cookie while it waits for the code.</summary>
    public string ChallengeHash { get; set; } = "";

    /// <summary>SHA-256 (hex) of the token and the code together.</summary>
    public string CodeHash { get; set; } = "";

    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>When the code was sent (the wait before a new one counts from here).</summary>
    public DateTime SentAtUtc { get; set; }

    /// <summary>How many codes were sent for this sign-in.</summary>
    public int Sends { get; set; } = 1;

    /// <summary>Wrong codes typed for the code that is valid now.</summary>
    public int Attempts { get; set; }

    /// <summary>The code was used or cancelled: it can't be used again.</summary>
    public DateTime? ConsumedAtUtc { get; set; }

    /// <summary>The number that was written to, like +63977***6918 (for the page and the log).</summary>
    public string SentTo { get; set; } = "";
}
