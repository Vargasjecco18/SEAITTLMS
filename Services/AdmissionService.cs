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
    public string? PreviousSchool { get; set; }

    /// <summary>Round 49: the email address of the student.</summary>
    public string? Email { get; set; }

    /// <summary>Round 49: the address of the student, in four parts (the whole line is made from them when the record is saved).</summary>
    public AddressParts Address { get; set; } = new();

    public string? FatherName { get; set; }
    public string? FatherPhone { get; set; }
    public string? FatherOccupation { get; set; }
    public string? FatherEmail { get; set; }
    public string? MotherName { get; set; }
    public string? MotherPhone { get; set; }
    public string? MotherOccupation { get; set; }
    public string? MotherEmail { get; set; }

    /// <summary>Round 49: where the parents live (one address for the father and the mother).</summary>
    public AddressParts ParentsAddress { get; set; } = new();

    public string? GuardianName { get; set; }
    public string? GuardianRelationship { get; set; }
    public string? GuardianPhone { get; set; }
    public string? GuardianOccupation { get; set; }
    public string? GuardianEmail { get; set; }
    public AddressParts GuardianAddress { get; set; } = new();

    /// <summary>Round 47: the strand the student wants.</summary>
    public int? StrandId { get; set; }

    /// <summary>Round 49: the track the student wants (the strands to choose from are the ones of the track).</summary>
    public int? TrackId { get; set; }
}

/// <summary>Round 49: an address in the four parts of the form: the street address, the city or municipality, the state or province and the postal / zip code.</summary>
public sealed class AddressParts
{
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? Province { get; set; } = AdmissionService.DefaultProvince;
    public string? PostalCode { get; set; }

    public bool IsEmpty => string.IsNullOrWhiteSpace(Street) && string.IsNullOrWhiteSpace(City) && string.IsNullOrWhiteSpace(PostalCode);

    /// <summary>The whole line: "Purok 3, Rawis, Legazpi City, Albay 4500".</summary>
    public string? Line()
    {
        if (IsEmpty)
        {
            return null;
        }

        var place = string.Join(", ", new[] { Street, City, Province }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()));
        return string.IsNullOrWhiteSpace(PostalCode) ? place : $"{place} {PostalCode.Trim()}";
    }
}

/// <summary>A line of the list of the Admission staff and of the list the registrar chooses from.</summary>
public sealed record AdmissionRow(int Id, byte GradeLevel, string Lrn, string FullName, string? ParentsText, int DocumentsReceived, bool Enrolled, DateTime CreatedAtUtc,
    string? Strand = null, string? Section = null, string? Phone = null, int? StrandId = null, int OtherDocuments = 0);

/// <summary>Round 49: a document that has its file, for the Print and Download buttons.</summary>
public sealed record AdmissionDocumentFile(int Id, string Kind, string FileName, string ContentType, byte[] Data);

/// <summary>A strand to choose when a student is recorded (Round 49: with the id of its track, for the list of tracks).</summary>
public sealed record AdmissionStrand(int Id, string Code, string Name, string TrackCode, int TrackId = 0);

/// <summary>Round 49: a track to choose when a student is recorded.</summary>
public sealed record AdmissionTrack(int Id, string Code, string Name);

public sealed record AdmissionDocumentRow(int Id, string Kind, string? FileName, long SizeBytes, DateTime ReceivedAtUtc);

/// <summary>
/// Round 47: one line of the documents of a student: the kind (always there, nothing to choose), and what the school has of it: nothing, the paper in hand (the date), or a document (a photo or a scan,
/// which can be seen in a box).
/// </summary>
public sealed record AdmissionDocumentSlot(string Kind, int? DocumentId, bool InHand, bool HasFile, string? FileName, string? ContentType, DateTime? ReceivedAtUtc, bool IsOther = false)
{
    public bool IsImage => ContentType is { } type && type.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    /// <summary>"No document", "Paper in hand" or "Document" (a photo counts as a document).</summary>
    public string State => HasFile ? "Document" : InHand ? "Paper in hand" : "No document";
}

