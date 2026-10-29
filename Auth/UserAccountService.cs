using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;
using SEAITT_LMS.Services;

namespace SEAITT_LMS.Auth;

/// <summary>Everything needed to create one account and its role specific profile.</summary>
/// <param name="Role">One of the <see cref="AppRoles"/> names.</param>
/// <param name="Lrn">Required for students (12 digits), not allowed for anyone else.</param>
/// <param name="EmployeeNumber">Only for staff roles.</param>
public sealed record NewAccount(
    string Role,
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? MiddleName = null,
    string? NameExtension = null,
    string? PhoneNumber = null,
    string? Lrn = null,
    string? EmployeeNumber = null,
    DateOnly? BirthDate = null,
    Sex? Sex = null,
    bool IsActive = true);

public sealed record AccountResult(bool Succeeded, int? UserId, IReadOnlyList<string> Errors)
{
    public static AccountResult Ok(int userId) => new(true, userId, []);
    public static AccountResult Fail(params IReadOnlyList<string> errors) => new(false, null, errors);
}

/// <summary>
/// The one place that creates accounts, so a user and its profile row (student, teacher, ...) are always
/// created together and always match the role. Later features (registrar enrolling a student, admin adding a
/// teacher) should call this instead of inserting users themselves.
/// </summary>
public sealed class UserAccountService(IDbContextFactory<AppDbContext> dbFactory, PasswordHasher hasher)
{
    public async Task<AccountResult> CreateAsync(NewAccount request, CancellationToken ct = default)
    {
        var errors = new List<string>();

        if (!AppRoles.Ids.TryGetValue(request.Role ?? "", out var roleId))
        {
            errors.Add($"Unknown role '{request.Role}'.");
        }

        var email = EmailAddresses.Normalize(request.Email);
        if (email is null)
        {
            errors.Add("Enter a valid email address.");
        }

        if (PasswordPolicy.Validate(request.Password) is { } passwordError)
        {
            errors.Add(passwordError);
        }

        // Names are written with capital letters (juan dela cruz -> Juan Dela Cruz), whoever typed them.
        var firstName = NameCase.Title(request.FirstName);
        var lastName = NameCase.Title(request.LastName);
        var middleName = NullIfBlank(NameCase.Title(request.MiddleName));
        var nameExtension = NullIfBlank(NameCase.Title(request.NameExtension));

        if (string.IsNullOrEmpty(firstName) || firstName.Length > 100)
        {
            errors.Add("First name is required (up to 100 characters).");
        }

        if (string.IsNullOrEmpty(lastName) || lastName.Length > 100)
        {
            errors.Add("Last name is required (up to 100 characters).");
        }

        if (middleName is { Length: > 100 })
        {
            errors.Add("Middle name must be at most 100 characters.");
        }

        if (nameExtension is { Length: > 10 })
        {
            errors.Add("Name extension must be at most 10 characters.");
        }

        string? phone = null;
        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            phone = PhoneNumbers.Normalize(request.PhoneNumber);
            if (phone is null)
            {
                errors.Add("Enter a valid mobile number, for example 09171234567 or +639171234567.");
            }
        }

        var lrn = NullIfBlank(request.Lrn);
        var employeeNumber = NullIfBlank(request.EmployeeNumber);

        if (request.Role == AppRoles.Student)
        {
            if (lrn is null || lrn.Length != 12 || !lrn.All(char.IsAsciiDigit))
            {
                errors.Add("A student needs a 12-digit LRN.");
            }

            if (employeeNumber is not null)
            {
                errors.Add("A student can't have an employee number.");
            }
        }
        else
        {
            if (lrn is not null)
            {
                errors.Add("Only students have an LRN.");
            }

            if (employeeNumber is { Length: > 30 })
            {
                errors.Add("Employee number must be at most 30 characters.");
            }
        }

        if (errors.Count > 0)
        {
            return AccountResult.Fail(errors);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        if (await db.Users.AnyAsync(u => u.Email == email, ct))
        {
            return AccountResult.Fail("An account with this email already exists.");
        }

        if (lrn is not null && await db.Students.AnyAsync(s => s.Lrn == lrn, ct))
        {
            return AccountResult.Fail("A student with this LRN already exists.");
        }

        var user = new User
        {
            Email = email!,
            PasswordHash = hasher.Hash(request.Password),
            PhoneNumber = phone,
            RoleId = roleId,
            IsActive = request.IsActive,
            SecurityStamp = Guid.NewGuid().ToString("N"),
        };
        db.Users.Add(user);

        // The person's name goes into the profile table of their role. Staff contact numbers start as the sign-in number.
        T Person<T>(T profile) where T : PersonProfile
        {
            profile.User = user;
            profile.FirstName = firstName!;
            profile.MiddleName = middleName;
            profile.LastName = lastName!;
            profile.NameExtension = nameExtension;
            if (profile is StaffContactProfile staff && !request.IsActive)
            {
                staff.Status = StaffStatus.Inactive;
            }

            return profile;
        }

        switch (request.Role)
        {
            case AppRoles.Student:
                db.Students.Add(Person(new Student { Lrn = lrn!, BirthDate = request.BirthDate, Sex = request.Sex }));
                break;
            case AppRoles.Teacher:
                db.Teachers.Add(Person(new Teacher { EmployeeNumber = employeeNumber }));
                break;
            case AppRoles.Registrar:
                db.Registrars.Add(Person(new Registrar { EmployeeNumber = employeeNumber, PhoneNumber = phone }));
                break;
            case AppRoles.GuidanceCounselor:
                db.GuidanceCounselors.Add(Person(new GuidanceCounselor { EmployeeNumber = employeeNumber, PhoneNumber = phone }));
                break;
            case AppRoles.Admin:
                db.Administrators.Add(Person(new Administrator { EmployeeNumber = employeeNumber, PhoneNumber = phone }));
                break;
            case AppRoles.AcademicCoordinator:
                db.AcademicCoordinators.Add(Person(new AcademicCoordinator { EmployeeNumber = employeeNumber, PhoneNumber = phone }));
                break;
            case AppRoles.AcademicProvider:
                db.AcademicProviders.Add(Person(new AcademicProvider { EmployeeNumber = employeeNumber, PhoneNumber = phone }));
                break;
        }

        try
        {
            // One SaveChanges is one transaction: the user and its profile are created together or not at all.
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { ErrorCode: MySqlErrorCode.DuplicateKeyEntry } duplicate)
        {
            // Lost a race against another request that created the same email, LRN or employee number.
            return AccountResult.Fail(duplicate.Message switch
            {
                var m when m.Contains("email", StringComparison.OrdinalIgnoreCase) => "An account with this email already exists.",
                var m when m.Contains("lrn", StringComparison.OrdinalIgnoreCase) => "A student with this LRN already exists.",
                var m when m.Contains("employee_number", StringComparison.OrdinalIgnoreCase) => "This employee number is already in use.",
                _ => "This account conflicts with an existing one.",
            });
        }

        return AccountResult.Ok(user.Id);
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
