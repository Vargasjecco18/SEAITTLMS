namespace SEAITT_LMS.Data.Entities;

/// <summary>A school year such as "2025-2026".</summary>
public class SchoolYear : AuditableEntity
{
    public string Name { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public List<Term> Terms { get; set; } = [];
}

/// <summary>
/// A grading/enrollment period inside a school year (1st Term, 2nd Term, ...).
/// SEAITT uses terms instead of semesters.
/// </summary>
public class Term : AuditableEntity
{
    public int SchoolYearId { get; set; }
    public SchoolYear SchoolYear { get; set; } = null!;

    /// <summary>1, 2, 3 ... within the school year.</summary>
    public byte TermNumber { get; set; }

    public string Name { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    /// <summary>The term the school is running right now. The app keeps this true for at most one term.</summary>
    public bool IsCurrent { get; set; }
}

/// <summary>
/// A senior high school track. SEAITT offers two: ACAD (Academic) and TECH-PRO (Technical-Professional).
/// Every strand belongs to exactly one track.
/// </summary>
public class Track : AuditableEntity
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";

    /// <summary>Turn this off for a track the school does not offer, instead of deleting it.</summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>A senior high school strand: BUSSENT, EDUC, ENG, MED or USP (ACAD), CD1, HT1 or HT2 (TECH-PRO).</summary>
public class Strand : AuditableEntity
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";

    public int TrackId { get; set; }
    public Track Track { get; set; } = null!;

    public bool IsActive { get; set; } = true;
}

public class Subject : AuditableEntity
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>The units of the course (credit units), 0.5 to 12. It is printed on the enrollment form.</summary>
    public decimal Units { get; set; } = 3;

    /// <summary>When the academic provider marked the course of the library as Complete (its syllabus, modules and exam are ready). Null: not complete, so it is on the main page of the library; a complete course is only under Complete.</summary>
    public DateTime? LibraryCompletedAtUtc { get; set; }
}

/// <summary>The part of the day a class meets: AM (morning), MID (midday) or PM (afternoon), with the hours of that part of the day. A course of a section is given one of them.</summary>
public class Schedule : AuditableEntity
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";

    /// <summary>When the part of the day starts (Philippine time). The class time of a course in this part of the day must be inside it.</summary>
    public TimeOnly StartTime { get; set; }

    /// <summary>When the part of the day ends.</summary>
    public TimeOnly EndTime { get; set; }
}

/// <summary>A room where classes meet ("Room 101", "Computer lab"). Each room has its own class schedule: two classes can't be in one room at the same time.</summary>
public class Room : AuditableEntity
{
    public string Name { get; set; } = "";
}

/// <summary>
/// The days a course meets. A course is put on the week as MWF (Monday, Wednesday, Friday) or TTH (Tuesday, Thursday), and after that each day can be moved by itself, so the days of one
/// row of the schedule are any of the five days written in the order of the week: "MWF", "TTH", "MF", "W", "TH" ... (M Monday, T Tuesday, W Wednesday, TH Thursday, F Friday).
/// </summary>
public static class MeetingDays
{
    public const string Mwf = "MWF";
    public const string Tth = "TTH";

    /// <summary>What can be chosen for a whole course in a list (the other days come from moving one day by itself).</summary>
    public static readonly IReadOnlyList<string> All = [Mwf, Tth];

    /// <summary>MWF or TTH: the two patterns a course starts with.</summary>
    public static bool IsValid(string? days) => days is Mwf or Tth;

    private static readonly string[] Codes = ["M", "T", "W", "TH", "F"];

    private static readonly string[] Names = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"];

    /// <summary>The days (Monday = 1 ... Friday = 5) a text stands for, in the order of the week; an empty list when the text is not made of the day codes.</summary>
    public static IReadOnlyList<int> Parse(string? days)
    {
        var found = new List<int>();
        var text = days ?? "";
        for (var i = 0; i < text.Length; i++)
        {
            switch (text[i])
            {
                case 'M': found.Add(1); break;
                case 'W': found.Add(3); break;
                case 'F': found.Add(5); break;
                case 'T' when i + 1 < text.Length && text[i + 1] == 'H': found.Add(4); i++; break;
                case 'T': found.Add(2); break;
                default: return [];
            }
        }

        return [.. found.Distinct().Order()];
    }

    /// <summary>The text of some days (1 to 5): "MWF", "TTH", "MF", "W" ... in the order of the week. Empty when there are none.</summary>
    public static string Format(IEnumerable<int> days) => string.Concat(days.Where(d => d is >= 1 and <= 5).Distinct().Order().Select(d => Codes[d - 1]));

    /// <summary>True when the text is a set of days written the one way (in the order of the week, each day once).</summary>
    public static bool IsCanonical(string? days) => !string.IsNullOrEmpty(days) && Format(Parse(days)) == days;

    public static bool Overlap(string? a, string? b) => Parse(a).Intersect(Parse(b)).Any();

    public static bool Has(string? days, int day) => Parse(days).Contains(day);

    public static string Without(string days, int day) => Format(Parse(days).Where(d => d != day));

    public static string With(string days, int day) => Format(Parse(days).Append(day));

    /// <summary>The pattern a day starts with when a course is put on the week there: Monday, Wednesday and Friday give MWF, Tuesday and Thursday give TTH.</summary>
    public static string PatternOf(int day) => day is 1 or 3 or 5 ? Mwf : Tth;

