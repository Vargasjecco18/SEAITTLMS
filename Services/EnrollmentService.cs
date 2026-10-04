using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MySqlConnector;
using SEAITT_LMS.Auth;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>Settings for enrolling students, from the "Enrollment" section of appsettings.json.</summary>
public sealed class EnrollmentOptions
{
    public const string SectionName = "Enrollment";

    /// <summary>What comes after the @ in the email made for a new student.</summary>
    public string EmailDomain { get; set; } = "legazpi.seaitt.edu.ph";
}

// What the enrollment form offers to choose from.
public sealed record TermOption(int Id, string Name, byte TermNumber, string SchoolYear, bool IsCurrent);
public sealed record TrackOption(int Id, string Code, string Name);
public sealed record ScheduleOption(int Id, string Code, string Name);
public sealed record StrandOption(int Id, string Code, string Name, int TrackId, IReadOnlyList<int> ScheduleIds);
public sealed record SubjectOption(int Id, string Code, string Name);

public sealed record EnrollmentLookups(
    IReadOnlyList<TermOption> Terms,
    IReadOnlyList<TrackOption> Tracks,
    IReadOnlyList<StrandOption> Strands,
    IReadOnlyList<ScheduleOption> Schedules);

/// <summary>A student who already has an account (enrolled in an earlier term, for example).</summary>
public sealed record ExistingStudent(
    int StudentId, string FullName, string FirstName, string? MiddleName, string LastName, string Email, IReadOnlyList<int> EnrolledTermIds);

/// <param name="SubjectIds">Picked from the curriculum of the strand for that grade level and term.</param>
/// <param name="SectionName">Like "A". It is created when the strand, grade level and schedule don't have it yet.</param>
public sealed record EnrollmentRequest(
    string Lrn,
    string FirstName,
    string? MiddleName,
    string LastName,
    byte GradeLevel,
    int TermId,
    int TrackId,
    int StrandId,
    int ScheduleId,
    IReadOnlyCollection<int> SubjectIds,
    string SectionName);

/// <param name="NewAccount">True when this enrollment created the student's account. The password is only filled then.</param>
public sealed record EnrollmentReceipt(
    int EnrollmentId,
    int StudentId,
    string FullName,
    string Lrn,
    string SchoolYear,
    string Term,
    byte GradeLevel,
    string Track,
    string Strand,
    string Schedule,
    string Section,
    IReadOnlyList<string> Subjects,
    bool NewAccount,
    string Email,
    string? Password);

public sealed record EnrollmentResult(EnrollmentReceipt? Receipt, IReadOnlyList<string> Errors)
{
    public bool Succeeded => Receipt is not null;

    public static EnrollmentResult Fail(params IReadOnlyList<string> errors) => new(null, errors);
}

