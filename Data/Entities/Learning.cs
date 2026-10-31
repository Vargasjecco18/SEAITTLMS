namespace SEAITT_LMS.Data.Entities;

/// <summary>
/// A teacher teaches a subject to a section: "Ms. Reyes teaches BUSINESS ACCOUNTING to 11-BUSSENT-A". This is the
/// "subject" a teacher opens and the one the students of that section see. The administrator sets it up (Users Management,
/// Teaching). Modules, attendance and grades all hang off it.
/// </summary>
public class TeachingAssignment : AuditableEntity
{
    public int TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    public int SectionId { get; set; }
    public Section Section { get; set; } = null!;

    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;

    public List<LearningModule> Modules { get; set; } = [];
}

/// <summary>
/// A part of a subject: a lesson with text and, below it, the assignments, quizzes and task performances that go with it.
/// A new module is a draft that students cannot see. The teacher posts it now or sets a time for it to appear.
/// </summary>
public class LearningModule : AuditableEntity
{
    public int AssignmentId { get; set; }
    public TeachingAssignment Assignment { get; set; } = null!;

    public string Title { get; set; } = "";
    public string? Description { get; set; }

    /// <summary>The lesson itself, as plain text (line breaks are kept).</summary>
    public string? Body { get; set; }

    /// <summary>A link to a file or a video the lesson uses.</summary>
    public string? LinkUrl { get; set; }

    /// <summary>The syllabus of the course (sent by the academic provider or written by the teacher). It has no activities.</summary>
    public bool IsSyllabus { get; set; }

    /// <summary>
    /// The box that holds the summative exams the academic provider posted to the course. It is not a module: the menus and lists of modules leave it out, and its exams show under Exams.
    /// </summary>
    public bool IsExamSet { get; set; }

    /// <summary>The order in the subject, 1, 2, 3 ...</summary>
    public int Position { get; set; }

    /// <summary>
    /// When students can see it. Empty is a draft (hidden). A time in the past is posted, a time in the future is
    /// scheduled: it appears by itself at that time.
    /// </summary>
    public DateTime? VisibleFromUtc { get; set; }

    /// <summary>When the students were told it is there (set by the background worker), so they are told once.</summary>
    public DateTime? NotifiedAtUtc { get; set; }

    public List<ModuleItem> Items { get; set; } = [];

    /// <summary>Files attached to the module.</summary>
    public List<Attachment> Attachments { get; set; } = [];
}

public static class ItemKinds
{
    public const string Activity = "Activity";
    public const string Quiz = "Quiz";
    public const string TaskPerformance = "TaskPerformance";

    /// <summary>Work done in class (a few questions on the lesson of the day). Handled like an assignment.</summary>
    public const string Seatwork = "Seatwork";

    /// <summary>Recitation: the teacher gives the score for what the student said in class. Handled like an assignment (the teacher types the scores in the gradebook).</summary>
    public const string Recitation = "Recitation";

    /// <summary>A major test (a midterm, a final): it is added from the gradebook, where its scores are typed. It is not one of the kinds a teacher adds to a module.</summary>
    public const string Exam = "Exam";

    /// <summary>The kinds a teacher adds to a module, in the order of the course menu and of the Excel template: seatwork, assignment, quiz, task performance. (The recitation is only a column of the gradebook.)</summary>
    public static readonly string[] All = [Seatwork, Activity, Quiz, TaskPerformance];

    /// <summary>Every kind that is listed in the module, in the menu of the course and in the gradebook: the kinds of <see cref="All"/>, the recitation (gradebook) and the exam.</summary>
    public static readonly string[] Listed = [Seatwork, Activity, Quiz, TaskPerformance, Recitation, Exam];

    /// <summary>The kinds whose tab (in the menu of the course and in a module) is only there once the course has one: the recitation and the exam.</summary>
    public static bool IsOptionalTab(string kind) => kind is Exam or Recitation;

    /// <summary>A quiz and a summative exam are taken on the screen: they have questions, a time limit and the anti-cheating watch, and the system gives the score.</summary>
    public static bool IsQuizLike(string? kind) => kind is Quiz or Exam;

    public static string Display(string kind) => kind switch
    {
        Quiz => "Quiz",
        TaskPerformance => "Task performance",
        Seatwork => "Seatwork",
        Recitation => "Recitation",
        Exam => "Exam",
        _ => "Assignment",
    };

    public static string Plural(string kind) => kind switch
    {
        Quiz => "Quizzes",
        TaskPerformance => "Task performances",
        Seatwork => "Seatworks",
        Recitation => "Recitations",
        Exam => "Exams",
        _ => "Assignments",
    };

