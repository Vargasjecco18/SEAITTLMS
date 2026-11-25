using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Auth;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

public sealed class AdmissionInput
{
    public int? Id { get; set; }
    public string Lrn { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = "";
    public string? NameExtension { get; set; }
    public Sex? Sex { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public string? PreviousSchool { get; set; }
    public string? FatherName { get; set; }
    public string? FatherPhone { get; set; }
    public string? MotherName { get; set; }
    public string? MotherPhone { get; set; }
    public string? GuardianName { get; set; }
    public string? GuardianRelationship { get; set; }
    public string? GuardianPhone { get; set; }
    public string? GuardianAddress { get; set; }
}

/// <summary>A line of the list of the Admission staff and of the list the registrar chooses from.</summary>
public sealed record AdmissionRow(int Id, byte GradeLevel, string Lrn, string FullName, string? ParentsText, int DocumentsReceived, bool Enrolled, DateTime CreatedAtUtc);

public sealed record AdmissionDocumentRow(int Id, string Kind, string? FileName, long SizeBytes, DateTime ReceivedAtUtc);

/// <summary>What the registrar needs of a recorded student: the names, the phone, the parents and the guardian (the printable form fills them in by itself).</summary>
public sealed record AdmissionDetails(
    int Id, byte GradeLevel, string Lrn, string FirstName, string? MiddleName, string LastName, string? NameExtension, Sex? Sex, string? PhoneNumber, string? Address, string? PreviousSchool,
    string? FatherName, string? FatherPhone, string? MotherName, string? MotherPhone, string? GuardianName, string? GuardianRelationship, string? GuardianPhone, string? GuardianAddress, bool Enrolled)
{
    public string FullName => NameCase.Title(string.Join(' ', new[] { FirstName, MiddleName, LastName, NameExtension }.Where(x => !string.IsNullOrWhiteSpace(x))));
}

/// <summary>
/// Round 46: the Admission staff (Grade 11 and Grade 12) record the information and the documents of the students that the registrar enrolls. The Admission staff see only the students of their grade level;
/// the registrar chooses from the recorded students and reads the parents and the guardian (the printable form uses them).
/// </summary>
public sealed class AdmissionService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider clock)
{
    public const long MaxDocumentBytes = 10 * 1024 * 1024;

    /// <summary>The grade levels (11 and/or 12) the person may record: the Admission pages that were ticked for the role of the account; an administrator may read both.</summary>
    public async Task<IReadOnlySet<byte>> GradesOfAsync(int userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var user = await db.Users.AsNoTracking().Where(u => u.Id == userId && u.IsActive).Select(u => new { u.RoleId, Pages = u.CustomRole != null ? u.CustomRole.Pages : "" }).FirstOrDefaultAsync(ct);
        var grades = new HashSet<byte>();
        if (user is null)
        {
            return grades;
        }

        var pages = StaffPages.Parse(user.Pages);
        if (pages.Contains("admission-grade11")) { grades.Add(11); }
        if (pages.Contains("admission-grade12")) { grades.Add(12); }
        return grades;
    }

    private static string? Parents(AdmissionRecord r)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(r.FatherName)) { parts.Add($"Father: {r.FatherName}"); }
        if (!string.IsNullOrWhiteSpace(r.MotherName)) { parts.Add($"Mother: {r.MotherName}"); }
        if (!string.IsNullOrWhiteSpace(r.GuardianName)) { parts.Add($"Guardian: {r.GuardianName}{(string.IsNullOrWhiteSpace(r.GuardianRelationship) ? "" : $" ({r.GuardianRelationship})")}"); }
        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    private static string FullName(AdmissionRecord r) => $"{r.LastName}, {r.FirstName}{(string.IsNullOrWhiteSpace(r.MiddleName) ? "" : " " + r.MiddleName)}{(string.IsNullOrWhiteSpace(r.NameExtension) ? "" : " " + r.NameExtension)}";

