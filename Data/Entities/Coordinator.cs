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

    /// <summary>
    /// The box that holds the summative exams of the course (one per course, made when the first exam is added; it is not a module and is never listed or
    /// sent as one). Each exam is a <see cref="MaterialItem"/> of kind Exam in it.
    /// </summary>
    public bool IsExamSet { get; set; }

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

    /// <summary>Files attached to the syllabus or the module. They go to the teachers with it.</summary>
    public List<Attachment> Attachments { get; set; } = [];
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

    /// <summary>A suggested deadline. The teacher can change it. For a summative exam: when it closes.</summary>
    public DateTime? DueAtUtc { get; set; }

    /// <summary>Summative exams only: when the exam opens (its schedule runs from here to <see cref="DueAtUtc"/>).</summary>
    public DateTime? VisibleFromUtc { get; set; }

    public int Position { get; set; }

    // ---- quizzes only
    public int? TimeLimitMinutes { get; set; }
    public bool ShuffleQuestions { get; set; } = true;
    public bool ShuffleOptions { get; set; } = true;
    public bool AntiCheat { get; set; } = true;
    public int MaxViolations { get; set; } = 3;
    public bool ShowResult { get; set; } = true;

    public List<MaterialQuestion> Questions { get; set; } = [];

    /// <summary>Files attached to the activity, quiz or task performance (a quiz can be questions, a file, or both).</summary>
    public List<Attachment> Attachments { get; set; } = [];
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
    /// <summary>5-Point Likert Scale: how much the student agrees (Strongly disagree to Strongly agree). Answer: 1 to 5.</summary>
    public const string Likert = "Likert";

    /// <summary>5-Point Performance Scale: how good it is (Poor to Excellent). Answer: 1 to 5.</summary>
    public const string Performance = "Performance";

    /// <summary>Binary scale: Yes or No.</summary>
    public const string Binary = "Binary";

    /// <summary>Ranking scale: the student puts the options in order, the best first.</summary>
    public const string Ranking = "Ranking";

    /// <summary>Frequency Scale: how often (Never to Always). Answer: 1 to 5.</summary>
    public const string Frequency = "Frequency";

    /// <summary>Select-All-That-Apply: the student ticks every option that fits (none is fine).</summary>
    public const string SelectAll = "SelectAll";

    /// <summary>The student writes a few words.</summary>
    public const string Comment = "Comment";

    public static readonly string[] All = [Likert, Performance, Frequency, Binary, Ranking, SelectAll, Comment];

    /// <summary>The questions answered with 1 to 5. Their average is a teacher's result.</summary>
    public static bool IsScale(string kind) => kind is Likert or Performance or Frequency;

    /// <summary>The questions that come with a list of options written by the coordinator.</summary>
    public static bool HasOptions(string kind) => kind is Ranking or SelectAll;

    public const int MinOptions = 2;
    public const int MaxOptions = 10;

    /// <summary>The name of a kind of question, as the coordinator picks it.</summary>
    public static string Display(string kind) => kind switch
    {
        Likert => "5-Point Likert Scale",
        Performance => "5-Point Performance Scale",
        Frequency => "Frequency Scale",
        Binary => "Binary Scale",
        Ranking => "Ranking Scale",
        SelectAll => "Select-All-That-Apply",
        _ => "Comment",
    };

    /// <summary>What the students see, in a few words.</summary>
    public static string Describe(string kind) => kind switch
    {
        Likert => "Strongly disagree to Strongly agree",
        Performance => "Poor to Excellent",
        Frequency => "Never to Always",
        Binary => "Yes or No",
        Ranking => "Put the options in order, the best first",
        SelectAll => "Tick every option that fits",
        _ => "A few words, in their own words",
    };

    public static string Icon(string kind) => kind switch
    {
        Likert => "list-checks",
        Performance => "star",
        Frequency => "history",
        Binary => "check-check",
        Ranking => "list-ordered",
        SelectAll => "clipboard-list",
        _ => "message-square-text",
    };

    /// <summary>The five labels of a scale question, from 1 to 5. Empty for the other kinds.</summary>
    public static IReadOnlyList<string> ScaleLabels(string kind) => kind switch
    {
        Likert => ["Strongly disagree", "Disagree", "Neutral", "Agree", "Strongly agree"],
        Performance => ["Poor", "Fair", "Good", "Very good", "Excellent"],
        Frequency => ["Never", "Rarely", "Sometimes", "Often", "Always"],
        _ => [],
    };

    /// <summary>What a number of a scale question means ("Agree" for 4 on a Likert scale).</summary>
    public static string ScaleLabel(string kind, int rating) =>
        ScaleLabels(kind) is { Count: 5 } labels && rating is >= 1 and <= 5 ? labels[rating - 1] : "";

    /// <summary>What each number of the performance scale means.</summary>
    public static string RatingLabel(int rating) => ScaleLabel(Performance, rating);

    /// <summary>How an average of the ratings reads.</summary>
    public static string AverageLabel(decimal average) => average switch
    {
        >= 4.5m => "Outstanding",
        >= 3.5m => "Very satisfactory",
        >= 2.5m => "Satisfactory",
        >= 1.5m => "Fair",
        _ => "Needs improvement",
    };

    public static IReadOnlyList<string> OptionsOf(string? optionsText) =>
        (optionsText ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public const string Yes = "Yes";
    public const string No = "No";
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
    public string Kind { get; set; } = EvaluationQuestionKinds.Likert;

    /// <summary>The options of a ranking or select-all question, one in each line.</summary>
    public string? OptionsText { get; set; }

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

    /// <summary>1 to 5 (the scale questions: Likert, Performance and Frequency).</summary>
    public byte? Rating { get; set; }

    /// <summary>
    /// The choice of the student for the other questions: "Yes" or "No" (binary); the numbers of the options (0 is the first one) in the
    /// order the student ranked them, the best first, like "2,0,1,3" (ranking); or the numbers of the options ticked, like "0,2" (select-all).
    /// </summary>
    public string? Selection { get; set; }

    /// <summary>The words of the student (comment questions).</summary>
    public string? Comment { get; set; }
}
