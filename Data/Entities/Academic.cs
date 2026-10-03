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

/// <summary>A senior high school strand such as STEM, ABM, HUMSS, GAS or a TVL specialization.</summary>
public class Strand : AuditableEntity
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";

    /// <summary>Academic, TVL, Sports, Arts and Design, ...</summary>
    public string? Track { get; set; }

    public bool IsActive { get; set; } = true;
}

public class Subject : AuditableEntity
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>A group of students, e.g. "11-STEM-A" for school year 2025-2026.</summary>
public class Section : AuditableEntity
{
    public int SchoolYearId { get; set; }
    public SchoolYear SchoolYear { get; set; } = null!;

    public int StrandId { get; set; }
    public Strand Strand { get; set; } = null!;

    /// <summary>11 or 12.</summary>
    public byte GradeLevel { get; set; }

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

    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Enrolled;

    /// <summary>The registrar (user) who enrolled the student.</summary>
    public int? EnrolledByUserId { get; set; }
    public User? EnrolledBy { get; set; }
}
