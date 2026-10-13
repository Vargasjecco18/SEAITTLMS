namespace SEAITT_LMS.Data.Entities;

/// <summary>
/// A file attached to a syllabus, a module, or an activity, quiz or task performance. The academic provider attaches files to the library
/// (<see cref="MaterialModuleId"/>, <see cref="MaterialItemId"/>); when the library is sent to a teacher, every file is copied to the teacher's
/// draft (<see cref="ModuleId"/>, <see cref="ItemId"/>), where the teacher can add and remove files too. The students of the course open them
/// when the module is posted. A student's work (<see cref="SubmissionId"/>) is sent as files too. Exactly one of the five owners is set. The bytes are in <see cref="AttachmentContent"/> (so a backup has them).
/// </summary>
public class Attachment : AuditableEntity
{
    public string Name { get; set; } = "";

    /// <summary>What the person's browser said it is. It is not trusted: the type a file is served as is decided from its name and its first bytes.</summary>
    public string ContentType { get; set; } = "application/octet-stream";

    public long SizeBytes { get; set; }

    /// <summary>Who added it (empty when the account was deleted).</summary>
    public int? UploadedByUserId { get; set; }
    public User? UploadedBy { get; set; }

    public int? MaterialModuleId { get; set; }
    public MaterialModule? MaterialModule { get; set; }

    public int? MaterialItemId { get; set; }
    public MaterialItem? MaterialItem { get; set; }

    public int? ModuleId { get; set; }
    public LearningModule? Module { get; set; }

    public int? ItemId { get; set; }
    public ModuleItem? Item { get; set; }

    /// <summary>A file of a student's work (an activity, quiz or task performance handed in).</summary>
    public int? SubmissionId { get; set; }
    public Submission? Submission { get; set; }

    public AttachmentContent? Content { get; set; }
}

public class AttachmentContent
{
    public int AttachmentId { get; set; }
    public Attachment Attachment { get; set; } = null!;

    public byte[] Data { get; set; } = [];
}
