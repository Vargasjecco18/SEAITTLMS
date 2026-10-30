using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SEAITT_LMS.Auth;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>One account in a role's list.</summary>
/// <param name="Placement">For students: the section as kept on the student's row (or, when that is empty, of their latest enrollment), like 11-BUSSENT-A. For teachers: the sections they teach in, like "11-BUSSENT-A, 12-HT1-A". Empty otherwise.</param>
/// <param name="Courses">For students: the courses of their enrollment (the current term's, else the latest). For teachers: each course they teach with its section, like "BUSINESS ACCOUNTING · 11-BUSSENT-A". Empty otherwise.</param>
public sealed record UserListItem(
    int UserId,
    string Role,
    string FullName,
    string Email,
    string? PhoneNumber,
    string? EmployeeNumber,
    string? Lrn,
    bool IsActive,
    DateTime? LastLoginAtUtc,
    DateTime CreatedAtUtc,
    string? Placement,
    IReadOnlyList<string>? Courses = null,
    string? StudentStatus = null,
    string? RoleTitle = null);

public sealed record UserPage(IReadOnlyList<UserListItem> Items, int Total, int Page, int PageSize)
{
    public int PageCount => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

/// <summary>The form for editing an account. Which fields are used depends on the role.</summary>
public sealed class UserEdit
{
    public int UserId { get; set; }

    public string Role { get; set; } = "";

    [Required(ErrorMessage = "Enter the first name."), StringLength(100, ErrorMessage = "At most 100 characters.")]
    public string FirstName { get; set; } = "";

    [StringLength(100, ErrorMessage = "At most 100 characters.")]
    public string? MiddleName { get; set; }

    [Required(ErrorMessage = "Enter the last name."), StringLength(100, ErrorMessage = "At most 100 characters.")]
    public string LastName { get; set; } = "";

    [StringLength(10, ErrorMessage = "At most 10 characters.")]
    public string? NameExtension { get; set; }

    [Required(ErrorMessage = "Enter the email."), StringLength(254)]
    public string Email { get; set; } = "";

    public string? PhoneNumber { get; set; }

    /// <summary>Teachers, administrators, registrars and guidance counselors.</summary>
    [StringLength(30, ErrorMessage = "At most 30 characters.")]
    public string? EmployeeNumber { get; set; }

    /// <summary>Students only.</summary>
    public string? Lrn { get; set; }

    /// <summary>Students only: the id of the student (not of the account) and the status now (see <see cref="StudentStatus"/>).</summary>
    public int? StudentId { get; set; }

    public string? StudentStatus { get; set; }

    /// <summary>Whether the account can sign in. For staff it also sets their status (Active or Inactive).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Leave empty to keep the password. Otherwise the account gets this new password.</summary>
    public string? NewPassword { get; set; }
}

/// <summary>The form for adding a student's account. The email and the password can be empty: they are made from the last name and the LRN.</summary>
public sealed class NewStudentAccount
{
    [Required(ErrorMessage = "Enter the first name."), StringLength(100, ErrorMessage = "At most 100 characters.")]
    public string FirstName { get; set; } = "";

    [StringLength(100, ErrorMessage = "At most 100 characters.")]
    public string? MiddleName { get; set; }

    [Required(ErrorMessage = "Enter the last name."), StringLength(100, ErrorMessage = "At most 100 characters.")]
    public string LastName { get; set; } = "";

    [StringLength(10, ErrorMessage = "At most 10 characters.")]
    public string? NameExtension { get; set; }

    [Required(ErrorMessage = "Enter the LRN."), StringLength(12, MinimumLength = 12, ErrorMessage = "The LRN has 12 digits.")]
    public string Lrn { get; set; } = "";