    /// <summary>"Monday, Wednesday, Friday" for MWF.</summary>
    public static string LongName(string days) => string.Join(", ", Parse(days).Select(d => Names[d - 1]));

    /// <summary>The days of the week of some days, for the calendar.</summary>
    public static IReadOnlyList<DayOfWeek> Days(string days) => [.. Parse(days).Select(d => (DayOfWeek)(d % 7))];
}

/// <summary>
/// When a course meets for a section: the part of the day (AM, MID or PM), the days (MWF or TTH) and the time, inside the hours of that part of the day.
/// It belongs to the section and the course, not to the teacher, so the academic coordinator can set it before a teacher is given the course.
/// </summary>
public class CourseSchedule : AuditableEntity
{
    public int SectionId { get; set; }
    public Section Section { get; set; } = null!;

    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;

    /// <summary>AM, MID or PM: follows the time the class starts (before noon AM, the hour of noon MID, after that PM).</summary>
    public int ScheduleId { get; set; }
    public Schedule Schedule { get; set; } = null!;

    /// <summary>One of <see cref="MeetingDays"/>.</summary>
    public string Days { get; set; } = MeetingDays.Mwf;

    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    /// <summary>The room the class meets in; null when no room was chosen yet.</summary>
    public int? RoomId { get; set; }
    public Room? Room { get; set; }
}

/// <summary>
/// The curriculum: a subject a strand takes in one term of a grade level, for example "BUSINESS ACCOUNTING for BUSSENT,
/// grade 11, 1st term". It is the same every school year. When a student is enrolled, the subjects are picked from here.
/// </summary>
public class StrandSubject
{
    public int StrandId { get; set; }
    public Strand Strand { get; set; } = null!;

    /// <summary>11 or 12.</summary>
    public byte GradeLevel { get; set; }

    /// <summary>1 for the 1st term, 2 for the 2nd term, ...</summary>
    public byte TermNumber { get; set; }

    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;

    /// <summary>Offered: the registrar enrolls students in this course of the strand and it is in the class schedule. Unchecked, it stays in the curriculum but is left out.</summary>
    public bool IsOffered { get; set; } = true;
}

/// <summary>The section names the registrar can choose when enrolling a student: A, B and C. (Sections with other names can exist; the registrar can still pick those.)</summary>
public static class SectionNames
{
    public static readonly IReadOnlyList<string> Choices = ["A", "B", "C"];

    public static bool IsChoice(string name) => Choices.Contains(name, StringComparer.Ordinal);
}

/// <summary>A group of students, e.g. "11-BUSSENT-A" (grade 11, strand BUSSENT, section A) for school year 2026-2027.</summary>
public class Section : AuditableEntity
{
    public int SchoolYearId { get; set; }
    public SchoolYear SchoolYear { get; set; } = null!;

    public int StrandId { get; set; }
    public Strand Strand { get; set; } = null!;

    /// <summary>11 or 12.</summary>
    public byte GradeLevel { get; set; }

    /// <summary>"A", "Newton", ... A section is the school year, the strand, the grade level and a name (the part of the day belongs to each course, not to the section).</summary>
    public string Name { get; set; } = "";

    public int? AdviserTeacherId { get; set; }
    public Teacher? Adviser { get; set; }
}

public enum EnrollmentStatus
{
    Enrolled,
    Dropped,
    Completed,
}

/// <summary>A student placed in a section for one term. One enrollment per student per term.</summary>
public class Enrollment : AuditableEntity
{
    /// <summary>Must equal the school year of both the term and the section, the database enforces it.</summary>
    public int SchoolYearId { get; set; }

    public int TermId { get; set; }
    public Term Term { get; set; } = null!;

    public int SectionId { get; set; }
    public Section Section { get; set; } = null!;

    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    // The student as they were when they were enrolled. They are copied from the student and the registrar's choices,
    // so a row of this table reads on its own and later changes (a new name, a promotion) don't rewrite history.
    // The database makes sure the strand belongs to the track and that the section is the strand's and the grade level's.

    /// <summary>The student's Learner Reference Number, 12 digits.</summary>
    public string Lrn { get; set; } = "";

    public string FirstName { get; set; } = "";
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = "";

    /// <summary>Jr., Sr., III ... (optional).</summary>
    public string? NameExtension { get; set; }

    /// <summary>11 or 12.</summary>
    public byte GradeLevel { get; set; }

    public int TrackId { get; set; }
    public Track Track { get; set; } = null!;

    public int StrandId { get; set; }
    public Strand Strand { get; set; } = null!;

    /// <summary>
    /// The names of the enrolled subjects, separated by commas, for reading the table. The same subjects are the rows of
    /// <see cref="EnrolledSubjects"/> (enrollment_subjects), which is what the program uses.
    /// </summary>
    public string? Subjects { get; set; }

    public List<EnrollmentSubject> EnrolledSubjects { get; set; } = [];

    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Enrolled;

    /// <summary>The registrar (user) who enrolled the student.</summary>
    public int? EnrolledByUserId { get; set; }
    public User? EnrolledBy { get; set; }
}

/// <summary>One subject a student takes in an enrollment.</summary>
public class EnrollmentSubject
{
    public int EnrollmentId { get; set; }
    public Enrollment Enrollment { get; set; } = null!;

    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
}
