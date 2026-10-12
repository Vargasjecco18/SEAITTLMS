namespace SEAITT_LMS.Data.Entities;

// What the central team keeps: the course materials the academic provider sends to the teachers, and the teacher evaluation of the academic coordinator.

// ====================================================================== course materials

/// <summary>
/// A module the academic provider prepared for a course (a subject), or the syllabus of the course. It is not seen by
/// students: the provider sends it to the teachers of the course, and each teacher gets their own copy as a draft
/// (see <see cref="MaterialDelivery"/>) that they edit and post.
/// </summary>
public class MaterialModule : AuditableEntity
{
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;

    /// <summary>The syllabus of the course (it has no activities). Otherwise a module.</summary>
    public bool IsSyllabus { get; set; }

    public string Title { get; set; } = "";
    public string? Description { get; set; }

    /// <summary>The lesson (or the syllabus) as plain text.</summary>
    public string? Body { get; set; }

    /// <summary>A link to the file or the video.</summary>
    public string? LinkUrl { get; set; }

    public int Position { get; set; }

    public int? CreatedByUserId { get; set; }

    public List<MaterialItem> Items { get; set; } = [];
    public List<MaterialDelivery> Deliveries { get; set; } = [];
}

/// <summary>An activity, quiz or task performance of a <see cref="MaterialModule"/>. The same settings as a <see cref="ModuleItem"/>.</summary>
public class MaterialItem : AuditableEntity
{
    public int ModuleId { get; set; }
    public MaterialModule Module { get; set; } = null!;

    /// <summary>One of <see cref="ItemKinds"/>.</summary>
    public string Kind { get; set; } = ItemKinds.Activity;

    public string Title { get; set; } = "";
    public string? Instructions { get; set; }
    public decimal MaxScore { get; set; } = 100;

    /// <summary>A suggested deadline. The teacher can change it.</summary>
    public DateTime? DueAtUtc { get; set; }

    public int Position { get; set; }

    // ---- quizzes only
    public int? TimeLimitMinutes { get; set; }
    public bool ShuffleQuestions { get; set; } = true;
    public bool ShuffleOptions { get; set; } = true;
    public bool AntiCheat { get; set; } = true;
    public int MaxViolations { get; set; } = 3;
    public bool ShowResult { get; set; } = true;

    public List<MaterialQuestion> Questions { get; set; } = [];
}

public class MaterialQuestion : AuditableEntity
{
    public int ItemId { get; set; }
    public MaterialItem Item { get; set; } = null!;

    public int Position { get; set; }
    public string Text { get; set; } = "";

    /// <summary>One of <see cref="QuestionKinds"/>.</summary>
    public string Kind { get; set; } = QuestionKinds.MultipleChoice;

    public string? OptionsText { get; set; }
    public string CorrectAnswer { get; set; } = "";
    public decimal Points { get; set; } = 1;
}

/// <summary>
/// That a module of the library was sent to a teacher's course: <see cref="ModuleId"/> is the draft the teacher got. One per
/// library module and course of a teacher. When the teacher deletes their copy this row goes too, and the provider can send it again.
/// </summary>
public class MaterialDelivery : AuditableEntity
{
    public int MaterialModuleId { get; set; }
    public MaterialModule MaterialModule { get; set; } = null!;

    public int AssignmentId { get; set; }
    public TeachingAssignment Assignment { get; set; } = null!;

    /// <summary>The teacher's copy.</summary>
    public int ModuleId { get; set; }
    public LearningModule Module { get; set; } = null!;

    public int? SentByUserId { get; set; }

    /// <summary>
    /// When the teacher first opened the copy: its text (Edit), one of its activities, or when they posted or scheduled it. Empty until then.
    /// The academic provider reads it. (When the students open it is in the module views and item views.)
    /// </summary>
    public DateTime? OpenedAtUtc { get; set; }
}

// ====================================================================== teacher evaluation

public static class EvaluationQuestionKinds
{
    /// <summary>The student gives 1 to 5.</summary>
    public const string Rating = "Rating";

    /// <summary>The student writes a few words.</summary>
    public const string Comment = "Comment";

    public static readonly string[] All = [Rating, Comment];

    /// <summary>What each number of the rating means.</summary>
    public static string RatingLabel(int rating) => rating switch
    {
        5 => "Excellent",
        4 => "Very good",
        3 => "Good",
        2 => "Fair",
        1 => "Poor",
        _ => "",
    };

    /// <summary>How an average of the ratings reads.</summary>
    public static string AverageLabel(decimal average) => average switch
    {
        >= 4.5m => "Outstanding",
        >= 3.5m => "Very satisfactory",
        >= 2.5m => "Satisfactory",
        >= 1.5m => "Fair",
        _ => "Needs improvement",
    };
}

/// <summary>
/// A questionnaire the academic coordinator makes about the teachers. A draft can be edited. Sent, it goes to the students of the
/// courses it is about (<see cref="EvaluationTarget"/>), who answer once for each teacher before the due date; the answers are
/// anonymous for everybody except that the coordinator can see who has not answered yet.
/// </summary>
public class TeacherEvaluation : AuditableEntity
{
    public string Title { get; set; } = "";
    public string? Instructions { get; set; }

    /// <summary>Empty: a draft. Otherwise when it was sent to the students.</summary>
    public DateTime? SentAtUtc { get; set; }

    /// <summary>The deadline for the students.</summary>
    public DateTime? DueAtUtc { get; set; }

    /// <summary>The term it was sent in.</summary>
    public int? TermId { get; set; }
    public Term? Term { get; set; }

    public int? CreatedByUserId { get; set; }

    public List<EvaluationQuestion> Questions { get; set; } = [];
    public List<EvaluationTarget> Targets { get; set; } = [];
    public List<EvaluationResponse> Responses { get; set; } = [];
}

public class EvaluationQuestion : AuditableEntity
{
    public int EvaluationId { get; set; }
    public TeacherEvaluation Evaluation { get; set; } = null!;

    public int Position { get; set; }
    public string Text { get; set; } = "";

    /// <summary>One of <see cref="EvaluationQuestionKinds"/>.</summary>
    public string Kind { get; set; } = EvaluationQuestionKinds.Rating;

    /// <summary>The area the question is about ("Teaching", "Classroom management"), so the analytics can group them. Optional.</summary>
    public string? Category { get; set; }
}

/// <summary>A teacher's course (one teacher, one section, one subject) that a sent evaluation is about.</summary>
public class EvaluationTarget
{
    public int EvaluationId { get; set; }
    public TeacherEvaluation Evaluation { get; set; } = null!;

    public int AssignmentId { get; set; }
    public TeachingAssignment Assignment { get; set; } = null!;
}

/// <summary>One student's answers about one teacher's course. One per student, evaluation and course.</summary>
public class EvaluationResponse : AuditableEntity
{
    public int EvaluationId { get; set; }
    public TeacherEvaluation Evaluation { get; set; } = null!;

    public int AssignmentId { get; set; }
    public TeachingAssignment Assignment { get; set; } = null!;

    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public DateTime SubmittedAtUtc { get; set; }

    public List<EvaluationAnswer> Answers { get; set; } = [];
}

public class EvaluationAnswer
{
    public int ResponseId { get; set; }
    public EvaluationResponse Response { get; set; } = null!;

    public int QuestionId { get; set; }
    public EvaluationQuestion Question { get; set; } = null!;

    /// <summary>1 to 5 (rating questions).</summary>
    public byte? Rating { get; set; }

    /// <summary>The words of the student (comment questions).</summary>
    public string? Comment { get; set; }
}