/// <summary>
/// Enrolls a student for a term. A student who is not in the system yet gets an account (email and password made from
/// the last name and the LRN, see <see cref="StudentCredentials"/>); a student who already has one only gets the new
/// enrollment. Everything is written in one transaction, so an enrollment never ends up half done.
/// </summary>
public sealed partial class EnrollmentService(
    IDbContextFactory<AppDbContext> dbFactory,
    PasswordHasher hasher,
    IOptions<EnrollmentOptions> options)
{
    [GeneratedRegex(@"^[\p{L}\p{N}][\p{L}\p{N} \-]*$")]
    private static partial Regex SectionNamePattern();

    private const int MaxSectionNameLength = 50;

    /// <summary>What the student's login will be, for showing it while the form is filled in. A taken email is only noticed on Enroll.</summary>
    public StudentLogin PreviewLogin(string? lastName, string lrn) =>
        StudentCredentials.Create(lastName, lrn, options.Value.EmailDomain);

    public async Task<EnrollmentLookups> GetLookupsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var terms = await db.Terms.AsNoTracking()
            .OrderByDescending(t => t.SchoolYear.StartDate).ThenBy(t => t.TermNumber)
            .Select(t => new TermOption(t.Id, t.Name, t.TermNumber, t.SchoolYear.Name, t.IsCurrent))
            .ToListAsync(ct);

        var tracks = await db.Tracks.AsNoTracking().Where(t => t.IsActive).OrderBy(t => t.Id)
            .Select(t => new TrackOption(t.Id, t.Code, t.Name))
            .ToListAsync(ct);

        var schedules = await db.Schedules.AsNoTracking().OrderBy(s => s.Id)
            .Select(s => new ScheduleOption(s.Id, s.Code, s.Name))
            .ToListAsync(ct);

        var strandRows = await db.Strands.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Id)
            .Select(s => new { s.Id, s.Code, s.Name, s.TrackId })
            .ToListAsync(ct);
        var links = await db.StrandSchedules.AsNoTracking()
            .Select(x => new { x.StrandId, x.ScheduleId })
            .ToListAsync(ct);

        var strands = strandRows
            .Select(s => new StrandOption(
                s.Id, s.Code, s.Name, s.TrackId,
                links.Where(l => l.StrandId == s.Id).Select(l => l.ScheduleId).Order().ToList()))
            .ToList();

        return new EnrollmentLookups(terms, tracks, strands, schedules);
    }

    /// <summary>The subjects a strand takes in a term of a grade level (the curriculum), to pick a student's subjects from.</summary>
    public async Task<IReadOnlyList<SubjectOption>> GetCurriculumAsync(int strandId, byte gradeLevel, int termId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var termNumber = await db.Terms.AsNoTracking().Where(t => t.Id == termId).Select(t => (byte?)t.TermNumber).FirstOrDefaultAsync(ct);
        if (termNumber is null)
        {
            return [];
        }

        return await db.StrandSubjects.AsNoTracking()
            .Where(x => x.StrandId == strandId && x.GradeLevel == gradeLevel && x.TermNumber == termNumber)
            .OrderBy(x => x.Subject.Name)
            .Select(x => new SubjectOption(x.Subject.Id, x.Subject.Code, x.Subject.Name))
            .ToListAsync(ct);
    }

    /// <summary>The sections that already exist for this strand, grade level and schedule in the term's school year.</summary>
    public async Task<IReadOnlyList<string>> GetSectionNamesAsync(int termId, int strandId, byte gradeLevel, int scheduleId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var schoolYearId = await db.Terms.AsNoTracking().Where(t => t.Id == termId).Select(t => (int?)t.SchoolYearId).FirstOrDefaultAsync(ct);
        if (schoolYearId is null)
        {
            return [];
        }

        return await db.Sections.AsNoTracking()
            .Where(s => s.SchoolYearId == schoolYearId && s.StrandId == strandId && s.GradeLevel == gradeLevel && s.ScheduleId == scheduleId)
            .OrderBy(s => s.Name)
            .Select(s => s.Name)
            .ToListAsync(ct);
    }

    public async Task<ExistingStudent?> FindStudentAsync(string? lrn, CancellationToken ct = default)
    {
        lrn = lrn?.Trim();
        if (lrn is not { Length: 12 } || !lrn.All(char.IsAsciiDigit))
        {
            return null;
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var student = await db.Students.AsNoTracking()
            .Where(s => s.Lrn == lrn)
            .Select(s => new { s.Id, s.FirstName, s.MiddleName, s.LastName, s.NameExtension, s.User.Email })
            .FirstOrDefaultAsync(ct);
        if (student is null)
        {
            return null;
        }

        var termIds = await db.Enrollments.AsNoTracking().Where(e => e.StudentId == student.Id).Select(e => e.TermId).ToListAsync(ct);
        var fullName = string.Join(' ', new[] { student.FirstName, student.MiddleName, student.LastName, student.NameExtension }.Where(p => !string.IsNullOrWhiteSpace(p)));
        return new ExistingStudent(student.Id, fullName, student.FirstName, student.MiddleName, student.LastName, student.Email, termIds);
    }

    public async Task<EnrollmentResult> EnrollAsync(EnrollmentRequest request, int actingUserId, CancellationToken ct = default)
    {
        var errors = new List<string>();

        var lrn = (request.Lrn ?? "").Trim();
        if (lrn.Length != 12 || !lrn.All(char.IsAsciiDigit))
        {
            errors.Add("The LRN must be exactly 12 digits.");
        }

        var firstName = Clean(request.FirstName);
        var middleName = Clean(request.MiddleName);
        var lastName = Clean(request.LastName);
        if (firstName.Length is 0 or > 100)
        {
            errors.Add("Enter the first name (up to 100 characters).");
        }

        if (lastName.Length is 0 or > 100)
        {
            errors.Add("Enter the last name (up to 100 characters).");
        }

        if (middleName.Length > 100)
        {
            errors.Add("The middle name can have at most 100 characters.");
        }

        if (request.GradeLevel is not (11 or 12))
        {
            errors.Add("Pick grade level 11 or 12.");
        }

        var sectionName = Clean(request.SectionName);
        if (sectionName.Length is 0 or > MaxSectionNameLength || !SectionNamePattern().IsMatch(sectionName))
        {
            errors.Add("Enter the section, for example A. Use letters, numbers, spaces and dashes (up to 50 characters).");
        }

        if (errors.Count > 0)
        {
            return EnrollmentResult.Fail(errors);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        // The page already limits who can get here. This checks it again, against the database, at the moment of saving.
        var actor = await db.Users.AsNoTracking().Where(u => u.Id == actingUserId)
            .Select(u => new { u.IsActive, u.RoleId })
            .FirstOrDefaultAsync(ct);
        if (actor is null || !actor.IsActive || (actor.RoleId != AppRoles.RegistrarId && actor.RoleId != AppRoles.AdminId))
        {
            return EnrollmentResult.Fail("You don't have permission to enroll students.");
        }

        var term = await db.Terms.AsNoTracking().Include(t => t.SchoolYear).FirstOrDefaultAsync(t => t.Id == request.TermId, ct);
        var strand = await db.Strands.AsNoTracking().Include(s => s.Track).FirstOrDefaultAsync(s => s.Id == request.StrandId, ct);
        var schedule = await db.Schedules.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.ScheduleId, ct);

        if (term is null)
        {
            errors.Add("Pick the term.");
        }

        if (strand is null || !strand.IsActive || strand.TrackId != request.TrackId || !strand.Track.IsActive)
        {
            errors.Add("Pick a track and one of its strands.");
        }

        if (schedule is null)
        {
            errors.Add("Pick the schedule.");
        }
        else if (strand is not null && !await db.StrandSchedules.AnyAsync(x => x.StrandId == strand.Id && x.ScheduleId == schedule.Id, ct))
        {
            errors.Add($"{strand.Code} has no {schedule.Code} classes. Pick one of its schedules.");
        }

        if (errors.Count > 0)
        {
            return EnrollmentResult.Fail(errors);
        }

        // The subjects must come from the curriculum of this strand, grade level and term. When the curriculum has
        // subjects, at least one must be picked.
        var curriculum = await db.StrandSubjects.AsNoTracking()
            .Where(x => x.StrandId == strand!.Id && x.GradeLevel == request.GradeLevel && x.TermNumber == term!.TermNumber)
            .Select(x => x.Subject)
            .OrderBy(s => s.Name)
            .ToListAsync(ct);

        var wanted = (request.SubjectIds ?? []).Distinct().ToList();
        if (wanted.Any(id => curriculum.All(s => s.Id != id)))
        {
            errors.Add("One of the subjects is not part of this strand's subjects for that grade level and term.");
        }
        else if (curriculum.Count > 0 && wanted.Count == 0)
        {
            errors.Add("Pick at least one subject.");
        }

        if (errors.Count > 0)
        {
            return EnrollmentResult.Fail(errors);
        }

        var subjects = curriculum.Where(s => wanted.Contains(s.Id)).ToList();

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            // ---- the student: an existing account, or a new one with the automatic email and password
            var student = await db.Students.Include(s => s.User).FirstOrDefaultAsync(s => s.Lrn == lrn, ct);
            StudentLogin? newLogin = null;

            if (student is not null)
            {
                if (await db.Enrollments.AnyAsync(e => e.StudentId == student.Id && e.TermId == term!.Id, ct))
                {
                    return EnrollmentResult.Fail($"{student.FullName} is already enrolled for the {term!.Name} of {term.SchoolYear.Name}.");
                }

                // The name stays what the system already has. Changing it is for the administrator (user management).
                firstName = student.FirstName;
                middleName = student.MiddleName ?? "";
                lastName = student.LastName;
            }
            else
            {
                newLogin = await MakeLoginAsync(db, lastName, lrn, ct);
                if (newLogin is null)
                {
                    return EnrollmentResult.Fail("An email for this student could not be made, because the ones made from this last name and LRN are all taken.");
                }

                student = new Student
                {
                    User = new User
                    {
                        Email = newLogin.Email,
                        PasswordHash = hasher.Hash(newLogin.Password),
                        RoleId = AppRoles.StudentId,
                        IsActive = true,
                        SecurityStamp = Guid.NewGuid().ToString("N"),
                    },
                    Lrn = lrn,
                    FirstName = firstName,
                    MiddleName = NullIfBlank(middleName),
                    LastName = lastName,
                };
                db.Students.Add(student);
            }

            // ---- the section: it is created the first time somebody is enrolled in it
            var section = await db.Sections.FirstOrDefaultAsync(s =>
                s.SchoolYearId == term!.SchoolYearId && s.StrandId == strand!.Id && s.GradeLevel == request.GradeLevel
                && s.ScheduleId == schedule!.Id && s.Name == sectionName, ct);
            if (section is null)
            {
                section = new Section
                {
                    SchoolYearId = term!.SchoolYearId,
                    StrandId = strand!.Id,
                    GradeLevel = request.GradeLevel,
                    ScheduleId = schedule!.Id,
                    Name = sectionName,
                };
                db.Sections.Add(section);
            }

            await db.SaveChangesAsync(ct); // gives the student and the section their ids

            // ---- the enrollment, with the student's details as they are today and the chosen subjects
            var enrollment = new Enrollment
            {
                SchoolYearId = term!.SchoolYearId,
                TermId = term.Id,
                SectionId = section.Id,
                StudentId = student.Id,
                Lrn = lrn,
                FirstName = firstName,
                MiddleName = NullIfBlank(middleName),
                LastName = lastName,
                GradeLevel = request.GradeLevel,
                TrackId = strand!.TrackId,
                StrandId = strand.Id,
                Subjects = subjects.Count == 0 ? null : string.Join(", ", subjects.Select(s => s.Name)),
                Status = EnrollmentStatus.Enrolled,
                EnrolledByUserId = actingUserId,
                EnrolledSubjects = subjects.Select(s => new EnrollmentSubject { SubjectId = s.Id }).ToList(),
            };
            db.Enrollments.Add(enrollment);

            // The short summary on the student's own row follows the newest enrollment.
            student.GradeLevel = request.GradeLevel;
            student.TrackId = strand.TrackId;
            student.StrandId = strand.Id;
            student.SectionId = section.Id;

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            var fullName = string.Join(' ', new[] { firstName, NullIfBlank(middleName), lastName }.Where(p => !string.IsNullOrWhiteSpace(p)));
            return new EnrollmentResult(
                new EnrollmentReceipt(
                    enrollment.Id, student.Id, fullName, lrn, term.SchoolYear.Name, term.Name, request.GradeLevel,
                    strand.Track.Name, strand.Code, schedule!.Code, $"{request.GradeLevel}-{strand.Code}-{section.Name}",
                    subjects.Select(s => s.Name).ToList(),
                    NewAccount: newLogin is not null, newLogin?.Email ?? student.User.Email, newLogin?.Password),
                []);
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { ErrorCode: MySqlErrorCode.DuplicateKeyEntry } duplicate)
        {
            // Somebody else saved the same student, email or section a moment earlier.
            var text = duplicate.Message;
            return EnrollmentResult.Fail(
                text.Contains("lrn", StringComparison.OrdinalIgnoreCase) || text.Contains("email", StringComparison.OrdinalIgnoreCase)
                    ? "This student was just added by someone else. Enter the LRN again to continue."
                    : text.Contains("student_id", StringComparison.OrdinalIgnoreCase)
                        ? "This student is already enrolled for that term."
                        : "Someone else saved the same thing a moment ago. Please try again.");
        }
    }

    /// <summary>The first login, from the 6 digits of the LRN on, whose email nobody has yet.</summary>
    private async Task<StudentLogin?> MakeLoginAsync(AppDbContext db, string lastName, string lrn, CancellationToken ct)
    {
        for (var digits = StudentCredentials.DefaultDigits; digits <= StudentCredentials.MaxDigits; digits++)
        {
            var login = StudentCredentials.Create(lastName, lrn, options.Value.EmailDomain, digits);
            if (!await db.Users.AnyAsync(u => u.Email == login.Email, ct))
            {
                return login;
            }
        }

        return null;
    }

    /// <summary>Trimmed, with every run of spaces made into one space. Null becomes "".</summary>
    private static string Clean(string? text) =>
        string.Join(' ', (text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static string? NullIfBlank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;
}