    public string? Email { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Password { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Where a new teacher works, chosen when the administrator adds the teacher. Everything is optional (0 / empty = not chosen), but what is
/// chosen must agree: the strand is in the track, the section is a section of the strand, and the courses are courses of that strand
/// at the grade level of the section. The courses are given to the teacher for that section.
/// </summary>
public sealed record TeacherPlacement(int TrackId, int StrandId, int SectionId, IReadOnlyList<int> SubjectIds)
{
    public bool IsEmpty => TrackId == 0 && StrandId == 0 && SectionId == 0 && SubjectIds.Count == 0;
}

public sealed record ServiceResult(bool Succeeded, IReadOnlyList<string> Errors, string? Message = null, int? Id = null)
{
    public static ServiceResult Ok(string? message = null, int? id = null) => new(true, [], message, id);

    public static ServiceResult Fail(params IReadOnlyList<string> errors) => new(false, errors);
}

/// <summary>What deleting an account takes with it, shown before the administrator confirms.</summary>
public sealed record DeleteImpact(string FullName, string Role, IReadOnlyList<string> Effects);

/// <summary>
/// What an administrator does with accounts: list them by role, edit them, delete them. Every change checks again, in
/// the database, that the person doing it is an active administrator.
/// </summary>
public sealed class UserManagementService(
    IDbContextFactory<AppDbContext> dbFactory, PasswordHasher hasher, UserAccountService accounts, SecurityLogService? securityLog = null,
    Microsoft.Extensions.Options.IOptions<EnrollmentOptions>? enrollmentOptions = null)
{
    /// <summary>
    /// The administrator adds a student's account without an enrollment (the registrar enrolls the student later, with the same LRN). The email and the password
    /// can be left empty: then they are made from the last name and the LRN, as when the registrar enrolls a new student.
    /// </summary>
    public async Task<ServiceResult> AddStudentAsync(NewStudentAccount student, int actingUserId, CancellationToken ct = default)
    {
        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            if (!await IsActiveAdminAsync(db, actingUserId, ct))
            {
                return ServiceResult.Fail("Only an administrator can add accounts.");
            }
        }

        var lrn = (student.Lrn ?? "").Trim();
        if (lrn.Length != 12 || !lrn.All(char.IsAsciiDigit))
        {
            return ServiceResult.Fail("The LRN must be exactly 12 digits.");
        }

        var firstName = NameCase.Title(student.FirstName);
        var lastName = NameCase.Title(student.LastName);
        if (firstName.Length == 0 || lastName.Length == 0)
        {
            return ServiceResult.Fail("Enter the first name and the last name.");
        }

        await using var check = await dbFactory.CreateDbContextAsync(ct);
        if (await check.Students.AnyAsync(s => s.Lrn == lrn, ct))
        {
            return ServiceResult.Fail("A student with this LRN already exists.");
        }

        var email = string.IsNullOrWhiteSpace(student.Email) ? null : student.Email.Trim();
        var password = string.IsNullOrEmpty(student.Password) ? null : student.Password;
        if (email is null || password is null)
        {
            // The same email and password the registrar's enrollment makes: lastname.123456@domain and lastname123456 (more digits when taken).
            var domain = enrollmentOptions?.Value.EmailDomain ?? new EnrollmentOptions().EmailDomain;
            StudentLogin? made = null;
            for (var digits = StudentCredentials.DefaultDigits; digits <= StudentCredentials.MaxDigits; digits++)
            {
                var attempt = StudentCredentials.Create(lastName, lrn, domain, digits);
                if (!await check.Users.AnyAsync(u => u.Email == attempt.Email, ct))
                {
                    made = attempt;
                    break;
                }
            }

            if (made is null)
            {
                return ServiceResult.Fail("An email for this student could not be made, because the ones made from this last name and LRN are all taken.");
            }

            email ??= made.Email;
            password ??= made.Password;
        }

        var result = await accounts.CreateAsync(new NewAccount(
            AppRoles.Student, email, password, firstName, lastName, NullIfBlank(NameCase.Title(student.MiddleName)), NullIfBlank(NameCase.Title(student.NameExtension)),
            NullIfBlank(student.PhoneNumber), Lrn: lrn, IsActive: student.IsActive), ct);
        if (!result.Succeeded)
        {
            return ServiceResult.Fail(result.Errors);
        }

        if (securityLog is not null)
        {
            await securityLog.AddAsync(SecurityEvents.AccountCreated, result.UserId, details: "A student account was added.", actorUserId: actingUserId, ct: ct);
        }

        return ServiceResult.Ok($"Added {FullName(firstName, NullIfBlank(NameCase.Title(student.MiddleName)), lastName, NullIfBlank(NameCase.Title(student.NameExtension)))}. Email: {email}", result.UserId);
    }

    /// <summary>
    /// Adds an account for a teacher, registrar, guidance counselor or administrator. (Students are added by enrolling them.)
    /// The email must be given; the administrator chooses the first password.
    /// </summary>
    public async Task<ServiceResult> AddStaffAsync(NewAccount account, int actingUserId, CancellationToken ct = default)
    {
        if (account.Role == AppRoles.Student)
        {
            return ServiceResult.Fail("Students are added with Add student, or by enrolling them.");
        }

        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            if (!await IsActiveAdminAsync(db, actingUserId, ct))
            {
                return ServiceResult.Fail("Only an administrator can add accounts.");
            }
        }

        // One administrator, one registrar and one guidance counselor. The check and the creation happen one at a time, so two
        // administrators pressing Add together can't both get through.
        // (A role the administrator made up, like "Assistant Registrar", can have as many people as the school needs.)
        var gate = account.CustomRoleId is null && AppRoles.OnlyOneAllowed(account.Role) ? RoleGates.GetOrAdd(account.Role, _ => new SemaphoreSlim(1, 1)) : null;
        if (gate is not null)
        {
            await gate.WaitAsync(ct);
        }

        try
        {
            if (gate is not null)
            {
                await using var db = await dbFactory.CreateDbContextAsync(ct);
                if (await db.Users.AnyAsync(u => u.RoleId == AppRoles.Ids[account.Role], ct))
                {
                    return ServiceResult.Fail($"There is already {AppRoles.WithArticle(AppRoles.DisplayName(account.Role).ToLowerInvariant())}. The school can have only one.");
                }
            }

            return await CreateStaffAsync(account, actingUserId, ct);
        }
        finally
        {
            gate?.Release();
        }
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> RoleGates = new();

    /// <summary>The roles the administrator made up in Add staff, by name.</summary>
    public async Task<IReadOnlyList<CustomRole>> ListCustomRolesAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.CustomRoles.AsNoTracking().OrderBy(r => r.Name).ToListAsync(ct);
    }

    /// <summary>
    /// Makes a new role for the staff, like "Assistant Registrar". It works like <paramref name="baseRole"/> (one of the roles of the non-teaching staff): the same pages and the same
    /// permissions. The name is 2 to 40 letters, numbers, spaces and dashes, and is not the name of a role that is there already.
    /// </summary>
    public async Task<ServiceResult> AddRoleAsync(string? name, string? baseRole, int actingUserId, CancellationToken ct = default)
    {
        name = string.Join(' ', (name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (name.Length is < 2 or > 40 || !name.All(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '\''))
        {
            return ServiceResult.Fail("Write the name of the role: 2 to 40 letters, numbers, spaces or dashes.");
        }

        if (baseRole is null || !NonTeachingRoles.Contains(baseRole))
        {
            return ServiceResult.Fail("Choose the role it works like.");
        }

        // The name is also not one of the usual roles (with or without the space: "Academic Provider", "AcademicProvider").
        var plain = name.Replace(" ", "");
        if (AppRoles.Ids.Keys.Any(r => string.Equals(r, plain, StringComparison.OrdinalIgnoreCase) || string.Equals(AppRoles.DisplayName(r), name, StringComparison.OrdinalIgnoreCase)) || string.Equals(name, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult.Fail("A role with this name is there already. Choose another name.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsActiveAdminAsync(db, actingUserId, ct))
        {
            return ServiceResult.Fail("Only an administrator can add a role.");
        }

        if (await db.CustomRoles.AnyAsync(r => r.Name == name, ct))
        {
            return ServiceResult.Fail("A role with this name is there already. Choose another name.");
        }

        if (await db.CustomRoles.CountAsync(ct) >= 30)
        {
            return ServiceResult.Fail("There are 30 roles already. That is the most.");
        }

        var role = new CustomRole { Name = name, BaseRole = baseRole };
        db.CustomRoles.Add(role);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Fail("A role with this name is there already. Choose another name.");
        }

        if (securityLog is not null)
        {
            await securityLog.AddAsync(SecurityEvents.AccountCreated, null, details: $"The role {name} was added. It works like {AppRoles.DisplayName(baseRole).ToLowerInvariant()}.", actorUserId: actingUserId, ct: ct);
        }

        return ServiceResult.Ok($"The role {name} was added. It works like {AppRoles.DisplayName(baseRole).ToLowerInvariant()}.", role.Id);
    }

    private async Task<ServiceResult> CreateStaffAsync(NewAccount account, int actingUserId, CancellationToken ct)
    {
        var result = await accounts.CreateAsync(account, ct);
        if (result.Succeeded && securityLog is not null)
        {
            await securityLog.AddAsync(SecurityEvents.AccountCreated, result.UserId, details: $"A {AppRoles.DisplayName(account.Role).ToLowerInvariant()} account was added.", actorUserId: actingUserId, ct: ct);
        }

        return result.Succeeded
            ? ServiceResult.Ok($"Added {FullName(account.FirstName.Trim(), NullIfBlank(account.MiddleName), account.LastName.Trim(), NullIfBlank(account.NameExtension))}.", result.UserId)
            : ServiceResult.Fail(result.Errors);
    }

    private sealed record ResolvedPlacement(int? TrackId, int? StrandId, int? SectionId, byte? GradeLevel, IReadOnlyList<int> SubjectIds);

    /// <summary>
    /// Adds a teacher together with the track, strand, section and courses they work in. The choices are checked first, so a wrong choice
    /// adds nothing. The courses are given to the teacher for the chosen section.
    /// </summary>
    public async Task<ServiceResult> AddTeacherAsync(NewAccount account, TeacherPlacement placement, int actingUserId, CancellationToken ct = default)
    {
        if (account.Role != AppRoles.Teacher)
        {
            return ServiceResult.Fail("This is for adding teachers.");
        }

        ResolvedPlacement? resolved = null;
        if (!placement.IsEmpty)
        {
            var (found, errors) = await ResolvePlacementAsync(placement, ct);
            if (errors.Count > 0)
            {
                return ServiceResult.Fail(errors);
            }

            resolved = found;
        }

        var added = await AddStaffAsync(account, actingUserId, ct);
        if (!added.Succeeded || resolved is null || added.Id is not { } userId)
        {
            return added;
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var teacher = await db.Teachers.FirstOrDefaultAsync(t => t.UserId == userId, ct);
        if (teacher is null)
        {
            return added;
        }

        teacher.TrackId = resolved.TrackId;
        teacher.StrandId = resolved.StrandId;
        teacher.SectionId = resolved.SectionId;
        teacher.GradeLevel = resolved.GradeLevel;
        teacher.SubjectId = resolved.SubjectIds.Count > 0 ? resolved.SubjectIds[0] : null;
        if (resolved.SectionId is { } sectionId)
        {
            foreach (var subjectId in resolved.SubjectIds)
            {
                db.TeachingAssignments.Add(new TeachingAssignment { TeacherId = teacher.Id, SectionId = sectionId, SubjectId = subjectId });
            }
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The account is already there; somebody else took one of the courses a moment ago.
            return ServiceResult.Ok($"{added.Message} The courses could not be given because somebody just gave one of them to another teacher. Open Courses to give them.", userId);
        }

        return ServiceResult.Ok(
            resolved.SubjectIds.Count == 0 ? added.Message : $"{added.Message} {(resolved.SubjectIds.Count == 1 ? "1 course" : $"{resolved.SubjectIds.Count} courses")} given.", userId);
    }

    /// <summary>Checks what was chosen for a new teacher and works out the rest (the track of the strand, the strand and grade level of the section).</summary>
    private async Task<(ResolvedPlacement? Placement, List<string> Errors)> ResolvePlacementAsync(TeacherPlacement p, CancellationToken ct)
    {
        var errors = new List<string>();
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var section = p.SectionId == 0 ? null : await db.Sections.AsNoTracking().Where(s => s.Id == p.SectionId)
            .Select(s => new { s.Id, s.StrandId, s.GradeLevel, TrackId = s.Strand.TrackId, StrandCode = s.Strand.Code, s.Name }).FirstOrDefaultAsync(ct);
        if (p.SectionId != 0 && section is null)
        {
            errors.Add("Choose one of the sections in the list.");
        }

        var strand = p.StrandId == 0 ? null : await db.Strands.AsNoTracking().Where(s => s.Id == p.StrandId)
            .Select(s => new { s.Id, s.TrackId }).FirstOrDefaultAsync(ct);
        if (p.StrandId != 0 && strand is null)
        {
            errors.Add("Choose one of the strands in the list.");
        }

        if (p.TrackId != 0 && !await db.Tracks.AnyAsync(t => t.Id == p.TrackId, ct))
        {
            errors.Add("Choose one of the tracks in the list.");
        }

        if (errors.Count > 0)
        {
            return (null, errors);
        }

        if (strand is not null && section is not null && section.StrandId != strand.Id)
        {
            errors.Add("The section is not a section of that strand.");
        }

        int? strandId = strand?.Id ?? section?.StrandId;
        int? trackId = strand?.TrackId ?? section?.TrackId ?? (p.TrackId == 0 ? null : p.TrackId);
        if (p.TrackId != 0 && trackId != p.TrackId)
        {
            errors.Add("The strand is not in that track.");
        }

        var subjectIds = p.SubjectIds.Distinct().ToList();
        if (subjectIds.Count > 0 && section is null)
        {
            errors.Add("Choose the section the courses are for.");
        }

        if (errors.Count == 0 && section is not null && subjectIds.Count > 0)
        {
            var inCurriculum = await db.StrandSubjects.AsNoTracking()
                .Where(x => x.StrandId == section.StrandId && x.GradeLevel == section.GradeLevel && subjectIds.Contains(x.SubjectId))
                .Select(x => x.SubjectId).Distinct().ToListAsync(ct);
            foreach (var missing in subjectIds.Except(inCurriculum))
            {
                var name = await db.Subjects.AsNoTracking().Where(s => s.Id == missing).Select(s => s.Name).FirstOrDefaultAsync(ct) ?? "A course";
                errors.Add($"{name} is not one of the courses of {section.StrandCode} in grade {section.GradeLevel}.");
            }

            var taken = await db.TeachingAssignments.AsNoTracking()
                .Where(a => a.SectionId == section.Id && subjectIds.Contains(a.SubjectId))
                .Select(a => new { Subject = a.Subject.Name, Teacher = a.Teacher.FirstName + " " + a.Teacher.LastName })
                .ToListAsync(ct);
            errors.AddRange(taken.Select(t => $"{t.Subject} of that section is already taught by {t.Teacher}."));
        }

        return errors.Count > 0
            ? (null, errors)
            : (new ResolvedPlacement(trackId, strandId, section?.Id, section?.GradeLevel, subjectIds), errors);
    }

    /// <summary>The "role" of the list of the staff who do not teach: registrars, guidance counselors, academic coordinators and academic providers together.</summary>
    public const string NonTeachingRole = "NonTeaching";

    /// <summary>The roles of the non-teaching staff (the administrator is not in the list: the school has one and it is not managed here).</summary>
    public static readonly string[] NonTeachingRoles = [AppRoles.Registrar, AppRoles.GuidanceCounselor, AppRoles.AcademicCoordinator, AppRoles.AcademicProvider];

    /// <summary>How the list is ordered: by name (the default), the newest accounts first, or the oldest first.</summary>
    public const string SortName = "name";
    public const string SortNewest = "newest";
    public const string SortOldest = "oldest";

    /// <summary>The choices of the status filter of the students: active, inactive (switched off), dropped out, graduated.</summary>
    public static readonly string[] StudentStatusFilters = ["active", "inactive", "dropped", "graduated"];

    /// <param name="active">Only the accounts that are active (true) or inactive (false); empty: all of them.</param>
    /// <param name="sort">One of <see cref="SortName"/>, <see cref="SortNewest"/>, <see cref="SortOldest"/>.</param>
    /// <param name="status">Students only: "active", "inactive", "dropped" or "graduated" (empty: all). It replaces <paramref name="active"/> for students.</param>
    /// <param name="staffRole">The non-teaching list only: one role of <see cref="NonTeachingRoles"/>; empty: all of them.</param>
    public async Task<UserPage> ListAsync(string role, string? search, int page, int pageSize, CancellationToken ct = default, bool? active = null,
        string sort = SortName, string? status = null, string? staffRole = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var tokens = (search ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(6).ToArray();

        switch (role)
        {
            case NonTeachingRole:
                return await NonTeachingPageAsync(db, tokens, active, sort, staffRole, page, pageSize, ct);

            case AppRoles.Student:
            {
                var filtered = Filter(db.Students.AsNoTracking(), tokens, t => s =>
                    s.FirstName.Contains(t) || (s.MiddleName != null && s.MiddleName.Contains(t)) || s.LastName.Contains(t)
                    || s.Lrn.Contains(t) || s.User.Email.Contains(t));
                var query = status switch
                {
                    "active" => filtered.Where(s => s.Status == StudentStatus.Active && s.User.IsActive),
                    "inactive" => filtered.Where(s => s.Status == StudentStatus.Active && !s.User.IsActive),
                    "dropped" => filtered.Where(s => s.Status == StudentStatus.DroppedOut),
                    "graduated" => filtered.Where(s => s.Status == StudentStatus.Graduated),
                    _ => ByStatus(filtered, active),
                };

                var total = await query.CountAsync(ct);
                var rows = await OrderBySort(query, sort)
                    .Skip((page - 1) * pageSize).Take(pageSize)
                    .Select(s => new
                    {
                        StudentId = s.Id, s.UserId, s.FirstName, s.MiddleName, s.LastName, s.NameExtension, s.User.Email, s.User.PhoneNumber, s.Lrn,
                        s.User.IsActive, s.User.LastLoginAtUtc, s.User.CreatedAtUtc, s.GradeLevel, s.StrandId, s.SectionId, s.Status,
                    })
                    .ToListAsync(ct);

                var strandIds = rows.Where(r => r.StrandId != null).Select(r => r.StrandId!.Value).Distinct().ToList();
                var strandCodes = await db.Strands.AsNoTracking().Where(s => strandIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Code, ct);
                var sectionIds = rows.Where(r => r.SectionId != null).Select(r => r.SectionId!.Value).Distinct().ToList();
                var sectionNames = await db.Sections.AsNoTracking().Where(s => sectionIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);

                // A student whose own row has no section (enrolled before that summary was kept) shows the section of
                // their latest enrollment instead.
                var noSection = rows.Where(r => r.SectionId == null).Select(r => r.StudentId).ToList();
                var latest = noSection.Count == 0
                    ? []
                    : (await db.Enrollments.AsNoTracking()
                        .Where(e => noSection.Contains(e.StudentId))
                        .OrderByDescending(e => e.Term.StartDate).ThenByDescending(e => e.Id)
                        .Select(e => new { e.StudentId, e.GradeLevel, StrandCode = e.Strand.Code, SectionName = e.Section.Name })
                        .ToListAsync(ct))
                        .GroupBy(e => e.StudentId)
                        .ToDictionary(g => g.Key, g => Placement(g.First().GradeLevel, g.First().StrandCode, g.First().SectionName));

                // The courses of each student: those of the current term, or of their latest term when they have none now.
                var studentIds = rows.Select(r => r.StudentId).ToList();
                var taken = await db.EnrollmentSubjects.AsNoTracking()
                    .Where(x => studentIds.Contains(x.Enrollment.StudentId) && x.Enrollment.Status != EnrollmentStatus.Dropped)
                    .Select(x => new { x.Enrollment.StudentId, x.Enrollment.TermId, x.Enrollment.Term.IsCurrent, x.Enrollment.Term.StartDate, Course = x.Subject.Name })
                    .ToListAsync(ct);
                IReadOnlyList<string> CoursesOf(int studentId)
                {
                    var own = taken.Where(t => t.StudentId == studentId).ToList();
                    var term = own.Where(t => t.IsCurrent).Select(t => (int?)t.TermId).FirstOrDefault()
                               ?? own.OrderByDescending(t => t.StartDate).Select(t => (int?)t.TermId).FirstOrDefault();
                    return own.Where(t => t.TermId == term).Select(t => t.Course).Distinct().OrderBy(c => c).ToList();
                }

                var items = rows.Select(r => new UserListItem(
                    r.UserId, role, FullName(r.FirstName, r.MiddleName, r.LastName, r.NameExtension), r.Email, r.PhoneNumber, null, r.Lrn,
                    r.IsActive, r.LastLoginAtUtc, r.CreatedAtUtc,
                    r.SectionId is null && latest.TryGetValue(r.StudentId, out var fromEnrollment)
                        ? fromEnrollment
                        : Placement(r.GradeLevel, r.StrandId is { } sid ? strandCodes.GetValueOrDefault(sid) : null,
                            r.SectionId is { } secId ? sectionNames.GetValueOrDefault(secId) : null),
                    CoursesOf(r.StudentId), r.Status)).ToList();
                return new UserPage(items, total, page, pageSize);
            }

            case AppRoles.Teacher:
            {
                var query = ByStatus(Filter(db.Teachers.AsNoTracking(), tokens, t => p =>
                    p.FirstName.Contains(t) || (p.MiddleName != null && p.MiddleName.Contains(t)) || p.LastName.Contains(t)
                    || (p.EmployeeNumber != null && p.EmployeeNumber.Contains(t)) || p.User.Email.Contains(t)), active);

                var total = await query.CountAsync(ct);
                var rows = await OrderBySort(query, sort)
                    .Skip((page - 1) * pageSize).Take(pageSize)
                    .Select(p => new
                    {
                        p.UserId, p.FirstName, p.MiddleName, p.LastName, p.NameExtension, p.User.Email, p.User.PhoneNumber, p.EmployeeNumber,
                        p.User.IsActive, p.User.LastLoginAtUtc, p.User.CreatedAtUtc,
                    })
                    .ToListAsync(ct);

                // What each teacher teaches: the course and its section.
                var teacherUserIds = rows.Select(r => r.UserId).ToList();
                var taught = await db.TeachingAssignments.AsNoTracking()
                    .Where(a => teacherUserIds.Contains(a.Teacher.UserId))
                    .OrderBy(a => a.Subject.Name).ThenBy(a => a.Section.GradeLevel).ThenBy(a => a.Section.Strand.Code).ThenBy(a => a.Section.Name)
                    .Select(a => new { a.Teacher.UserId, Course = a.Subject.Name, a.Section.GradeLevel, Strand = a.Section.Strand.Code, Section = a.Section.Name })
                    .ToListAsync(ct);
                string SectionsOf(int userId) => string.Join(", ", taught.Where(t => t.UserId == userId).Select(t => $"{t.GradeLevel}-{t.Strand}-{t.Section}").Distinct());

                return new UserPage(rows.Select(r => new UserListItem(
                    r.UserId, role, FullName(r.FirstName, r.MiddleName, r.LastName, r.NameExtension), r.Email, r.PhoneNumber, r.EmployeeNumber, null,
                    r.IsActive, r.LastLoginAtUtc, r.CreatedAtUtc, SectionsOf(r.UserId) is { Length: > 0 } sections ? sections : null,
                    taught.Where(t => t.UserId == r.UserId).Select(t => $"{t.Course} · {t.GradeLevel}-{t.Strand}-{t.Section}").ToList())).ToList(), total, page, pageSize);
            }

            case AppRoles.Registrar:
                return await StaffPageAsync(db.Registrars.AsNoTracking(), role, tokens, active, sort, page, pageSize, ct);

            case AppRoles.GuidanceCounselor:
                return await StaffPageAsync(db.GuidanceCounselors.AsNoTracking(), role, tokens, active, sort, page, pageSize, ct);

            case AppRoles.AcademicCoordinator:
                return await StaffPageAsync(db.AcademicCoordinators.AsNoTracking(), role, tokens, active, sort, page, pageSize, ct);

            case AppRoles.AcademicProvider:
                return await StaffPageAsync(db.AcademicProviders.AsNoTracking(), role, tokens, active, sort, page, pageSize, ct);

            case AppRoles.Admin:
                return await StaffPageAsync(db.Administrators.AsNoTracking(), role, tokens, active, sort, page, pageSize, ct);

            default:
                return new UserPage([], 0, 1, pageSize);
        }
    }

    /// <summary>Administrators, registrars and guidance counselors have the same columns.</summary>
    private static async Task<UserPage> StaffPageAsync<T>(IQueryable<T> source, string role, string[] tokens, bool? active, string sort, int page, int pageSize, CancellationToken ct)
        where T : StaffContactProfile
    {
        var query = ByStatus(Filter(source, tokens, t => p =>
            p.FirstName.Contains(t) || (p.MiddleName != null && p.MiddleName.Contains(t)) || p.LastName.Contains(t)
            || (p.EmployeeNumber != null && p.EmployeeNumber.Contains(t)) || p.User.Email.Contains(t)), active);

        var total = await query.CountAsync(ct);
        var rows = await OrderBySort(query, sort)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new
            {
                p.UserId, p.FirstName, p.MiddleName, p.LastName, p.NameExtension, p.User.Email, p.PhoneNumber, p.EmployeeNumber,
                p.User.IsActive, p.User.LastLoginAtUtc, p.User.CreatedAtUtc,
            })
            .ToListAsync(ct);

        return new UserPage(rows.Select(r => new UserListItem(
            r.UserId, role, FullName(r.FirstName, r.MiddleName, r.LastName, r.NameExtension), r.Email, r.PhoneNumber, r.EmployeeNumber, null,
            r.IsActive, r.LastLoginAtUtc, r.CreatedAtUtc, null)).ToList(), total, page, pageSize);
    }

    /// <summary>
    /// The staff who do not teach in one list, with the role of each: the four small tables are read (each filtered in the database), put together,
    /// ordered and cut into pages here. There are only a few of them.
    /// </summary>
    private static async Task<UserPage> NonTeachingPageAsync(AppDbContext db, string[] tokens, bool? active, string sort, string? staffRole, int page, int pageSize, CancellationToken ct)
    {
        async Task<List<UserListItem>> One<T>(IQueryable<T> source, string role) where T : StaffContactProfile
        {
            if (staffRole is { Length: > 0 } && staffRole != role)
            {
                return [];
            }

            var query = ByStatus(Filter(source.AsNoTracking(), tokens, t => p =>
                p.FirstName.Contains(t) || (p.MiddleName != null && p.MiddleName.Contains(t)) || p.LastName.Contains(t)
                || (p.EmployeeNumber != null && p.EmployeeNumber.Contains(t)) || p.User.Email.Contains(t)), active);
            var rows = await query.Select(p => new
            {
                p.UserId, p.FirstName, p.MiddleName, p.LastName, p.NameExtension, p.User.Email, p.PhoneNumber, p.EmployeeNumber,
                p.User.IsActive, p.User.LastLoginAtUtc, p.User.CreatedAtUtc, RoleTitle = p.User.CustomRole != null ? p.User.CustomRole.Name : null,
            }).Take(500).ToListAsync(ct);
            return rows.Select(r => new UserListItem(
                r.UserId, role, FullName(r.FirstName, r.MiddleName, r.LastName, r.NameExtension), r.Email, r.PhoneNumber, r.EmployeeNumber, null,
                r.IsActive, r.LastLoginAtUtc, r.CreatedAtUtc, null, RoleTitle: r.RoleTitle)).ToList();
        }

        var all = new List<UserListItem>();
        all.AddRange(await One(db.Registrars, AppRoles.Registrar));
        all.AddRange(await One(db.GuidanceCounselors, AppRoles.GuidanceCounselor));
        all.AddRange(await One(db.AcademicCoordinators, AppRoles.AcademicCoordinator));
        all.AddRange(await One(db.AcademicProviders, AppRoles.AcademicProvider));

        IEnumerable<UserListItem> ordered = sort switch
        {
            SortNewest => all.OrderByDescending(u => u.CreatedAtUtc).ThenByDescending(u => u.UserId),
            SortOldest => all.OrderBy(u => u.CreatedAtUtc).ThenBy(u => u.UserId),
            _ => all.OrderBy(u => u.FullName, StringComparer.CurrentCultureIgnoreCase).ThenBy(u => u.UserId),
        };
        return new UserPage(ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList(), all.Count, page, pageSize);
    }

    /// <summary>The order of a list: by last name, or by the day the account was made (newest first, oldest first).</summary>
    private static IOrderedQueryable<T> OrderBySort<T>(IQueryable<T> source, string sort) where T : PersonProfile => sort switch
    {
        SortNewest => source.OrderByDescending(p => p.User.CreatedAtUtc).ThenByDescending(p => p.Id),
        SortOldest => source.OrderBy(p => p.User.CreatedAtUtc).ThenBy(p => p.Id),
        _ => source.OrderBy(p => p.LastName).ThenBy(p => p.FirstName).ThenBy(p => p.Id),
    };

    /// <summary>Only the active (or only the inactive) accounts; with no choice, all.</summary>
    private static IQueryable<T> ByStatus<T>(IQueryable<T> source, bool? active) where T : PersonProfile =>
        active is { } on ? source.Where(p => p.User.IsActive == on) : source;

    /// <summary>Every word of the search must match somewhere (name, email, LRN or employee number).</summary>
    private static IQueryable<T> Filter<T>(IQueryable<T> source, string[] tokens, Func<string, Expression<Func<T, bool>>> matches)
    {
        foreach (var token in tokens)
        {
            source = source.Where(matches(token));
        }

        return source;
    }

    /// <summary>How many accounts each role has, for the tabs.</summary>
    public async Task<IReadOnlyDictionary<string, int>> CountByRoleAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var counts = await db.Users.AsNoTracking().GroupBy(u => u.RoleId).Select(g => new { RoleId = g.Key, Count = g.Count() }).ToListAsync(ct);
        return AppRoles.Ids.ToDictionary(kv => kv.Key, kv => counts.FirstOrDefault(c => c.RoleId == kv.Value)?.Count ?? 0);
    }

    public async Task<UserEdit?> GetForEditAsync(int userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var user = await db.Users.AsNoTracking().IncludeProfiles().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user?.Profile is not { } profile)
        {
            return null;
        }

        return new UserEdit
        {
            UserId = user.Id,
            Role = user.Role.Name,
            FirstName = profile.FirstName,
            MiddleName = profile.MiddleName,
            LastName = profile.LastName,
            NameExtension = profile.NameExtension,
            Email = user.Email,
            PhoneNumber = profile is StaffContactProfile staff ? staff.PhoneNumber : user.PhoneNumber,
            EmployeeNumber = (profile as StaffProfile)?.EmployeeNumber,
            Lrn = (profile as Student)?.Lrn,
            StudentId = (profile as Student)?.Id,
            StudentStatus = (profile as Student)?.Status,
            IsActive = user.IsActive,
        };
    }

    /// <summary>The student (not the account) of a user, or null when the user is not a student.</summary>
    public async Task<int?> GetStudentIdAsync(int userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Students.AsNoTracking().Where(s => s.UserId == userId).Select(s => (int?)s.Id).FirstOrDefaultAsync(ct);
    }

    public async Task<ServiceResult> UpdateAsync(UserEdit edit, int actingUserId, CancellationToken ct = default)
    {
        var errors = new List<string>();

        var firstName = NameCase.Title(edit.FirstName);
        var lastName = NameCase.Title(edit.LastName);
        var middleName = NullIfBlank(NameCase.Title(edit.MiddleName));
        var nameExtension = NullIfBlank(NameCase.Title(edit.NameExtension));
        if (firstName.Length is 0 or > 100)
        {
            errors.Add("Enter the first name (up to 100 characters).");
        }

        if (lastName.Length is 0 or > 100)
        {
            errors.Add("Enter the last name (up to 100 characters).");
        }

        if (middleName is { Length: > 100 })
        {
            errors.Add("The middle name can have at most 100 characters.");
        }

        if (nameExtension is { Length: > 10 })
        {
            errors.Add("The name extension can have at most 10 characters.");
        }

        var email = EmailAddresses.Normalize(edit.Email);
        if (email is null)
        {
            errors.Add("Enter a valid email address.");
        }

        string? phone = null;
        if (!string.IsNullOrWhiteSpace(edit.PhoneNumber))
        {
            phone = PhoneNumbers.Normalize(edit.PhoneNumber);
            if (phone is null)
            {
                errors.Add("Enter a valid mobile number, for example 09171234567 or +639171234567.");
            }
        }

        var newPassword = string.IsNullOrEmpty(edit.NewPassword) ? null : edit.NewPassword;
        if (newPassword is not null && PasswordPolicy.Validate(newPassword) is { } passwordError)
        {
            errors.Add(passwordError);
        }

        var isStudent = edit.Role == AppRoles.Student;
        var lrn = NullIfBlank(edit.Lrn?.Trim());
        var employeeNumber = NullIfBlank(edit.EmployeeNumber?.Trim());
        if (isStudent && (lrn is null || lrn.Length != 12 || !lrn.All(char.IsAsciiDigit)))
        {
            errors.Add("The LRN must be exactly 12 digits.");
        }

        if (!isStudent && employeeNumber is { Length: > 30 })
        {
            errors.Add("The employee number can have at most 30 characters.");
        }

        if (errors.Count > 0)
        {
            return ServiceResult.Fail(errors);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        if (!await IsActiveAdminAsync(db, actingUserId, ct))
        {
            return ServiceResult.Fail("Only an administrator can change accounts.");
        }

        var user = await db.Users.IncludeProfiles().FirstOrDefaultAsync(u => u.Id == edit.UserId, ct);
        if (user?.Profile is not { } profile)
        {
            return ServiceResult.Fail("This account no longer exists.");
        }

        if (user.Id == actingUserId && !edit.IsActive)
        {
            return ServiceResult.Fail("You can't deactivate your own account.");
        }

        if (await db.Users.AnyAsync(u => u.Email == email && u.Id != user.Id, ct))
        {
            return ServiceResult.Fail("Another account already uses this email.");
        }

        if (profile is Student student)
        {
            if (await db.Students.AnyAsync(s => s.Lrn == lrn && s.Id != student.Id, ct))
            {
                return ServiceResult.Fail("Another student already has this LRN.");
            }
        }
        else if (employeeNumber is not null && await EmployeeNumberTakenAsync(db, user, employeeNumber, ct))
        {
            return ServiceResult.Fail("Another account already has this employee number.");
        }

        // Signing out everywhere (a new security stamp) when the way in changes or the account is switched off.
        var rotateStamp = newPassword is not null || email != user.Email || (user.IsActive && !edit.IsActive);
        var wasActive = user.IsActive;
        var oldEmail = user.Email;

        // The account and the copies of the name in the enrollments are saved together, or not at all.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        profile.FirstName = firstName;
        profile.MiddleName = middleName;
        profile.LastName = lastName;
        profile.NameExtension = nameExtension;

        user.Email = email!;
        user.PhoneNumber = phone;
        user.IsActive = edit.IsActive;
        if (newPassword is not null)
        {
            user.PasswordHash = hasher.Hash(newPassword);
            user.FailedLoginCount = 0;
            user.LockoutEndUtc = null;
        }

        if (rotateStamp)
        {
            user.SecurityStamp = Guid.NewGuid().ToString("N");
        }

        switch (profile)
        {
            case Student s:
                s.Lrn = lrn!;
                // A correction of the name or LRN shows in the student's enrollment records as well.
                await db.Enrollments.Where(e => e.StudentId == s.Id).ExecuteUpdateAsync(set => set
                    .SetProperty(e => e.Lrn, lrn!)
                    .SetProperty(e => e.FirstName, firstName)
                    .SetProperty(e => e.MiddleName, middleName)
                    .SetProperty(e => e.LastName, lastName), ct);
                break;
            case StaffContactProfile staff:
                staff.EmployeeNumber = employeeNumber;
                staff.PhoneNumber = phone;
                staff.Status = edit.IsActive ? StaffStatus.Active : StaffStatus.Inactive;
                break;
            case StaffProfile other:
                other.EmployeeNumber = employeeNumber;
                break;
        }

        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { ErrorCode: MySqlErrorCode.DuplicateKeyEntry })
        {
            return ServiceResult.Fail("The email, LRN or employee number is already used by another account.");
        }

        if (securityLog is not null)
        {
            var who = FullName(firstName, middleName, lastName, nameExtension);
            await securityLog.AddAsync(SecurityEvents.AccountChanged, user.Id, email, $"The account of {who} was saved" + (email != oldEmail ? $" (the email was {oldEmail})." : "."), actorUserId: actingUserId, ct: ct);
            if (newPassword is not null)
            {
                await securityLog.AddAsync(SecurityEvents.PasswordReset, user.Id, email, $"A new password was set for {who}.", actorUserId: actingUserId, ct: ct);
            }

            if (wasActive && !edit.IsActive)
            {
                await securityLog.AddAsync(SecurityEvents.AccountDeactivated, user.Id, email, $"The account of {who} was switched off.", actorUserId: actingUserId, ct: ct);
            }
            else if (!wasActive && edit.IsActive)
            {
                await securityLog.AddAsync(SecurityEvents.AccountActivated, user.Id, email, $"The account of {who} was switched on.", actorUserId: actingUserId, ct: ct);
            }
        }

        return ServiceResult.Ok($"Saved {FullName(firstName, middleName, lastName, nameExtension)}.");
    }

    /// <summary>What deleting the account would remove or clear, to show before the administrator confirms.</summary>
    public async Task<DeleteImpact?> GetDeleteImpactAsync(int userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var user = await db.Users.AsNoTracking().IncludeProfiles().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user?.Profile is not { } profile)
        {
            return null;
        }

        var effects = new List<string>();
        switch (profile)
        {
            case Student s:
            {
                var enrollments = await db.Enrollments.CountAsync(e => e.StudentId == s.Id, ct);
                if (enrollments > 0)
                {
                    effects.Add(enrollments == 1 ? "Their 1 enrollment is deleted too." : $"Their {enrollments} enrollments are deleted too.");
                }

                break;
            }

            case Teacher t:
            {
                var sections = await db.Sections.CountAsync(s => s.AdviserTeacherId == t.Id, ct);
                if (sections > 0)
                {
                    effects.Add($"{sections} section(s) lose their adviser.");
                }

                break;
            }

            case StaffContactProfile:
            {
                var enrolled = await db.Enrollments.CountAsync(e => e.EnrolledByUserId == user.Id, ct);
                if (enrolled > 0)
                {
                    effects.Add($"The {enrolled} enrollment(s) they made stay, with \"enrolled by\" cleared.");
                }

                break;
            }
        }

        return new DeleteImpact(profile.FullName, AppRoles.DisplayName(user.Role.Name), effects);
    }

    public async Task<ServiceResult> DeleteAsync(int userId, int actingUserId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        if (!await IsActiveAdminAsync(db, actingUserId, ct))
        {
            return ServiceResult.Fail("Only an administrator can delete accounts.");
        }

        if (userId == actingUserId)
        {
            return ServiceResult.Fail("You can't delete your own account.");
        }

        var user = await db.Users.AsNoTracking().IncludeProfiles().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user?.Profile is not { } profile)
        {
            return ServiceResult.Fail("This account no longer exists.");
        }

        // The administrator who is deleting is always one active administrator who stays, so there is always one.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        switch (profile)
        {
            case Student s:
                // The subject rows go with their enrollments (cascade).
                await db.Enrollments.Where(e => e.StudentId == s.Id).ExecuteDeleteAsync(ct);
                break;
            case Teacher t:
                await db.Sections.Where(x => x.AdviserTeacherId == t.Id)
                    .ExecuteUpdateAsync(set => set.SetProperty(x => x.AdviserTeacherId, (int?)null), ct);
                break;
        }

        // The profile row goes with the user (cascade); "enrolled by" in enrollments is cleared by the database.
        await db.Users.Where(u => u.Id == userId).ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct);

        if (securityLog is not null)
        {
            await securityLog.AddAsync(SecurityEvents.AccountDeleted, null, user.Email, $"The {AppRoles.DisplayName(user.Role.Name).ToLowerInvariant()} account of {profile.FullName} was deleted.", actorUserId: actingUserId, ct: ct);
        }

        return ServiceResult.Ok($"Deleted {profile.FullName}.");
    }

    private static async Task<bool> IsActiveAdminAsync(AppDbContext db, int userId, CancellationToken ct) =>
        await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.IsActive && u.RoleId == AppRoles.AdminId, ct);