    /// <summary>The recorded students of one grade level, the newest first (the Admission staff of that grade level).</summary>
    public async Task<IReadOnlyList<AdmissionRow>> ListAsync(int userId, byte grade, string? search, CancellationToken ct = default)
    {
        if (!(await GradesOfAsync(userId, ct)).Contains(grade))
        {
            return [];
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var query = db.AdmissionRecords.AsNoTracking().Where(r => r.GradeLevel == grade);
        foreach (var token in (search ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(4))
        {
            var t = token;
            query = query.Where(r => r.FirstName.Contains(t) || r.LastName.Contains(t) || r.Lrn.Contains(t));
        }

        var rows = await query.OrderByDescending(r => r.CreatedAtUtc).ThenBy(r => r.LastName).Take(300).Include(r => r.Documents).ToListAsync(ct);
        return [.. rows.Select(r => new AdmissionRow(r.Id, r.GradeLevel, r.Lrn, FullName(r), Parents(r), r.Documents.Count, r.EnrolledAtUtc is not null, r.CreatedAtUtc))];
    }

    public async Task<AdmissionInput?> GetForEditAsync(int userId, int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var r = await db.AdmissionRecords.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null || !(await GradesOfAsync(userId, ct)).Contains(r.GradeLevel))
        {
            return null;
        }

        return new AdmissionInput
        {
            Id = r.Id, Lrn = r.Lrn, FirstName = r.FirstName, MiddleName = r.MiddleName, LastName = r.LastName, NameExtension = r.NameExtension, Sex = r.Sex, PhoneNumber = PhoneNumbers.ToLocal(r.PhoneNumber),
            Address = r.Address, PreviousSchool = r.PreviousSchool, FatherName = r.FatherName, FatherPhone = PhoneNumbers.ToLocal(r.FatherPhone), MotherName = r.MotherName, MotherPhone = PhoneNumbers.ToLocal(r.MotherPhone),
            GuardianName = r.GuardianName, GuardianRelationship = r.GuardianRelationship, GuardianPhone = PhoneNumbers.ToLocal(r.GuardianPhone), GuardianAddress = r.GuardianAddress,
        };
    }

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static string? Phone(string? typed, string label, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(typed))
        {
            return null;
        }

        var clean = string.Concat(typed.Where(c => !char.IsWhiteSpace(c)));
        var normal = (clean.StartsWith('+') ? clean[1..] : clean).All(char.IsAsciiDigit) ? PhoneNumbers.Normalize(clean) : null;
        if (normal is null)
        {
            errors.Add($"{label}: digits only, like 09171234567.");
        }

        return normal;
    }

