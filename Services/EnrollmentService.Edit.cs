using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SEAITT_LMS.Auth;
using SEAITT_LMS.Components.Shared;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>One student enrolled on a day, for the registrar's list "Enrolled Students Today".</summary>
public sealed record EnrolledTodayRow(int EnrollmentId, string StudentName, string Lrn, byte GradeLevel, string Strand, string Section, string Term, string SchoolYear, DateTime EnrolledAtUtc);

/// <summary>How full a section is: the students enrolled in it for a term, and the limit.</summary>
public sealed record SectionLoad(string Name, int Students, int Capacity)
{
    public bool IsFull => Students >= Capacity;

    public int SeatsLeft => Math.Max(0, Capacity - Students);

    public string Text => $"{Students} of {Capacity} students";
}

/// <summary>Where the re-enroll box starts: the grade level, track and strand of the student's last enrollment, moved on when a new school year starts. The section is left empty.</summary>
public sealed record ReEnrollDefaults(byte GradeLevel, int TrackId, int StrandId, string PreviousStrandCode, byte PreviousGradeLevel);

/// <summary>An enrollment as the edit box shows it.</summary>
public sealed record EnrollmentEditView(
    int EnrollmentId, int StudentId, string Lrn, string FirstName, string? MiddleName, string LastName, string? NameExtension, string? PhoneNumber,
    int TermId, byte GradeLevel, int TrackId, int StrandId, string SectionName, IReadOnlyList<int> SubjectIds, EnrollmentStatus Status);

/// <summary>What the registrar changes in an enrollment. <paramref name="SubjectIds"/> empty (null) means: all the courses of the strand at that grade level and term.</summary>
public sealed record EnrollmentChange(
    int EnrollmentId, string FirstName, string? MiddleName, string LastName, string? NameExtension, string? PhoneNumber,
    int TermId, byte GradeLevel, int TrackId, int StrandId, string SectionName, IReadOnlyList<int>? SubjectIds);

/// <summary>The sign-in details of a student's account: the email, and the password when it is still the first one (null when the student changed it).</summary>
public sealed record StudentSignIn(string StudentName, string Lrn, string Email, bool IsActive, string? Password);

public sealed partial class EnrollmentService
{
    /// <summary>
    /// The sections A and B of a strand and grade level for a term, each with the number of students enrolled in it and the limit
    /// (and any other section that exists). <paramref name="exceptEnrollmentId"/> is not counted: the enrollment that is being edited.
    /// </summary>
    public async Task<IReadOnlyList<SectionLoad>> GetSectionLoadsAsync(int termId, int strandId, byte gradeLevel, int? exceptEnrollmentId = null, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var counts = await db.Enrollments.AsNoTracking()
            .Where(e => e.TermId == termId && e.StrandId == strandId && e.GradeLevel == gradeLevel && e.Status == EnrollmentStatus.Enrolled && e.Id != exceptEnrollmentId)
            .GroupBy(e => e.Section.Name)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var existing = await GetSectionNamesAsync(termId, strandId, gradeLevel, ct);
        var capacity = SectionCapacity;
        return SectionNames.Choices.Concat(existing).Distinct().Order()
            .Select(name => new SectionLoad(name, counts.FirstOrDefault(c => c.Name == name)?.Count ?? 0, capacity))
            .ToList();
    }

