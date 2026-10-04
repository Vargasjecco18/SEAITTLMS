using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SEAITT_LMS.Auth;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>One account in a role's list.</summary>
/// <param name="Placement">For students: the section as kept on the student's row (or, when that is empty, of their latest enrollment), like 11-BUSSENT-A. Empty otherwise.</param>
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
    string? Placement);

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

    /// <summary>Whether the account can sign in. For staff it also sets their status (Active or Inactive).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Leave empty to keep the password. Otherwise the account gets this new password.</summary>
    public string? NewPassword { get; set; }
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
public sealed class UserManagementService(IDbContextFactory<AppDbContext> dbFactory, PasswordHasher hasher, UserAccountService accounts)
{
    /// <summary>
    /// Adds an account for a teacher, registrar, guidance counselor or administrator. (Students are added by enrolling them.)
    /// The email must be given; the administrator chooses the first password.
    /// </summary>
    public async Task<ServiceResult> AddStaffAsync(NewAccount account, int actingUserId, CancellationToken ct = default)
    {
        if (account.Role == AppRoles.Student)
        {
            return ServiceResult.Fail("Students are added by enrolling them.");
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
        var gate = AppRoles.OnlyOneAllowed(account.Role) ? RoleGates.GetOrAdd(account.Role, _ => new SemaphoreSlim(1, 1)) : null;
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
                    return ServiceResult.Fail($"There is already a {AppRoles.DisplayName(account.Role).ToLowerInvariant()}. The school can have only one.");
                }
            }