    /// <summary>Records a new student of the grade level, or changes the record that has the id. The LRN is 12 digits and unique.</summary>
    public async Task<ServiceResult> SaveAsync(int userId, byte grade, AdmissionInput input, CancellationToken ct = default)
    {
        if (!(await GradesOfAsync(userId, ct)).Contains(grade))
        {
            return ServiceResult.Fail($"Your account does not record the students of Grade {grade}.");
        }

        var errors = new List<string>();
        var lrn = new string((input.Lrn ?? "").Where(char.IsAsciiDigit).ToArray());
        if (lrn.Length != 12)
        {
            errors.Add("The LRN is 12 digits.");
        }

        var first = Blank(input.FirstName);
        var last = Blank(input.LastName);
        if (first is null) { errors.Add("Type the first name."); }
        if (last is null) { errors.Add("Type the last name."); }
        var phone = Phone(input.PhoneNumber, "Phone number", errors);
        var fatherPhone = Phone(input.FatherPhone, "Father's phone", errors);
        var motherPhone = Phone(input.MotherPhone, "Mother's phone", errors);
        var guardianPhone = Phone(input.GuardianPhone, "Guardian's phone", errors);
        if (Blank(input.FatherName) is null && Blank(input.MotherName) is null && Blank(input.GuardianName) is null)
        {
            errors.Add("Write the father, the mother or the guardian (at least one).");
        }

        if (errors.Count > 0)
        {
            return ServiceResult.Fail(errors);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        AdmissionRecord? record = null;
        if (input.Id is { } id)
        {
            record = await db.AdmissionRecords.FirstOrDefaultAsync(r => r.Id == id && r.GradeLevel == grade, ct);
            if (record is null)
            {
                return ServiceResult.Fail("This record was not found.");
            }
        }

        if (await db.AdmissionRecords.AnyAsync(r => r.Lrn == lrn && r.Id != (record == null ? 0 : record.Id), ct))
        {
            return ServiceResult.Fail("A student with this LRN is already recorded.");
        }

        if (record is null)
        {
            record = new AdmissionRecord { GradeLevel = grade, RecordedByUserId = userId };
            db.AdmissionRecords.Add(record);
        }

        record.Lrn = lrn;
        record.FirstName = NameCase.Title(first!);
        record.MiddleName = Blank(input.MiddleName) is { } m ? NameCase.Title(m) : null;
        record.LastName = NameCase.Title(last!);
        record.NameExtension = Blank(input.NameExtension);
        record.Sex = input.Sex;
        record.PhoneNumber = phone;
        record.Address = Blank(input.Address);
        record.PreviousSchool = Blank(input.PreviousSchool);
        record.FatherName = Blank(input.FatherName) is { } f ? NameCase.Title(f) : null;
        record.FatherPhone = fatherPhone;
        record.MotherName = Blank(input.MotherName) is { } mo ? NameCase.Title(mo) : null;
        record.MotherPhone = motherPhone;
        record.GuardianName = Blank(input.GuardianName) is { } g ? NameCase.Title(g) : null;
        record.GuardianRelationship = Blank(input.GuardianRelationship);
        record.GuardianPhone = guardianPhone;
        record.GuardianAddress = Blank(input.GuardianAddress);
        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok(input.Id is null ? "The student was recorded." : "The record was saved.", record.Id);
    }

    // ---------------------------------------------------------------- the documents

    public async Task<IReadOnlyList<AdmissionDocumentRow>> ListDocumentsAsync(int userId, int recordId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var grade = await db.AdmissionRecords.AsNoTracking().Where(r => r.Id == recordId).Select(r => (byte?)r.GradeLevel).FirstOrDefaultAsync(ct);
        if (grade is null || !(await GradesOfAsync(userId, ct)).Contains(grade.Value))
        {
            return [];
        }

        return await db.AdmissionDocuments.AsNoTracking().Where(d => d.RecordId == recordId).OrderBy(d => d.Kind).ThenBy(d => d.Id)
            .Select(d => new AdmissionDocumentRow(d.Id, d.Kind, d.FileName, d.SizeBytes, d.ReceivedAtUtc)).ToListAsync(ct);
    }

    /// <summary>A document the Admission staff received: only the kind (a paper in hand), or the kind with a scanned copy.</summary>
    public async Task<ServiceResult> AddDocumentAsync(int userId, int recordId, string kind, string? fileName, string? contentType, byte[]? data, CancellationToken ct = default)
    {
        if (!AdmissionDocumentKinds.All.Contains(kind))
        {
            return ServiceResult.Fail("Choose the kind of the document.");
        }

        if (data is not null && data.LongLength > MaxDocumentBytes)
        {
            return ServiceResult.Fail($"The file is bigger than {MaxDocumentBytes / (1024 * 1024)} MB.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var grade = await db.AdmissionRecords.AsNoTracking().Where(r => r.Id == recordId).Select(r => (byte?)r.GradeLevel).FirstOrDefaultAsync(ct);
        if (grade is null || !(await GradesOfAsync(userId, ct)).Contains(grade.Value))
        {
            return ServiceResult.Fail("This record was not found.");
        }

        db.AdmissionDocuments.Add(new AdmissionDocument
        {
            RecordId = recordId, Kind = kind, FileName = fileName is { Length: > 0 } ? Path.GetFileName(fileName) : null, ContentType = contentType, SizeBytes = (long)(data?.Length ?? 0), Data = data,
            ReceivedAtUtc = clock.GetUtcNow().UtcDateTime, ReceivedByUserId = userId,
        });
        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok("The document was added.");
    }

    public async Task<ServiceResult> RemoveDocumentAsync(int userId, int documentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var doc = await db.AdmissionDocuments.Include(d => d.Record).FirstOrDefaultAsync(d => d.Id == documentId, ct);
        if (doc is null || !(await GradesOfAsync(userId, ct)).Contains(doc.Record.GradeLevel))
        {
            return ServiceResult.Fail("This document was not found.");
        }

        db.AdmissionDocuments.Remove(doc);
        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok("The document was taken out.");
    }

    public async Task<(string Name, string ContentType, byte[] Data)?> OpenDocumentAsync(int userId, int documentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var doc = await db.AdmissionDocuments.AsNoTracking().Include(d => d.Record).FirstOrDefaultAsync(d => d.Id == documentId && d.Data != null, ct);
        if (doc is null || !(await GradesOfAsync(userId, ct)).Contains(doc.Record.GradeLevel))
        {
            return null;
        }

        return (doc.FileName ?? doc.Kind, doc.ContentType ?? "application/octet-stream", doc.Data!);
    }

    // ---------------------------------------------------------------- the registrar

    /// <summary>The recorded students that are not enrolled yet (the list the registrar chooses from; the LRN is shown, not typed).</summary>
    public async Task<IReadOnlyList<AdmissionRow>> ListToEnrollAsync(string? search = null, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var query = db.AdmissionRecords.AsNoTracking().Where(r => r.EnrolledAtUtc == null);
        foreach (var token in (search ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(4))
        {
            var t = token;
            query = query.Where(r => r.FirstName.Contains(t) || r.LastName.Contains(t) || r.Lrn.Contains(t));
        }

        var rows = await query.OrderBy(r => r.LastName).ThenBy(r => r.FirstName).Take(300).Include(r => r.Documents).ToListAsync(ct);
        return [.. rows.Select(r => new AdmissionRow(r.Id, r.GradeLevel, r.Lrn, FullName(r), Parents(r), r.Documents.Count, false, r.CreatedAtUtc))];
    }

    /// <summary>The record of a student by the LRN (for the enrollment form and the printable form), or nothing.</summary>
    public async Task<AdmissionDetails?> FindByLrnAsync(string? lrn, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(lrn) || lrn.Length != 12)
        {
            return null;
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.AdmissionRecords.AsNoTracking().Where(r => r.Lrn == lrn).Select(r => new AdmissionDetails(
            r.Id, r.GradeLevel, r.Lrn, r.FirstName, r.MiddleName, r.LastName, r.NameExtension, r.Sex, r.PhoneNumber, r.Address, r.PreviousSchool, r.FatherName, r.FatherPhone, r.MotherName, r.MotherPhone,
            r.GuardianName, r.GuardianRelationship, r.GuardianPhone, r.GuardianAddress, r.EnrolledAtUtc != null)).FirstOrDefaultAsync(ct);
    }

    /// <summary>The student was enrolled from the record: it leaves the list the registrar chooses from. (Called by the enrollment.)</summary>
    public async Task MarkEnrolledAsync(string lrn, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        await db.AdmissionRecords.Where(r => r.Lrn == lrn && r.EnrolledAtUtc == null).ExecuteUpdateAsync(set => set.SetProperty(r => r.EnrolledAtUtc, now), ct);
    }
}