    /// <summary>
    /// The registrar adds a section for a strand and grade level (the school year is the one of the term), from the Section list of the enrollment form. The name is letters, numbers, spaces
    /// and dashes (up to 50 characters); a single letter is written as a capital. The new section is a choice in the list at once.
    /// </summary>
    public async Task<ServiceResult> AddSectionAsync(int actingUserId, int termId, int strandId, byte gradeLevel, string? name, CancellationToken ct = default)
    {
        var clean = string.Join(' ', (name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (clean.Length == 1)
        {
            clean = clean.ToUpperInvariant();
        }

        if (clean.Length is 0 or > 50 || !System.Text.RegularExpressions.Regex.IsMatch(clean, @"^[A-Za-z0-9][A-Za-z0-9 \-]*$"))
        {
            return ServiceResult.Fail("Type the name of the section, like C. Use letters, numbers, spaces and dashes (up to 50 characters).");
        }

        if (gradeLevel is not (11 or 12))
        {
            return ServiceResult.Fail("Choose the grade level first.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await db.Users.AsNoTracking().InArea(AppRoles.Registrar).AnyAsync(u => u.Id == actingUserId && u.IsActive, ct)
            && !await db.Users.AsNoTracking().InArea(AppRoles.AcademicCoordinator).AnyAsync(u => u.Id == actingUserId && u.IsActive, ct))
        {
            return ServiceResult.Fail("Only the registrar can add a section here.");
        }

        var term = await db.Terms.AsNoTracking().Where(t => t.Id == termId).Select(t => new { t.SchoolYearId }).FirstOrDefaultAsync(ct);
        var strand = await db.Strands.AsNoTracking().Where(s => s.Id == strandId).Select(s => new { s.Id, s.Code }).FirstOrDefaultAsync(ct);
        if (term is null || strand is null)
        {
            return ServiceResult.Fail("Choose the strand first.");
        }

        if (await db.Sections.AnyAsync(s => s.SchoolYearId == term.SchoolYearId && s.StrandId == strand.Id && s.GradeLevel == gradeLevel && s.Name == clean, ct))
        {
            return ServiceResult.Fail($"There is already a section {clean} for {gradeLevel}-{strand.Code}.");
        }

        var section = new Section { SchoolYearId = term.SchoolYearId, StrandId = strand.Id, GradeLevel = gradeLevel, Name = clean };
        db.Sections.Add(section);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Fail($"There is already a section {clean} for {gradeLevel}-{strand.Code}.");
        }

        return ServiceResult.Ok($"The section {clean} was added.", section.Id);
    }

    /// <summary>
    /// Where a student starts when enrolled again for <paramref name="termId"/>. The second term of a school year keeps the grade level, track and strand of the
    /// first. The first term of a new school year moves a Grade 11 student up to Grade 12 (the strand and track stay). The section is chosen by the registrar.
    /// Null when the student has no earlier enrollment.
    /// </summary>
    public async Task<ReEnrollDefaults?> GetReEnrollDefaultsAsync(int studentId, int termId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var term = await db.Terms.AsNoTracking().Where(t => t.Id == termId).Select(t => new { t.SchoolYearId }).FirstOrDefaultAsync(ct);
        var last = await db.Enrollments.AsNoTracking()
            .Where(e => e.StudentId == studentId && e.TermId != termId)
            .OrderByDescending(e => e.Term.SchoolYear.StartDate).ThenByDescending(e => e.Term.TermNumber).ThenByDescending(e => e.Id)
            .Select(e => new { e.GradeLevel, e.TrackId, e.StrandId, StrandCode = e.Strand.Code, e.SchoolYearId })
            .FirstOrDefaultAsync(ct);
        if (term is null || last is null)
        {
            return null;
        }

        var grade = last.SchoolYearId == term.SchoolYearId ? last.GradeLevel : (byte)Math.Min(12, last.GradeLevel + 1);
        return new ReEnrollDefaults(grade, last.TrackId, last.StrandId, last.StrandCode, last.GradeLevel);
    }

    /// <summary>
    /// The sign-in details of a student's account, for the registrar. The email is the account's. The password is only shown when it is still the one made at the enrollment
    /// (the last name and the end of the LRN, checked against the saved password): after the student changed it, or an administrator set a new one, it is not known here.
    /// </summary>
    public async Task<StudentSignIn?> GetSignInAsync(int studentId, int actingUserId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await db.Users.AsNoTracking().InArea(AppRoles.Registrar).AnyAsync(u => u.Id == actingUserId && u.IsActive, ct))
        {
            return null;
        }

        var s = await db.Students.AsNoTracking().Where(x => x.Id == studentId)
            .Select(x => new { x.FirstName, x.MiddleName, x.LastName, x.NameExtension, x.Lrn, x.User.Email, x.User.IsActive, x.User.PasswordHash })
            .FirstOrDefaultAsync(ct);
        if (s is null)
        {
            return null;
        }

        // The first password is the part of the email before the @ without the dot (vargas.111111@... gives vargas111111).
        var local = s.Email.Split('@')[0].Replace(".", "", StringComparison.Ordinal);
        var password = local.Length >= PasswordPolicy.MinLength && hasher.Verify(local, s.PasswordHash) ? local : null;
        var name = string.Join(' ', new[] { s.FirstName, s.MiddleName, s.LastName, s.NameExtension }.Where(p => !string.IsNullOrWhiteSpace(p)));
        return new StudentSignIn(name, s.Lrn, s.Email, s.IsActive, password);
    }

    public async Task<EnrollmentEditView?> GetEnrollmentForEditAsync(int enrollmentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var e = await db.Enrollments.AsNoTracking().Where(x => x.Id == enrollmentId)
            .Select(x => new
            {
                x.Id, x.StudentId, x.Student.Lrn, x.Student.FirstName, x.Student.MiddleName, x.Student.LastName, x.Student.NameExtension, x.Student.User.PhoneNumber,
                x.TermId, x.GradeLevel, x.TrackId, x.StrandId, SectionName = x.Section.Name, x.Status,
            }).FirstOrDefaultAsync(ct);
        if (e is null)
        {
            return null;
        }

        var subjectIds = await db.EnrollmentSubjects.AsNoTracking().Where(s => s.EnrollmentId == enrollmentId).Select(s => s.SubjectId).ToListAsync(ct);
        return new EnrollmentEditView(e.Id, e.StudentId, e.Lrn, e.FirstName, e.MiddleName, e.LastName, e.NameExtension, e.PhoneNumber,
            e.TermId, e.GradeLevel, e.TrackId, e.StrandId, e.SectionName, subjectIds, e.Status);
    }

    /// <summary>
    /// The registrar corrects an enrollment: the name and the phone number of the student, the term, the grade level, the track and strand (the course of
    /// study), the section and the courses. The student's copies of the name in the other enrollments follow the new name. The section limit is kept.
    /// </summary>
    public async Task<ServiceResult> UpdateEnrollmentAsync(EnrollmentChange change, int actingUserId, CancellationToken ct = default)
    {
        var errors = new List<string>();

        var firstName = NameCase.Title(change.FirstName);
        var middleName = NameCase.Title(change.MiddleName);
        var lastName = NameCase.Title(change.LastName);
        var nameExtension = NameCase.Title(change.NameExtension);
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

        if (nameExtension.Length > 10)
        {
            errors.Add("The name extension (Jr., Sr., III ...) can have at most 10 characters.");
        }

        // The names are words: letters only (with the spaces, dots, dashes and apostrophes inside real names), no digits.
        if (!NameCase.IsName(firstName) || !NameCase.IsName(middleName) || !NameCase.IsName(lastName) || !NameCase.IsName(nameExtension))
        {
            errors.Add("The names can have letters only: no digits or other signs.");
        }

        if (change.GradeLevel is not (11 or 12))
        {
            errors.Add("Pick grade level 11 or 12.");
        }

        var sectionName = Clean(change.SectionName);
        if (sectionName.Length is 0 or > MaxSectionNameLength || !SectionNamePattern().IsMatch(sectionName))
        {
            errors.Add("Choose the section: A or B.");
        }

        string? phone = null;
        if (!string.IsNullOrWhiteSpace(change.PhoneNumber))
        {
            // Digits only (the spaces of a pasted "0917 123 4567" are ignored, and the + of a number that is already stored); a letter or another sign is not a phone number.
            var typed = string.Concat(change.PhoneNumber.Where(c => !char.IsWhiteSpace(c)));
            phone = (typed.StartsWith('+') ? typed[1..] : typed).All(char.IsAsciiDigit) ? PhoneNumbers.Normalize(typed) : null;
            if (phone is null)
            {
                errors.Add("The phone number can have digits only, like 09171234567. Leave it empty if there is none.");
            }
        }

        if (errors.Count > 0)
        {
            return ServiceResult.Fail(errors);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await db.Users.AsNoTracking().InArea(AppRoles.Registrar).AnyAsync(u => u.Id == actingUserId && u.IsActive, ct))
        {
            return ServiceResult.Fail("Only a registrar can change an enrollment.");
        }

        var enrollment = await db.Enrollments.Include(e => e.EnrolledSubjects).Include(e => e.Student).ThenInclude(s => s.User)
            .FirstOrDefaultAsync(e => e.Id == change.EnrollmentId, ct);
        if (enrollment is null)
        {
            return ServiceResult.Fail("This enrollment was not found.");
        }

        if (enrollment.Status != EnrollmentStatus.Enrolled)
        {
            return ServiceResult.Fail("Only an enrollment that is going on can be changed.");
        }

        var term = await db.Terms.AsNoTracking().Include(t => t.SchoolYear).FirstOrDefaultAsync(t => t.Id == change.TermId, ct);
        var strand = await db.Strands.AsNoTracking().Include(s => s.Track).FirstOrDefaultAsync(s => s.Id == change.StrandId, ct);
        if (term is null)
        {
            errors.Add("Pick the term.");
        }

        if (strand is null || !strand.IsActive || strand.TrackId != change.TrackId || !strand.Track.IsActive)
        {
            errors.Add("Pick a track and one of its strands.");
        }

        if (errors.Count > 0)
        {
            return ServiceResult.Fail(errors);
        }

        if (term!.Id != enrollment.TermId && await db.Enrollments.AnyAsync(e => e.StudentId == enrollment.StudentId && e.TermId == term.Id && e.Id != enrollment.Id, ct))
        {
            return ServiceResult.Fail($"{enrollment.Student.FullName} is already enrolled for the {term.Name} of {term.SchoolYear.Name}.");
        }

        // The courses: the ones chosen (they must be courses of the strand at this grade level and term), or all of them when nothing was chosen.
        var curriculum = await db.StrandSubjects.AsNoTracking()
            .Where(x => x.StrandId == strand!.Id && x.GradeLevel == change.GradeLevel && x.TermNumber == term.TermNumber && x.Subject.IsActive && x.IsOffered)
            .Select(x => x.Subject).OrderBy(s => s.Name).ToListAsync(ct);
        List<Subject> subjects;
        var oldSubjectIds = enrollment.EnrolledSubjects.Select(s => s.SubjectId).ToHashSet();
        var curriculumMoved = term.Id != enrollment.TermId || strand!.Id != enrollment.StrandId || change.GradeLevel != enrollment.GradeLevel;
        if (change.SubjectIds is { Count: > 0 } chosen && !(curriculumMoved && oldSubjectIds.SetEquals(chosen)))
        {
            subjects = curriculum.Where(s => chosen.Contains(s.Id)).ToList();
            if (subjects.Count != chosen.Distinct().Count())
            {
                return ServiceResult.Fail("A chosen course is not a course of this strand in that grade level and term.");
            }
        }
        else
        {
            // Nothing chosen, or the same courses as before while the term, strand or grade level moved: all the courses of the new place.
            subjects = curriculum;
        }

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var section = await db.Sections.FirstOrDefaultAsync(s =>
                s.SchoolYearId == term.SchoolYearId && s.StrandId == strand!.Id && s.GradeLevel == change.GradeLevel && s.Name == sectionName, ct);
            if (section is not null && section.Id != enrollment.SectionId)
            {
                var inSection = await db.Enrollments.CountAsync(e => e.TermId == term.Id && e.SectionId == section.Id && e.Status == EnrollmentStatus.Enrolled && e.Id != enrollment.Id, ct);
                if (inSection >= SectionCapacity)
                {
                    return ServiceResult.Fail($"Section {section.Name} is full: {inSection} of {SectionCapacity} students. Choose another section.");
                }
            }

            if (section is null)
            {
                if (!SectionNames.IsChoice(sectionName))
                {
                    return ServiceResult.Fail($"There is no section {sectionName} for this strand and grade level. Choose A or B.");
                }

                section = new Section { SchoolYearId = term.SchoolYearId, StrandId = strand!.Id, GradeLevel = change.GradeLevel, Name = sectionName };
                db.Sections.Add(section);
                await db.SaveChangesAsync(ct);
            }

            // the student: the name (the copies in all the enrollments follow) and the phone number
            var student = enrollment.Student;
            student.FirstName = firstName;
            student.MiddleName = NullIfBlank(middleName);
            student.LastName = lastName;
            student.NameExtension = NullIfBlank(nameExtension);
            student.User.PhoneNumber = phone;

            // the enrollment
            enrollment.SchoolYearId = term.SchoolYearId;
            enrollment.TermId = term.Id;
            enrollment.SectionId = section.Id;
            enrollment.GradeLevel = change.GradeLevel;
            enrollment.TrackId = strand!.TrackId;
            enrollment.StrandId = strand.Id;
            enrollment.Subjects = subjects.Count == 0 ? null : string.Join(", ", subjects.Select(s => s.Name));
            db.EnrollmentSubjects.RemoveRange(enrollment.EnrolledSubjects);
            enrollment.EnrolledSubjects = subjects.Select(s => new EnrollmentSubject { SubjectId = s.Id }).ToList();
            await db.SaveChangesAsync(ct);

            await db.Enrollments.Where(e => e.StudentId == student.Id)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(e => e.FirstName, student.FirstName)
                    .SetProperty(e => e.MiddleName, student.MiddleName)
                    .SetProperty(e => e.LastName, student.LastName)
                    .SetProperty(e => e.NameExtension, student.NameExtension), ct);

            // The short summary on the student's own row follows the newest enrollment.
            var newest = await db.Enrollments.AsNoTracking().Where(e => e.StudentId == student.Id)
                .OrderByDescending(e => e.Term.SchoolYear.StartDate).ThenByDescending(e => e.Term.TermNumber).ThenByDescending(e => e.Id)
                .Select(e => new { e.Id, e.GradeLevel, e.TrackId, e.StrandId, e.SectionId }).FirstAsync(ct);
            if (newest.Id == enrollment.Id)
            {
                student.GradeLevel = newest.GradeLevel;
                student.TrackId = newest.TrackId;
                student.StrandId = newest.StrandId;
                student.SectionId = newest.SectionId;
                await db.SaveChangesAsync(ct);
            }

            await transaction.CommitAsync(ct);
            return ServiceResult.Ok($"The enrollment of {student.FullName} was changed.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { ErrorCode: MySqlErrorCode.DuplicateKeyEntry })
        {
            return ServiceResult.Fail("Someone else saved the same thing a moment ago. Please try again.");
        }
    }

    /// <summary>The students enrolled on a day (by the Philippine calendar; today when no day is given), the newest first.</summary>
    public async Task<IReadOnlyList<EnrolledTodayRow>> GetEnrolledOnAsync(DateOnly? day = null, CancellationToken ct = default)
    {
        var date = day ?? PhilippineTime.Today(clock ?? TimeProvider.System);
        var from = PhilippineTime.ToUtc(date.ToDateTime(TimeOnly.MinValue));
        var to = from.AddDays(1);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await db.Enrollments.AsNoTracking()
            .Where(e => e.CreatedAtUtc >= from && e.CreatedAtUtc < to && e.Status == EnrollmentStatus.Enrolled && e.Student.Status == StudentStatus.Active)
            .OrderByDescending(e => e.CreatedAtUtc).ThenByDescending(e => e.Id)
            .Select(e => new { e.Id, e.FirstName, e.MiddleName, e.LastName, e.NameExtension, e.Lrn, e.GradeLevel, Strand = e.Strand.Code, Section = e.Section.Name, Term = e.Term.Name, SchoolYear = e.Term.SchoolYear.Name, e.CreatedAtUtc })
            .ToListAsync(ct);
        return [.. rows.Select(r => new EnrolledTodayRow(r.Id, string.Join(' ', new[] { r.FirstName, r.MiddleName, r.LastName, r.NameExtension }.Where(n => !string.IsNullOrWhiteSpace(n))), r.Lrn, r.GradeLevel, r.Strand, r.Section, r.Term, r.SchoolYear, r.CreatedAtUtc))];
    }
}