            return await CreateStaffAsync(account, ct);
        }
        finally
        {
            gate?.Release();
        }
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> RoleGates = new();

    private async Task<ServiceResult> CreateStaffAsync(NewAccount account, CancellationToken ct)
    {
        var result = await accounts.CreateAsync(account, ct);
        return result.Succeeded
            ? ServiceResult.Ok($"Added {FullName(account.FirstName.Trim(), NullIfBlank(account.MiddleName), account.LastName.Trim(), NullIfBlank(account.NameExtension))}.")
            : ServiceResult.Fail(result.Errors);
    }

    public async Task<UserPage> ListAsync(string role, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var tokens = (search ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(6).ToArray();

        switch (role)
        {
            case AppRoles.Student:
            {
                var query = Filter(db.Students.AsNoTracking(), tokens, t => s =>
                    s.FirstName.Contains(t) || (s.MiddleName != null && s.MiddleName.Contains(t)) || s.LastName.Contains(t)
                    || s.Lrn.Contains(t) || s.User.Email.Contains(t));

                var total = await query.CountAsync(ct);
                var rows = await query.OrderBy(s => s.LastName).ThenBy(s => s.FirstName).ThenBy(s => s.Id)
                    .Skip((page - 1) * pageSize).Take(pageSize)
                    .Select(s => new
                    {
                        StudentId = s.Id, s.UserId, s.FirstName, s.MiddleName, s.LastName, s.NameExtension, s.User.Email, s.User.PhoneNumber, s.Lrn,
                        s.User.IsActive, s.User.LastLoginAtUtc, s.User.CreatedAtUtc, s.GradeLevel, s.StrandId, s.SectionId,
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

                var items = rows.Select(r => new UserListItem(
                    r.UserId, role, FullName(r.FirstName, r.MiddleName, r.LastName, r.NameExtension), r.Email, r.PhoneNumber, null, r.Lrn,
                    r.IsActive, r.LastLoginAtUtc, r.CreatedAtUtc,
                    r.SectionId is null && latest.TryGetValue(r.StudentId, out var fromEnrollment)
                        ? fromEnrollment
                        : Placement(r.GradeLevel, r.StrandId is { } sid ? strandCodes.GetValueOrDefault(sid) : null,
                            r.SectionId is { } secId ? sectionNames.GetValueOrDefault(secId) : null))).ToList();
                return new UserPage(items, total, page, pageSize);
            }

            case AppRoles.Teacher:
            {
                var query = Filter(db.Teachers.AsNoTracking(), tokens, t => p =>
                    p.FirstName.Contains(t) || (p.MiddleName != null && p.MiddleName.Contains(t)) || p.LastName.Contains(t)
                    || (p.EmployeeNumber != null && p.EmployeeNumber.Contains(t)) || p.User.Email.Contains(t));

                var total = await query.CountAsync(ct);
                var rows = await query.OrderBy(p => p.LastName).ThenBy(p => p.FirstName).ThenBy(p => p.Id)
                    .Skip((page - 1) * pageSize).Take(pageSize)
                    .Select(p => new
                    {
                        p.UserId, p.FirstName, p.MiddleName, p.LastName, p.NameExtension, p.User.Email, p.User.PhoneNumber, p.EmployeeNumber,
                        p.User.IsActive, p.User.LastLoginAtUtc, p.User.CreatedAtUtc,
                    })
                    .ToListAsync(ct);

                return new UserPage(rows.Select(r => new UserListItem(
                    r.UserId, role, FullName(r.FirstName, r.MiddleName, r.LastName, r.NameExtension), r.Email, r.PhoneNumber, r.EmployeeNumber, null,
                    r.IsActive, r.LastLoginAtUtc, r.CreatedAtUtc, null)).ToList(), total, page, pageSize);
            }

            case AppRoles.Registrar:
                return await StaffPageAsync(db.Registrars.AsNoTracking(), role, tokens, page, pageSize, ct);

            case AppRoles.GuidanceCounselor:
                return await StaffPageAsync(db.GuidanceCounselors.AsNoTracking(), role, tokens, page, pageSize, ct);

            case AppRoles.Admin:
                return await StaffPageAsync(db.Administrators.AsNoTracking(), role, tokens, page, pageSize, ct);

            default:
                return new UserPage([], 0, 1, pageSize);
        }
    }

    /// <summary>Administrators, registrars and guidance counselors have the same columns.</summary>
    private static async Task<UserPage> StaffPageAsync<T>(IQueryable<T> source, string role, string[] tokens, int page, int pageSize, CancellationToken ct)
        where T : StaffContactProfile
    {
        var query = Filter(source, tokens, t => p =>
            p.FirstName.Contains(t) || (p.MiddleName != null && p.MiddleName.Contains(t)) || p.LastName.Contains(t)
            || (p.EmployeeNumber != null && p.EmployeeNumber.Contains(t)) || p.User.Email.Contains(t));

        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(p => p.LastName).ThenBy(p => p.FirstName).ThenBy(p => p.Id)
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
            IsActive = user.IsActive,
        };
    }

    public async Task<ServiceResult> UpdateAsync(UserEdit edit, int actingUserId, CancellationToken ct = default)
    {
        var errors = new List<string>();

        var firstName = Clean(edit.FirstName);
        var lastName = Clean(edit.LastName);
        var middleName = NullIfBlank(Clean(edit.MiddleName));
        var nameExtension = NullIfBlank(Clean(edit.NameExtension));
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

        return ServiceResult.Ok($"Deleted {profile.FullName}.");
    }

    private static async Task<bool> IsActiveAdminAsync(AppDbContext db, int userId, CancellationToken ct) =>
        await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.IsActive && u.RoleId == AppRoles.AdminId, ct);

    private static async Task<bool> EmployeeNumberTakenAsync(AppDbContext db, User user, string employeeNumber, CancellationToken ct) => user.Role.Name switch
    {
        AppRoles.Teacher => await db.Teachers.AnyAsync(p => p.EmployeeNumber == employeeNumber && p.UserId != user.Id, ct),
        AppRoles.Registrar => await db.Registrars.AnyAsync(p => p.EmployeeNumber == employeeNumber && p.UserId != user.Id, ct),
        AppRoles.GuidanceCounselor => await db.GuidanceCounselors.AnyAsync(p => p.EmployeeNumber == employeeNumber && p.UserId != user.Id, ct),
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
        .Include(u => u.Registrar)
        .Include(u => u.Teacher)
        .Include(u => u.Student);
}
