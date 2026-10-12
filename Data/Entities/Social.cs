namespace SEAITT_LMS.Data.Entities;

// What the people of the school share with each other: a profile picture, friends, groups and files.

/// <summary>
/// The picture of a person (the one they upload in the settings menu). One row per user, the picture itself is the small JPEG
/// the browser made (at most 256 by 256 pixels). A deleted account takes its picture with it.
/// </summary>
public class ProfilePhoto
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>"image/jpeg", "image/png", "image/webp" or "image/gif".</summary>
    public string ContentType { get; set; } = "image/jpeg";

    public byte[] Data { get; set; } = [];

    /// <summary>When it was changed; it is also the version in the address of the picture, so a new picture is fetched again.</summary>
    public DateTime UpdatedAtUtc { get; set; }
}

// ====================================================================== friends

public static class FriendshipStates
{
    /// <summary>The first person asked, the other has not answered yet.</summary>
    public const string Pending = "Pending";

    public const string Accepted = "Accepted";
}

/// <summary>
/// Two people who are friends (or one who asked the other). The row is made by the person who asks (<see cref="RequesterUserId"/>);
/// when the other accepts, <see cref="Status"/> becomes Accepted. Saying no, cancelling and unfriending delete the row. There is never a
/// row for the same two people in both directions.
/// </summary>
public class Friendship : AuditableEntity
{
    public int RequesterUserId { get; set; }
    public User Requester { get; set; } = null!;

    public int AddresseeUserId { get; set; }
    public User Addressee { get; set; } = null!;

    /// <summary>One of <see cref="FriendshipStates"/>.</summary>
    public string Status { get; set; } = FriendshipStates.Pending;

    /// <summary>When the other person accepted.</summary>
    public DateTime? RespondedAtUtc { get; set; }
}

// ====================================================================== groups

/// <summary>The groups the system makes by itself.</summary>
public static class AutoGroupKinds
{
    /// <summary>The students of one grade level ("Grade 11 students").</summary>
    public const string GradeStudents = "GradeStudents";

    /// <summary>The students of one course (strand), of all grade levels.</summary>
    public const string CourseStudents = "CourseStudents";

    public const string AllStudents = "AllStudents";

    /// <summary>The teachers who teach in one grade level ("Grade 11 teachers").</summary>
    public const string GradeTeachers = "GradeTeachers";

    public const string AllTeachers = "AllTeachers";

    public static readonly string[] All = [GradeStudents, CourseStudents, AllStudents, GradeTeachers, AllTeachers];
}

/// <summary>
/// A group of students and teachers (a study group, a project team, a club): a list of members and a wall where the members write.
/// The owner made it; they can add and remove members and delete the group. Anybody can leave.
/// </summary>
public class StudyGroup : AuditableEntity
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }

    /// <summary>The person who made the group. Empty for an automatic group (see <see cref="AutoKind"/>): nobody owns it.</summary>
    public int? OwnerUserId { get; set; }
    public User? Owner { get; set; }

    /// <summary>
    /// Set for the groups the system makes and keeps by itself (one of <see cref="AutoGroupKinds"/>); empty for a group a student or teacher made.
    /// Who is in an automatic group is not chosen: it follows the enrollments and the courses of the teachers.
    /// </summary>
    public string? AutoKind { get; set; }

    /// <summary>With <see cref="AutoKind"/>: the grade level (11, 12) or the strand id; 0 when the group has no such number (all the students).</summary>
    public int? AutoKey { get; set; }

    public List<StudyGroupMember> Members { get; set; } = [];
    public List<GroupPost> Posts { get; set; } = [];
}

public class StudyGroupMember
{
    public int GroupId { get; set; }
    public StudyGroup Group { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime JoinedAtUtc { get; set; }

    /// <summary>Who added this person (the owner, or the person themselves for the owner's own row).</summary>
    public int? AddedByUserId { get; set; }
}

/// <summary>A message on the wall of a group. A reply has the post it answers in <see cref="ParentPostId"/>.</summary>
public class GroupPost : AuditableEntity
{
    public int GroupId { get; set; }
    public StudyGroup Group { get; set; } = null!;

    public int AuthorUserId { get; set; }
    public User Author { get; set; } = null!;

    public string Body { get; set; } = "";

    public int? ParentPostId { get; set; }
    public GroupPost? Parent { get; set; }
}

// ====================================================================== files

/// <summary>
/// A file in a person's locker. Only the person who owns it sees it, until they share it with other people, a group or one of
/// their courses (<see cref="SharedFile"/>). The bytes are in <see cref="StoredFileContent"/>, so a list of files never reads them.
/// </summary>
public class StoredFile : AuditableEntity
{
    public int OwnerUserId { get; set; }
    public User Owner { get; set; } = null!;