    /// <summary>The short code of the column in the gradebook: A, Q, TP, S, R, E.</summary>
    public static string Code(string kind) => kind switch
    {
        Quiz => "Q",
        TaskPerformance => "TP",
        Seatwork => "S",
        Recitation => "R",
        Exam => "E",
        _ => "A",
    };
}

/// <summary>Something a student does for a module: an assignment (stored as "Activity"), a quiz, a task performance, a seatwork or a recitation. Has a due date and a maximum score.</summary>
public class ModuleItem : AuditableEntity
{
    public int ModuleId { get; set; }
    public LearningModule Module { get; set; } = null!;

    /// <summary>One of <see cref="ItemKinds"/>.</summary>
    public string Kind { get; set; } = ItemKinds.Activity;

    public string Title { get; set; } = "";
    public string? Instructions { get; set; }

    public decimal MaxScore { get; set; } = 100;

    /// <summary>The deadline, or empty for none.</summary>
    public DateTime? DueAtUtc { get; set; }

    /// <summary>Empty: it appears when its module does. A time: it appears then (but never before its module).</summary>
    public DateTime? VisibleFromUtc { get; set; }

    public DateTime? NotifiedAtUtc { get; set; }
    public DateTime? DueReminderSentAtUtc { get; set; }

    public int Position { get; set; }

    /// <summary>The summative exam of the academic provider's library this item is the copy of (so it is not sent twice to the same course). Empty for the rest.</summary>
    public int? MaterialItemId { get; set; }

    /// <summary>The place of its column in the gradebook (1, 2, 3 ...) once the teacher moved the columns. 0: not placed yet, so after the columns that are.</summary>
    public int GradebookOrder { get; set; }

    // ---- quizzes only
    /// <summary>Minutes a student has once they start. Empty: no limit.</summary>
    public int? TimeLimitMinutes { get; set; }

    public bool ShuffleQuestions { get; set; } = true;
    public bool ShuffleOptions { get; set; } = true;

    /// <summary>Watch for leaving the quiz page, copying and pasting. After <see cref="MaxViolations"/> the quiz is handed in.</summary>
    public bool AntiCheat { get; set; } = true;

    public int MaxViolations { get; set; } = 3;

    /// <summary>Show the student their score as soon as they hand the quiz in.</summary>
    public bool ShowResult { get; set; } = true;

    public List<QuizQuestion> Questions { get; set; } = [];
    public List<Submission> Submissions { get; set; } = [];

    /// <summary>Files attached to the activity, quiz or task performance.</summary>
    public List<Attachment> Attachments { get; set; } = [];
}

public static class QuestionKinds
{
    public const string MultipleChoice = "MultipleChoice";
    public const string TrueFalse = "TrueFalse";
    public const string ShortAnswer = "ShortAnswer";
}

public class QuizQuestion : AuditableEntity
{
    public int ItemId { get; set; }
    public ModuleItem Item { get; set; } = null!;

    public int Position { get; set; }
    public string Text { get; set; } = "";

    /// <summary>One of <see cref="QuestionKinds"/>.</summary>
    public string Kind { get; set; } = QuestionKinds.MultipleChoice;

    /// <summary>The choices of a multiple choice question, one per line.</summary>
    public string? OptionsText { get; set; }

    /// <summary>
    /// The right answer: the text of the right choice, "True" or "False", or for a short answer the accepted answers
    /// separated by "|".
    /// </summary>
    public string CorrectAnswer { get; set; } = "";

    public decimal Points { get; set; } = 1;
}

public static class SubmissionStatus
{
    public const string InProgress = "InProgress";
    public const string Submitted = "Submitted";
    public const string Graded = "Graded";
}

/// <summary>What a teacher decided about late work: waiting, accepted (it is graded), or declined (it is not).</summary>
public static class LateDecisions
{
    public const string Pending = "Pending";
    public const string Accepted = "Accepted";
    public const string Declined = "Declined";
}

/// <summary>A student's work for one item, or their attempt at a quiz. One per student and item.</summary>
public class Submission : AuditableEntity
{
    public int ItemId { get; set; }
    public ModuleItem Item { get; set; } = null!;

    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    /// <summary>One of <see cref="SubmissionStatus"/>.</summary>
    public string Status { get; set; } = SubmissionStatus.InProgress;

    /// <summary>When a quiz was started.</summary>
    public DateTime? StartedAtUtc { get; set; }

    public DateTime? SubmittedAtUtc { get; set; }

    /// <summary>Handed in after the due date.</summary>
    public bool IsLate { get; set; }

