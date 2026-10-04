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

/// <summary>A senior high school strand: BUSSENT, EDUC, ENG or MED (ACAD), CD1, HT1, HT2 or USP (TECH-PRO).</summary>
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
}

/// <summary>The part of the day a class meets: AM (morning), MID (midday) or PM (afternoon).</summary>
public class Schedule : AuditableEntity
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

/// <summary>A schedule a strand runs in. BUSSENT has AM and PM classes, HT2 only PM, HT1 all three.</summary>
public class StrandSchedule
{
    public int StrandId { get; set; }
    public Strand Strand { get; set; } = null!;

    public int ScheduleId { get; set; }
    public Schedule Schedule { get; set; } = null!;
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

    /// <summary>
    /// When the section meets (AM, MID or PM). It must be one of the strand's schedules. Empty only for sections made
    /// before schedules existed.
    /// </summary>
    public int? ScheduleId { get; set; }
    public Schedule? Schedule { get; set; }

    /// <summary>"A", "Newton", ...</summary>
    public string Name { get; set; } = "";

    public int? AdviserTeacherId { get; set; }
    public Teacher? Adviser { get; set; }
}

/// <summary>
/// A subject taught to one section during one term by one teacher. This is "the class" that
/// lessons, assignments and quizzes will hang off later.
/// </summary>
public class ClassOffering : AuditableEntity
{
    /// <summary>Must equal the school year of both the term and the section, the database enforces it.</summary>
    public int SchoolYearId { get; set; }

    public int TermId { get; set; }
    public Term Term { get; set; } = null!;

    public int SectionId { get; set; }
    public Section Section { get; set; } = null!;

    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;

    public int? TeacherId { get; set; }
    public Teacher? Teacher { get; set; }
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