    private static async Task<bool> EmployeeNumberTakenAsync(AppDbContext db, User user, string employeeNumber, CancellationToken ct) => user.Role.Name switch
    {
        AppRoles.Teacher => await db.Teachers.AnyAsync(p => p.EmployeeNumber == employeeNumber && p.UserId != user.Id, ct),
        AppRoles.Registrar => await db.Registrars.AnyAsync(p => p.EmployeeNumber == employeeNumber && p.UserId != user.Id, ct),
        AppRoles.GuidanceCounselor => await db.GuidanceCounselors.AnyAsync(p => p.EmployeeNumber == employeeNumber && p.UserId != user.Id, ct),
        AppRoles.AcademicCoordinator => await db.AcademicCoordinators.AnyAsync(p => p.EmployeeNumber == employeeNumber && p.UserId != user.Id, ct),
        AppRoles.AcademicProvider => await db.AcademicProviders.AnyAsync(p => p.EmployeeNumber == employeeNumber && p.UserId != user.Id, ct),
        AppRoles.Admin => await db.Administrators.AnyAsync(p => p.EmployeeNumber == employeeNumber && p.UserId != user.Id, ct),
        _ => false,
    };

    private static string FullName(string first, string? middle, string last, string? extension) =>
        string.Join(' ', new[] { first, middle, last, extension }.Where(p => !string.IsNullOrWhiteSpace(p)));

    private static string? Placement(byte? gradeLevel, string? strandCode, string? sectionName)
    {
        // The usual way to write a section: 11-BUSSENT-A.
        if (gradeLevel is not null && strandCode is not null && sectionName is not null)
        {
            return $"{gradeLevel}-{strandCode}-{sectionName}";
        }

        var parts = new List<string>();
        if (gradeLevel is not null)
        {
            parts.Add($"Grade {gradeLevel}");
        }

        if (strandCode is not null)
        {
            parts.Add(strandCode);
        }

        if (sectionName is not null)
        {
            parts.Add($"Section {sectionName}");
        }

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    private static string Clean(string? text) =>
        string.Join(' ', (text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static string? NullIfBlank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;
}

internal static class UserQueryExtensions
{
    /// <summary>The role and the role's profile row of each user (only the one that matches has a row).</summary>
    public static IQueryable<User> IncludeProfiles(this IQueryable<User> users) => users
        .Include(u => u.Role)
        .Include(u => u.Administrator)
        .Include(u => u.GuidanceCounselor)
        .Include(u => u.AcademicCoordinator)
        .Include(u => u.AcademicProvider)
        .Include(u => u.Registrar)
        .Include(u => u.Teacher)
        .Include(u => u.Student);
}