    /// <summary>
    /// What the teacher decided about work that was handed in late: <see cref="LateDecisions"/>. Empty for work that was on time (and for work
    /// handed in late before teachers could decide). Late work is graded only after the teacher accepted it.
    /// </summary>
    public string? LateDecision { get; set; }

    /// <summary>What the student wrote, without formatting (for counting and for the screens that only need the words).</summary>
    public string? AnswerText { get; set; }

    /// <summary>What the student wrote in the editor, with its formatting, already cleaned by <c>RichText.Sanitize</c>.</summary>
    public string? AnswerHtml { get; set; }

    public string? LinkUrl { get; set; }

    /// <summary>A quiz: the answers given so far, and the order the questions were shown in, as JSON.</summary>
    public string? AnswersJson { get; set; }

    public decimal? Score { get; set; }
    public string? Feedback { get; set; }
    public DateTime? GradedAtUtc { get; set; }

    // ---- quizzes with the anti-cheating watch
    public int ViolationCount { get; set; }

    /// <summary>What was noticed (leaving the page, copying, ...), one line each with the time.</summary>
    public string? ViolationLog { get; set; }

    /// <summary>The teacher should look at this attempt.</summary>
    public bool IsFlagged { get; set; }

    /// <summary>Why a quiz was handed in by the system: "time" or "violations".</summary>
    public string? AutoSubmitReason { get; set; }

    /// <summary>The files the student sent with the work (the work is sent as files, not as a link).</summary>
    public List<Attachment> Attachments { get; set; } = [];
}

/// <summary>
/// The final grade the teacher gives a student in a subject (a number from 0 to 100), next to the average the gradebook computes.
/// One per student and subject; the student sees it with the grades.
/// </summary>
public class FinalGrade : AuditableEntity
{
    public int AssignmentId { get; set; }
    public TeachingAssignment Assignment { get; set; } = null!;

    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public decimal Grade { get; set; }
}

/// <summary>That a student has opened a module (so the dashboard can list the ones they have not opened yet).</summary>
public class ModuleView
{
    public int ModuleId { get; set; }
    public LearningModule Module { get; set; } = null!;

    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public DateTime FirstOpenedAtUtc { get; set; }
}

/// <summary>That a student has opened an activity, a quiz or a task performance (its page). The academic provider reads it to see what the students opened.</summary>
public class ItemView
{
    public int ItemId { get; set; }
    public ModuleItem Item { get; set; } = null!;

    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public DateTime FirstOpenedAtUtc { get; set; }
}

public static class AttendanceStatus
{
    public const string Present = "Present";
    public const string Late = "Late";
    public const string Absent = "Absent";

    public static readonly string[] All = [Present, Late, Absent];
}

/// <summary>Whether a student came to a subject's class on a day. The teacher of the subject marks it.</summary>
public class AttendanceRecord : AuditableEntity
{
    public int AssignmentId { get; set; }
    public TeachingAssignment Assignment { get; set; } = null!;

    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    /// <summary>The school day (a Philippine date).</summary>
    public DateOnly AttendedOn { get; set; }

    /// <summary>One of <see cref="AttendanceStatus"/>.</summary>
    public string Status { get; set; } = AttendanceStatus.Present;

    public string? Note { get; set; }
}

/// <summary>An event a person wrote on their own calendar (a student or a teacher): a title, a day, maybe a time and a note. Its owner sees it, and so do the people it was shared with.</summary>
public class CalendarEntry : AuditableEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public string Title { get; set; } = "";

    /// <summary>The day in the Philippines.</summary>
    public DateOnly Day { get; set; }

    /// <summary>The time of day (Philippine time), or empty for an event that takes the whole day.</summary>
    public TimeOnly? StartTime { get; set; }

    public string? Notes { get; set; }

    /// <summary>The people the event was shared with (a group, friends, classmates). They see it on their own calendar; only the owner changes it.</summary>
    public List<CalendarEntryInvite> Invites { get; set; } = [];
}

/// <summary>A person an event of someone else's calendar was shared with.</summary>
public class CalendarEntryInvite
{
    public int EntryId { get; set; }
    public CalendarEntry Entry { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>
/// That a student earned an achievement (a badge). The achievements themselves are worked out from the student's work; this remembers that it was earned
/// and whether the student has been shown the "you earned it" box, so the box appears once.
/// </summary>
public class AchievementAward : AuditableEntity
{
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    /// <summary>The key of the achievement, like "perfect-score".</summary>
    public string AchievementKey { get; set; } = "";

    public DateTime AwardedAtUtc { get; set; }

    /// <summary>When the student saw the box; empty until then.</summary>
    public DateTime? SeenAtUtc { get; set; }
}