    public string Name { get; set; } = "";

    public string ContentType { get; set; } = "application/octet-stream";

    public long SizeBytes { get; set; }

    public StoredFileContent? Content { get; set; }
    public List<SharedFile> Shares { get; set; } = [];
}

public class StoredFileContent
{
    public int FileId { get; set; }
    public StoredFile File { get; set; } = null!;

    public byte[] Data { get; set; } = [];
}

/// <summary>
/// That a file is shared with one person, a group, or a course of a teacher (its students). Exactly one of
/// <see cref="SharedWithUserId"/>, <see cref="GroupId"/> and <see cref="AssignmentId"/> is set.
/// </summary>
public class SharedFile : AuditableEntity
{
    public int FileId { get; set; }
    public StoredFile File { get; set; } = null!;

    public int? SharedWithUserId { get; set; }
    public User? SharedWithUser { get; set; }

    public int? GroupId { get; set; }
    public StudyGroup? Group { get; set; }

    /// <summary>A course of a teacher (teacher, section and subject): the students of that course see the file.</summary>
    public int? AssignmentId { get; set; }
    public TeachingAssignment? Assignment { get; set; }
}

// ====================================================================== security log and system settings

public static class SecurityEvents
{
    public const string SignInSucceeded = "SignInSucceeded";
    public const string SignInFailed = "SignInFailed";
    public const string AccountLocked = "AccountLocked";
    public const string SignInRefused = "SignInRefused";
    public const string SignedOut = "SignedOut";
    public const string AccountCreated = "AccountCreated";
    public const string AccountChanged = "AccountChanged";
    public const string PasswordReset = "PasswordReset";
    public const string AccountActivated = "AccountActivated";
    public const string AccountDeactivated = "AccountDeactivated";
    public const string AccountDeleted = "AccountDeleted";
    public const string BackupCreated = "BackupCreated";
    public const string BackupDeleted = "BackupDeleted";
    public const string BackupRestored = "BackupRestored";
    public const string BackupRestoreFailed = "BackupRestoreFailed";
    public const string SettingsChanged = "SettingsChanged";
    public const string LogsCleared = "LogsCleared";

    /// <summary>The words for the page: what each event is.</summary>
    public static string Display(string kind) => kind switch
    {
        SignInSucceeded => "Signed in",
        SignInFailed => "Failed sign-in",
        AccountLocked => "Account locked",
        SignInRefused => "Sign-in refused",
        SignedOut => "Signed out",
        AccountCreated => "Account created",
        AccountChanged => "Account changed",
        PasswordReset => "Password set",
        AccountActivated => "Account activated",
        AccountDeactivated => "Account deactivated",
        AccountDeleted => "Account deleted",
        BackupCreated => "Backup made",
        BackupDeleted => "Backup deleted",
        BackupRestored => "Backup restored",
        BackupRestoreFailed => "Restore failed",
        SettingsChanged => "Settings changed",
        LogsCleared => "Old logs cleared",
        _ => kind,
    };

    /// <summary>How serious it is: "Info", "Warning" or "Critical".</summary>
    public static string SeverityOf(string kind) => kind switch
    {
        SignInFailed or SignInRefused or AccountDeactivated or BackupDeleted or LogsCleared => "Warning",
        AccountLocked or AccountDeleted or BackupRestored or BackupRestoreFailed => "Critical",
        _ => "Info",
    };
}

/// <summary>
/// One line of the security log: something that happened to an account or to the system (a sign-in, a wrong password, an account
/// that was changed, a backup restored). Only the administrator reads it. The email is kept as it was, in case the account is deleted later.
/// </summary>
public class SecurityLog : AuditableEntity
{
    /// <summary>One of <see cref="SecurityEvents"/>.</summary>
    public string Kind { get; set; } = "";

    /// <summary>"Info", "Warning" or "Critical".</summary>
    public string Severity { get; set; } = "Info";

    /// <summary>Who it happened to, or who did it when it is about the system. Empty when nobody (a wrong email).</summary>
    public int? UserId { get; set; }

    /// <summary>The email that was typed, or the account's email.</summary>
    public string? Email { get; set; }

    /// <summary>The person who did it, when it is not the same as <see cref="UserId"/> (an administrator changing an account).</summary>
    public int? ActorUserId { get; set; }

    public string? ActorName { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public string? Details { get; set; }
}

/// <summary>One setting the administrator can change (System Settings). Settings that were never changed have no row, and use their default.</summary>
public class SystemSetting
{
    public string Key { get; set; } = "";

    public string Value { get; set; } = "";

    public DateTime UpdatedAtUtc { get; set; }

    public int? UpdatedByUserId { get; set; }
}