/// <summary>What the registrar needs of a recorded student: the names, the phone, the parents and the guardian (the printable form fills them in by itself).</summary>
public sealed record AdmissionDetails(
    int Id, byte GradeLevel, string Lrn, string FirstName, string? MiddleName, string LastName, string? NameExtension, Sex? Sex, string? PhoneNumber, string? Address, string? PreviousSchool,
    string? FatherName, string? FatherPhone, string? MotherName, string? MotherPhone, string? GuardianName, string? GuardianRelationship, string? GuardianPhone, string? GuardianAddress, bool Enrolled, string? ParentsAddress = null, string? Email = null)
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

        var rows = await query.OrderBy(r => r.LastName).ThenBy(r => r.FirstName).Take(600).Include(r => r.Documents).Include(r => r.Strand).ToListAsync(ct);

        // Round 47: the strand and the section of a student who is enrolled come from the enrollment; before that the strand is the one the Admission staff chose and there is no section yet.
        var lrns = rows.Select(r => r.Lrn).ToList();
        var placed = await db.Students.AsNoTracking().Where(s => lrns.Contains(s.Lrn) && s.StrandId != null)
            .Select(s => new { s.Lrn, StrandCode = db.Strands.Where(x => x.Id == s.StrandId).Select(x => x.Code).FirstOrDefault(), SectionName = db.Sections.Where(x => x.Id == s.SectionId).Select(x => x.Name).FirstOrDefault() })
            .ToListAsync(ct);
        var byLrn = placed.ToDictionary(p => p.Lrn);
        return [.. rows.Select(r => new AdmissionRow(r.Id, r.GradeLevel, r.Lrn, FullName(r), Parents(r), r.Documents.Select(d => d.Kind).Where(AdmissionDocumentKinds.IsFixed).Distinct().Count(), r.EnrolledAtUtc is not null, r.CreatedAtUtc,
            byLrn.TryGetValue(r.Lrn, out var p) ? p.StrandCode : r.Strand?.Code, byLrn.TryGetValue(r.Lrn, out var q) ? q.SectionName : null, PhoneNumbers.ToLocal(r.PhoneNumber), r.StrandId, r.Documents.Select(d => d.Kind).Where(k => !AdmissionDocumentKinds.IsFixed(k)).Distinct().Count()))];
    }

    /// <summary>The strands the Admission staff choose from (the active ones, with their track).</summary>
    public async Task<IReadOnlyList<AdmissionStrand>> ListStrandsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Strands.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Track.Code).ThenBy(s => s.Code)
            .Select(s => new AdmissionStrand(s.Id, s.Code, s.Name, s.Track.Code, s.TrackId)).ToListAsync(ct);
    }

    /// <summary>Round 49: the tracks (ACAD, TECH-PRO) that have a strand, to choose before the strand.</summary>
    public async Task<IReadOnlyList<AdmissionTrack>> ListTracksAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await db.Strands.AsNoTracking().Where(s => s.IsActive).Select(s => new { s.TrackId, s.Track.Code, s.Track.Name }).Distinct().ToListAsync(ct);
        return [.. rows.OrderBy(r => r.Code).Select(r => new AdmissionTrack(r.TrackId, r.Code, r.Name))];
    }

    /// <summary>The province a new address starts with (the school is in Albay).</summary>
    public const string DefaultProvince = "Albay";

    /// <summary>The ways a guardian can be related to the student (a list to choose from, then Others to type it).</summary>
    public static readonly IReadOnlyList<string> Relationships =
        ["Mother", "Father", "Grandmother", "Grandfather", "Aunt", "Uncle", "Sister", "Brother", "Cousin", "Godparent", PhilippineAddresses.Others];

    /// <summary>
    /// Round 49: the schools of Albay (<see cref="AdmissionOptionKinds.School"/>) or the cities and municipalities of Albay (<see cref="AdmissionOptionKinds.City"/>) to choose from, with the ones the
    /// Admission staff added. The first ones are put there by the migration <c>AdmissionRecordFields</c>.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetOptionsAsync(string kind, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.AdmissionOptions.AsNoTracking().Where(o => o.Kind == kind).OrderBy(o => o.Name).Select(o => o.Name).ToListAsync(ct);
    }

    /// <summary>The Add school and Add address buttons: a new name goes to the list (the name is written in capitals like a name, and a name that is there already is the same name).</summary>
    public async Task<ServiceResult> AddOptionAsync(int userId, string kind, string? name, CancellationToken ct = default)
    {
        if (kind is not (AdmissionOptionKinds.School or AdmissionOptionKinds.City))
        {
            return ServiceResult.Fail("This list does not take new names.");
        }

        if ((await GradesOfAsync(userId, ct)).Count == 0)
        {
            return ServiceResult.Fail("Only the Admission staff add to these lists.");
        }

        var clean = Blank(name);
        var what = kind == AdmissionOptionKinds.School ? "school" : "city or municipality";
        if (clean is null || clean.Length < 3 || clean.Length > AdmissionOptionKinds.MaxNameLength || clean.Any(char.IsControl))
        {
            return ServiceResult.Fail($"Type the name of the {what} (3 to {AdmissionOptionKinds.MaxNameLength} letters).");
        }

        clean = NameCase.Title(clean);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var there = await db.AdmissionOptions.AsNoTracking().Where(o => o.Kind == kind && o.Name == clean).Select(o => o.Name).FirstOrDefaultAsync(ct);
        if (there is not null)
        {
            return ServiceResult.Ok($"{there} is in the list already.");
        }

        db.AdmissionOptions.Add(new AdmissionOption { Kind = kind, Name = clean });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // somebody added the same name at the same moment: it is there
        }

        return ServiceResult.Ok(clean);
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
            Email = r.Email, PreviousSchool = r.PreviousSchool,
            Address = PartsOf(r.AddressStreet, r.AddressCity, r.AddressProvince, r.AddressPostalCode, r.Address),
            FatherName = r.FatherName, FatherPhone = PhoneNumbers.ToLocal(r.FatherPhone), FatherOccupation = r.FatherOccupation, FatherEmail = r.FatherEmail,
            MotherName = r.MotherName, MotherPhone = PhoneNumbers.ToLocal(r.MotherPhone), MotherOccupation = r.MotherOccupation, MotherEmail = r.MotherEmail,
            ParentsAddress = PartsOf(r.ParentsStreet, r.ParentsCity, r.ParentsProvince, r.ParentsPostalCode, r.ParentsAddress),
            GuardianName = r.GuardianName, GuardianRelationship = r.GuardianRelationship, GuardianPhone = PhoneNumbers.ToLocal(r.GuardianPhone), GuardianOccupation = r.GuardianOccupation, GuardianEmail = r.GuardianEmail,
            GuardianAddress = PartsOf(r.GuardianStreet, r.GuardianCity, r.GuardianProvince, r.GuardianPostalCode, r.GuardianAddress),
            StrandId = r.StrandId, TrackId = r.TrackId ?? r.Strand?.TrackId,
        };
    }

    /// <summary>The parts of an address; a record from before the four parts has only the line, which is put in the street address.</summary>
    private static AddressParts PartsOf(string? street, string? city, string? province, string? postal, string? line) =>
        street is null && city is null && province is null && postal is null && !string.IsNullOrWhiteSpace(line)
            ? new AddressParts { Street = line, Province = null }
            : new AddressParts { Street = street, City = city, Province = province, PostalCode = postal };

    private static string? Email(string? typed, string label, List<string> errors)
    {
        var text = Blank(typed);
        if (text is null)
        {
            return null;
        }

        var at = text.IndexOf('@');
        if (text.Length > 150 || at < 1 || at != text.LastIndexOf('@') || text.Contains(' ') || !text[(at + 1)..].Contains('.') || text.EndsWith('.') || text[(at + 1)..].StartsWith('.'))
        {
            errors.Add($"{label}: write a real email address, like name@example.com.");
            return null;
        }

        return text.ToLowerInvariant();
    }

    /// <summary>The four parts of an address cleaned (the postal code takes digits only, 3 to 10 of them); the whole line is made from them.</summary>
    private static (string? Street, string? City, string? Province, string? Postal, string? Line) Address(AddressParts? parts, string label, List<string> errors)
    {
        if (parts is null)
        {
            return (null, null, null, null, null);
        }

        var street = Blank(parts.Street);
        var city = Blank(parts.City);
        var province = Blank(parts.Province);
        var postal = Blank(parts.PostalCode)?.Replace(" ", "");
        if (postal is not null && (!postal.All(char.IsAsciiDigit) || postal.Length is < 3 or > 10))
        {
            errors.Add($"{label}: the postal / zip code is 3 to 10 digits.");
            postal = null;
        }

        if (street is null && city is null && postal is null)
        {
            return (null, null, null, null, null);
        }

        var clean = new AddressParts { Street = street, City = city, Province = province, PostalCode = postal };
        return (street, city, province, postal, clean.Line());
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
        var email = Email(input.Email, "The student's email", errors);
        var fatherEmail = Email(input.FatherEmail, "Father's email", errors);
        var motherEmail = Email(input.MotherEmail, "Mother's email", errors);
        var guardianEmail = Email(input.GuardianEmail, "Guardian's email", errors);
        var address = Address(input.Address, "Address", errors);
        var parentsAddress = Address(input.ParentsAddress, "Parents' address", errors);
        var guardianAddress = Address(input.GuardianAddress, "Guardian's address", errors);
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
        record.Address = address.Line;
        (record.AddressStreet, record.AddressCity, record.AddressProvince, record.AddressPostalCode) = (address.Street, address.City, address.Province, address.Postal);
        record.Email = email;
        record.PreviousSchool = Blank(input.PreviousSchool);
        record.FatherName = Blank(input.FatherName) is { } f ? NameCase.Title(f) : null;
        record.FatherPhone = fatherPhone;
        record.FatherOccupation = Blank(input.FatherOccupation);
        record.FatherEmail = fatherEmail;
        record.MotherName = Blank(input.MotherName) is { } mo ? NameCase.Title(mo) : null;
        record.MotherPhone = motherPhone;
        record.MotherOccupation = Blank(input.MotherOccupation);
        record.MotherEmail = motherEmail;
        record.ParentsAddress = parentsAddress.Line;
        (record.ParentsStreet, record.ParentsCity, record.ParentsProvince, record.ParentsPostalCode) = (parentsAddress.Street, parentsAddress.City, parentsAddress.Province, parentsAddress.Postal);
        record.GuardianName = Blank(input.GuardianName) is { } g ? NameCase.Title(g) : null;
        record.GuardianRelationship = Blank(input.GuardianRelationship);
        record.GuardianPhone = guardianPhone;
        record.GuardianOccupation = Blank(input.GuardianOccupation);
        record.GuardianEmail = guardianEmail;
        record.GuardianAddress = guardianAddress.Line;
        (record.GuardianStreet, record.GuardianCity, record.GuardianProvince, record.GuardianPostalCode) = (guardianAddress.Street, guardianAddress.City, guardianAddress.Province, guardianAddress.Postal);
        var strand = input.StrandId is { } strandId ? await db.Strands.AsNoTracking().Where(x => x.Id == strandId && x.IsActive).Select(x => new { x.Id, x.TrackId }).FirstOrDefaultAsync(ct) : null;
        record.StrandId = strand?.Id;

        // The track is the one of the strand; a track alone (no strand chosen yet) is kept too.
        var trackId = strand?.TrackId ?? (input.TrackId is { } wanted && await db.Tracks.AnyAsync(t => t.Id == wanted, ct) ? input.TrackId : null);
        record.TrackId = trackId;
        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok(input.Id is null ? "The student was recorded." : "The record was saved.", record.Id);
    }

    // ---------------------------------------------------------------- the documents

    /// <summary>What a person may do with the documents of a record: the registrar reads them, the Admission staff of the grade level read and change them.</summary>
    private async Task<(bool Read, bool Write)> AccessAsync(AppDbContext db, int userId, byte grade, CancellationToken ct)
    {
        if ((await GradesOfAsync(userId, ct)).Contains(grade))
        {
            return (true, true);
        }

        var role = await db.Users.AsNoTracking().Where(u => u.Id == userId && u.IsActive).Select(u => u.Role.Name).FirstOrDefaultAsync(ct);
        return (role == AppRoles.Registrar && await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.CustomRoleId == null, ct), false);
    }

    /// <summary>The documents of a student: a line for each kind (also the ones with nothing), in the same order for everybody.</summary>
    public async Task<IReadOnlyList<AdmissionDocumentSlot>> GetDocumentSlotsAsync(int userId, int recordId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var grade = await db.AdmissionRecords.AsNoTracking().Where(r => r.Id == recordId).Select(r => (byte?)r.GradeLevel).FirstOrDefaultAsync(ct);
        if (grade is null || !(await AccessAsync(db, userId, grade.Value, ct)).Read)
        {
            return [];
        }

        var docs = await db.AdmissionDocuments.AsNoTracking().Where(d => d.RecordId == recordId)
            .Select(d => new { d.Id, d.Kind, d.FileName, d.ContentType, HasFile = d.Data != null, d.ReceivedAtUtc }).ToListAsync(ct);
        AdmissionDocumentSlot Slot(string kind, bool other)
        {
            var mine = docs.Where(d => d.Kind == kind).OrderByDescending(d => d.HasFile).ThenByDescending(d => d.Id).FirstOrDefault();
            return mine is null
                ? new AdmissionDocumentSlot(kind, null, false, false, null, null, null, other)
                : new AdmissionDocumentSlot(kind, mine.Id, true, mine.HasFile, mine.FileName, mine.ContentType, mine.ReceivedAtUtc, other);
        }

        // Round 48: the five kinds, then the other documents the Admission staff added by typing a name (in the order they were added).
        var others = docs.Where(d => !AdmissionDocumentKinds.IsFixed(d.Kind)).GroupBy(d => d.Kind).OrderBy(g => g.Min(d => d.Id)).Select(g => g.Key);
        return [.. AdmissionDocumentKinds.All.Select(kind => Slot(kind, false)), .. others.Select(kind => Slot(kind, true))];
    }

    /// <summary>The name of an other document written in the usual way: trimmed, one space between the words, the first letter in capitals; empty when it is not a name.</summary>
    public static string? CleanOtherName(string? name)
    {
        var words = (name ?? "").Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var text = string.Join(' ', words);
        if (text.Length < 2 || text.Length > AdmissionDocumentKinds.MaxNameLength || text.Any(char.IsControl))
        {
            return null;
        }

        return char.ToUpperInvariant(text[0]) + text[1..];
    }

    /// <summary>
    /// Round 48: the Admission staff add another document by typing its name (a medical certificate, a baptismal certificate ...). The line is made with the paper in hand and/or a photo or scan.
    /// The name can't be one of the five kinds or one the student already has.
    /// </summary>
    public async Task<ServiceResult> AddOtherDocumentAsync(int userId, int recordId, string? name, bool inHand, string? fileName, string? contentType, byte[]? data, CancellationToken ct = default)
    {
        var clean = CleanOtherName(name);
        if (clean is null)
        {
            return ServiceResult.Fail($"Type the name of the document (2 to {AdmissionDocumentKinds.MaxNameLength} letters).");
        }

        if (!inHand && data is null)
        {
            return ServiceResult.Fail("Say that the paper is in hand or add the document.");
        }

        if (data is not null && data.LongLength > MaxDocumentBytes)
        {
            return ServiceResult.Fail($"The file is bigger than {MaxDocumentBytes / (1024 * 1024)} MB.");
        }

        if (data is not null && (contentType is null || !(contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) || contentType == "application/pdf")))
        {
            return ServiceResult.Fail("Add a picture or a PDF.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var grade = await db.AdmissionRecords.AsNoTracking().Where(r => r.Id == recordId).Select(r => (byte?)r.GradeLevel).FirstOrDefaultAsync(ct);
        if (grade is null || !(await AccessAsync(db, userId, grade.Value, ct)).Write)
        {
            return ServiceResult.Fail("This record was not found.");
        }

        var existing = await db.AdmissionDocuments.AsNoTracking().Where(d => d.RecordId == recordId).Select(d => d.Kind).Distinct().ToListAsync(ct);
        if (AdmissionDocumentKinds.All.Any(k => string.Equals(k, clean, StringComparison.OrdinalIgnoreCase)) || existing.Any(k => string.Equals(k, clean, StringComparison.OrdinalIgnoreCase)))
        {
            return ServiceResult.Fail("This student already has a line with this name.");
        }

        db.AdmissionDocuments.Add(new AdmissionDocument
        {
            RecordId = recordId,
            Kind = clean,
            ReceivedAtUtc = clock.GetUtcNow().UtcDateTime,
            ReceivedByUserId = userId,
            Data = data,
            FileName = data is not null && fileName is { Length: > 0 } ? Path.GetFileName(fileName) : null,
            ContentType = data is not null ? contentType : null,
            SizeBytes = data?.LongLength ?? 0,
        });
        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok($"{clean} was added.");
    }

    /// <summary>
    /// Sets one document of a student. <paramref name="inHand"/> is the paper in hand; a photo or a scan (<paramref name="data"/>) is kept with it and counts as a document. With no paper in hand and no
    /// photo the line is empty again. <paramref name="removeFile"/> takes the photo away and keeps the paper in hand.
    /// </summary>
    public async Task<ServiceResult> SetDocumentAsync(int userId, int recordId, string kind, bool inHand, string? fileName, string? contentType, byte[]? data, bool removeFile = false, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(kind) || kind.Length > AdmissionDocumentKinds.MaxNameLength)
        {
            return ServiceResult.Fail("This kind of document does not exist.");
        }

        if (data is not null && data.LongLength > MaxDocumentBytes)
        {
            return ServiceResult.Fail($"The file is bigger than {MaxDocumentBytes / (1024 * 1024)} MB.");
        }

        if (data is not null && (contentType is null || !(contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) || contentType == "application/pdf")))
        {
            return ServiceResult.Fail("Add a picture or a PDF.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var grade = await db.AdmissionRecords.AsNoTracking().Where(r => r.Id == recordId).Select(r => (byte?)r.GradeLevel).FirstOrDefaultAsync(ct);
        if (grade is null || !(await AccessAsync(db, userId, grade.Value, ct)).Write)
        {
            return ServiceResult.Fail("This record was not found.");
        }

        var rows = await db.AdmissionDocuments.Where(d => d.RecordId == recordId && d.Kind == kind).OrderByDescending(d => d.Id).ToListAsync(ct);
        if (!AdmissionDocumentKinds.IsFixed(kind) && rows.Count == 0)
        {
            return ServiceResult.Fail("This kind of document does not exist (add another document with its name).");   // a new name is added with AddOtherDocumentAsync
        }

        var doc = rows.FirstOrDefault();
        foreach (var extra in rows.Skip(1))
        {
            db.AdmissionDocuments.Remove(extra);   // one line for each kind
        }

        var keepsFile = data is not null || (doc?.Data is not null && !removeFile);
        if (!inHand && !keepsFile)
        {
            if (doc is not null)
            {
                db.AdmissionDocuments.Remove(doc);
            }

            await db.SaveChangesAsync(ct);
            return ServiceResult.Ok("The document was taken out.");
        }

        if (doc is null)
        {
            doc = new AdmissionDocument { RecordId = recordId, Kind = kind, ReceivedAtUtc = clock.GetUtcNow().UtcDateTime, ReceivedByUserId = userId };
            db.AdmissionDocuments.Add(doc);
        }

        if (data is not null)
        {
            doc.Data = data;
            doc.FileName = fileName is { Length: > 0 } ? Path.GetFileName(fileName) : null;
            doc.ContentType = contentType;
            doc.SizeBytes = data.LongLength;
        }
        else if (removeFile)
        {
            doc.Data = null;
            doc.FileName = null;
            doc.ContentType = null;
            doc.SizeBytes = 0;
        }

        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok(data is not null ? "The photo was kept." : "The document was saved.");
    }

    /// <summary>The scanned copy or photo of a document, to show in a box: the Admission staff of the grade level and the registrar.</summary>
    public async Task<(string Name, string ContentType, byte[] Data)?> OpenDocumentAsync(int userId, int documentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var doc = await db.AdmissionDocuments.AsNoTracking().Include(d => d.Record).FirstOrDefaultAsync(d => d.Id == documentId && d.Data != null, ct);
        if (doc is null || !(await AccessAsync(db, userId, doc.Record.GradeLevel, ct)).Read)
        {
            return null;
        }

        return (doc.FileName ?? doc.Kind, doc.ContentType ?? "application/octet-stream", doc.Data!);
    }

    /// <summary>
    /// Round 49: the files of the documents of a student (the ones that have a picture or a PDF), with the name and the LRN, for the Print and Download buttons of the registrar. Nothing when the person
    /// can't read the documents of the record.
    /// </summary>
    public async Task<(string Name, string Lrn, IReadOnlyList<AdmissionDocumentFile> Files)?> OpenAllDocumentsAsync(int userId, int recordId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var record = await db.AdmissionRecords.AsNoTracking().Where(r => r.Id == recordId)
            .Select(r => new { r.GradeLevel, r.Lrn, r.FirstName, r.MiddleName, r.LastName, r.NameExtension }).FirstOrDefaultAsync(ct);
        if (record is null || !(await AccessAsync(db, userId, record.GradeLevel, ct)).Read)
        {
            return null;
        }

        var files = await db.AdmissionDocuments.AsNoTracking().Where(d => d.RecordId == recordId && d.Data != null).OrderBy(d => d.Id)
            .Select(d => new { d.Id, d.Kind, d.FileName, d.ContentType, d.Data }).ToListAsync(ct);
        var name = NameCase.Title(string.Join(' ', new[] { record.FirstName, record.MiddleName, record.LastName, record.NameExtension }.Where(x => !string.IsNullOrWhiteSpace(x))));
        return (name, record.Lrn, [.. files.Select(f => new AdmissionDocumentFile(f.Id, f.Kind, f.FileName ?? f.Kind, f.ContentType ?? "application/octet-stream", f.Data!))]);
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
        return [.. rows.Select(r => new AdmissionRow(r.Id, r.GradeLevel, r.Lrn, FullName(r), Parents(r), r.Documents.Select(d => d.Kind).Distinct().Count(), false, r.CreatedAtUtc))];
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
            r.GuardianName, r.GuardianRelationship, r.GuardianPhone, r.GuardianAddress, r.EnrolledAtUtc != null, r.ParentsAddress, r.Email)).FirstOrDefaultAsync(ct);
    }

    /// <summary>The student was enrolled from the record: it leaves the list the registrar chooses from. (Called by the enrollment.)</summary>
    public async Task MarkEnrolledAsync(string lrn, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        await db.AdmissionRecords.Where(r => r.Lrn == lrn && r.EnrolledAtUtc == null).ExecuteUpdateAsync(set => set.SetProperty(r => r.EnrolledAtUtc, now), ct);
    }
}
