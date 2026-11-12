using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Auth;
using SEAITT_LMS.Components.Shared;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>What the student writes in the application form (the text boxes and lists; the papers are <see cref="ApplicationUpload"/>).</summary>
public sealed class ApplicationInput
{
    // 1. Personal information
    public string FirstName { get; set; } = "";
    public string MiddleName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Suffix { get; set; } = "";
    public DateOnly? BirthDate { get; set; }
    public string Sex { get; set; } = "";
    public string CivilStatus { get; set; } = "";
    public string Nationality { get; set; } = "";
    public string Religion { get; set; } = "";
    public string Lrn { get; set; } = "";

    // 2. Contact information
    public string Email { get; set; } = "";
    public string MobileNumber { get; set; } = "";
    public string Address { get; set; } = "";
    public string Province { get; set; } = "";
    public string City { get; set; } = "";
    public string Barangay { get; set; } = "";
    public string ZipCode { get; set; } = "";

    // 3. Academic information
    public string AcademicYear { get; set; } = "";
    public byte TermNumber { get; set; } = 1;
    public int TrackId { get; set; }
    public int StrandId { get; set; }

    // 4. Parent / guardian information
    public string FatherName { get; set; } = "";
    public string FatherContact { get; set; } = "";
    public string MotherName { get; set; } = "";
    public string MotherContact { get; set; } = "";
    public string GuardianName { get; set; } = "";
    public string GuardianRelationship { get; set; } = "";
    public string GuardianContact { get; set; } = "";
    public string GuardianAddress { get; set; } = "";

    /// <summary>The day and hour the student will come to the school (Philippine time, as the box gives it).</summary>
    public DateTime? VisitAtLocal { get; set; }
}

/// <summary>A paper the student chose to upload.</summary>
public sealed record ApplicationUpload(string Kind, string Name, string ContentType, byte[] Data);

public sealed record ApplicationRow(
    int Id, string Reference, string Name, string Track, string Strand, byte GradeLevel, string Status, DateTime CreatedAtUtc, DateTime? VisitAtUtc, int Files, string? Lrn);

public sealed record ApplicationFileInfo(int Id, string Kind, string KindName, string Name, string ContentType, long SizeBytes);

/// <summary>An application with everything the student wrote, for the box of the admission office and of the registrar.</summary>
public sealed record ApplicationDetail(
    int Id, string Reference, string Status, string? DecisionNote, DateTime CreatedAtUtc, DateTime? VisitAtUtc, DateTime? DecidedAtUtc, string? DecidedBy,
    IReadOnlyList<(string Heading, IReadOnlyList<(string Label, string Value)> Fields)> Sections, IReadOnlyList<ApplicationFileInfo> Files, string FullName, string? Lrn);

/// <summary>What the registrar's enrollment form takes from an accepted application: the LRN, the names, the phone number, the grade level, the track and the strand.</summary>
public sealed record ApplicationFill(int Id, string? Lrn, string FirstName, string? MiddleName, string LastName, string? Suffix, string Phone, byte GradeLevel, int TrackId, int StrandId);

public sealed record ApplicationCounts(int Pending, int Accepted, int Declined, int Enrolled, int VisitsAhead)
{
    public int Total => Pending + Accepted + Declined + Enrolled;
}

/// <summary>What the analytics of the admission office show: the applications by strand, by day of the last two weeks and by place (the province).</summary>
public sealed record ApplicationAnalytics(ApplicationCounts Counts, IReadOnlyList<(string Label, int Count)> ByStrand, IReadOnlyList<(DateOnly Day, int Count)> ByDay, IReadOnlyList<(string Label, int Count)> ByCity);

/// <summary>
/// The application of a new student (Grade 11): the student fills it in on the public page, with papers and the day they will come to the school; the admission office (of that grade level)
/// accepts or declines it; the registrar sees the accepted ones, fills the enrollment from them and enrolls the student.
/// </summary>
public sealed class ApplicationService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider clock, NotificationService notifications)
{
    public const long MaxFileBytes = 5 * 1024 * 1024;

    private static readonly string[] FileTypes = ["application/pdf", "image/jpeg", "image/png"];
    private static readonly string[] FileExtensions = [".pdf", ".jpg", ".jpeg", ".png"];

    private static readonly Regex Letters = new(@"^[\p{L} .'’-]+$", RegexOptions.Compiled);

    // ---------------------------------------------------------------- what the form chooses from

    public sealed record FormChoices(IReadOnlyList<string> AcademicYears, IReadOnlyList<(int Number, string Name)> Terms, IReadOnlyList<(int Id, string Code, string Name, IReadOnlyList<(int Id, string Code, string Name)> Strands)> Tracks);

    /// <summary>The school years that are not over, the terms, and the tracks with their strands (only the strands that study in grade 11).</summary>
    public async Task<FormChoices> GetChoicesAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var today = PhilippineTime.Today(clock);
        var years = await db.SchoolYears.AsNoTracking().Where(y => y.EndDate >= today).OrderBy(y => y.StartDate).Select(y => y.Name).ToListAsync(ct);
        if (years.Count == 0)
        {
            years = await db.SchoolYears.AsNoTracking().OrderByDescending(y => y.StartDate).Take(1).Select(y => y.Name).ToListAsync(ct);
        }

        var termNumbers = await db.Terms.AsNoTracking().Select(t => (int)t.TermNumber).Distinct().OrderBy(n => n).ToListAsync(ct);
        if (termNumbers.Count == 0)
        {
            termNumbers = [1, 2, 3];
        }

        var strands = await db.Strands.AsNoTracking().Where(s => s.IsActive && db.StrandSubjects.Any(x => x.StrandId == s.Id && x.GradeLevel == 11)).OrderBy(s => s.Code)
            .Select(s => new { s.Id, s.TrackId, s.Code, s.Name }).ToListAsync(ct);
        var tracks = (await db.Tracks.AsNoTracking().Where(t => t.IsActive).OrderBy(t => t.Id).Select(t => new { t.Id, t.Code, t.Name }).ToListAsync(ct))
            .Select(t => new { t.Id, t.Code, t.Name, Strands = strands.Where(s => s.TrackId == t.Id).ToList() }).ToList();
        return new FormChoices(years, termNumbers.Select(n => (n, SchoolCalendar.TermName(n))).ToList(),
            tracks.Where(t => t.Strands.Count > 0).Select(t => (t.Id, t.Code, t.Name, (IReadOnlyList<(int, string, string)>)t.Strands.Select(s => (s.Id, s.Code, s.Name)).ToList())).ToList());
    }

    // ---------------------------------------------------------------- sending

    /// <summary>The first and last moments of a visit: Monday to Friday, 8:00 to 16:59 (Philippine time).</summary>
    public static bool IsVisitTime(DateTime local) =>
        local.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && local.Hour is >= 8 and <= 16;

    /// <summary>What is wrong with the form, one sentence each (empty when it can be sent).</summary>
    public IReadOnlyList<string> Validate(ApplicationInput a, IReadOnlyCollection<ApplicationUpload> files)
    {
        var errors = new List<string>();
        void Name(string value, string label, bool required, int max = 100)
        {
            value = (value ?? "").Trim();
            if (value.Length == 0)
            {
                if (required)
                {
                    errors.Add($"{label}: write it.");
                }
            }
            else if (value.Length > max || !Letters.IsMatch(value))
            {
                errors.Add($"{label}: letters only (up to {max}).");
            }
        }

        void Text(string value, string label, int max, bool required = true)
        {
            value = (value ?? "").Trim();
            if (value.Length == 0)
            {
                if (required)
                {
                    errors.Add($"{label}: write it.");
                }
            }
            else if (value.Length > max)
            {
                errors.Add($"{label}: at most {max} characters.");
            }
        }

        Name(a.FirstName, "First name", true);
        Name(a.MiddleName, "Middle name", false);
        Name(a.LastName, "Last name", true);
        Text(a.Suffix, "Suffix", 20, false);
        if (a.BirthDate is not { } birth)
        {
            errors.Add("Date of birth: choose it.");
        }
        else
        {
            var age = PhilippineTime.Today(clock).Year - birth.Year;
            if (age is < 10 or > 40)
            {
                errors.Add("Date of birth: check the date.");
            }
        }

        if (a.Sex is not ("Male" or "Female"))
        {
            errors.Add("Sex: choose it.");
        }

        Text(a.CivilStatus, "Civil status", 30);
        Text(a.Nationality, "Nationality", 60);
        Text(a.Religion, "Religion", 80);
        if ((a.Lrn ?? "").Trim() is { Length: > 0 } lrn && (lrn.Length != 12 || !lrn.All(char.IsAsciiDigit)))
        {
            errors.Add("LRN: 12 digits (leave it empty if you do not have it yet).");
        }

        if (EmailAddresses.Normalize(a.Email) is null)
        {
            errors.Add("Email address: write a valid one.");
        }

        if (PhoneNumbers.Normalize(a.MobileNumber) is null)
        {
            errors.Add("Mobile number: for example 09171234567.");
        }

        Text(a.Address, "Complete address", 250);
        Text(a.Province, "Province", 80);
        Text(a.City, "City / municipality", 80);
        Text(a.Barangay, "Barangay", 80);
        if ((a.ZipCode ?? "").Trim() is not { Length: >= 3 and <= 10 } zip || !zip.All(char.IsAsciiDigit))
        {
            errors.Add("ZIP code: digits only.");
        }

        Text(a.AcademicYear, "Academic year", 20);
        if (a.TermNumber < 1)
        {
            errors.Add("Semester / term: choose it.");
        }

        if (a.TrackId <= 0)
        {
            errors.Add("Track: choose it.");
        }

        if (a.StrandId <= 0)
        {
            errors.Add("Strand: choose it.");
        }

        Name(a.FatherName, "Father's full name", false, 150);
        Name(a.MotherName, "Mother's full name", false, 150);
        Name(a.GuardianName, "Guardian's full name", true, 150);
        Text(a.GuardianRelationship, "Relationship to student", 50);
        Text(a.GuardianAddress, "Guardian's address", 250);
        foreach (var (value, label, required) in new[] { (a.FatherContact, "Father's contact number", false), (a.MotherContact, "Mother's contact number", false), (a.GuardianContact, "Guardian's contact number", true) })
        {
            if ((value ?? "").Trim().Length == 0)
            {
                if (required)
                {
                    errors.Add($"{label}: write it.");
                }
            }
            else if (PhoneNumbers.Normalize(value) is null)
            {
                errors.Add($"{label}: for example 09171234567.");
            }
        }

        // the day the student will come to the school to confirm the application
        if (a.VisitAtLocal is not { } visit)
        {
            errors.Add("Visit: choose the day and the hour you will come to the school to confirm the application.");
        }
        else
        {
            var now = PhilippineTime.Now(clock).DateTime;
            if (visit < now.AddHours(1) || visit > now.AddDays(60))
            {
                errors.Add("Visit: choose a day from tomorrow to 60 days from now.");
            }
            else if (!IsVisitTime(visit))
            {
                errors.Add("Visit: the school is open Monday to Friday, from 8:00 AM to 5:00 PM.");
            }
        }

        // the four papers
        foreach (var kind in ApplicationFileKinds.All)
        {
            var file = files.FirstOrDefault(f => f.Kind == kind);
            if (file is null)
            {
                errors.Add($"{ApplicationFileKinds.Display(kind)}: upload it.");
            }
            else if (file.Data.Length == 0 || file.Data.Length > MaxFileBytes)
            {
                errors.Add($"{ApplicationFileKinds.Display(kind)}: the file can be up to {MaxFileBytes / 1024 / 1024} MB.");
            }
            else if (!FileTypes.Contains(file.ContentType) || !FileExtensions.Contains(Path.GetExtension(file.Name).ToLowerInvariant()))
            {
                errors.Add($"{ApplicationFileKinds.Display(kind)}: a PDF, a JPG or a PNG.");
            }
        }

        return errors;
    }

    /// <summary>Sends the application. The admission officers of the grade level and the registrar are told. The message has the reference the student keeps.</summary>
    public async Task<ServiceResult> SubmitAsync(ApplicationInput input, IReadOnlyList<ApplicationUpload> files, CancellationToken ct = default)
    {
        var errors = Validate(input, files);
        if (errors.Count > 0)
        {
            return ServiceResult.Fail(errors);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var email = EmailAddresses.Normalize(input.Email)!;
        var since = clock.GetUtcNow().UtcDateTime.AddDays(-1);

        // one waiting application for an email, and not many in one day (the form is public)
        if (await db.StudentApplications.AnyAsync(x => x.Email == email && x.Status == ApplicationStates.Pending, ct))
        {
            return ServiceResult.Fail("An application with this email is waiting for the admission office already. Wait for its answer, or use another email.");
        }

        if (await db.StudentApplications.CountAsync(x => x.CreatedAtUtc >= since, ct) >= 200)
        {
            return ServiceResult.Fail("Too many applications were sent today. Please try again tomorrow.");
        }

        var strand = await db.Strands.AsNoTracking().Where(s => s.Id == input.StrandId && s.TrackId == input.TrackId && s.IsActive).Select(s => new { s.Id, s.Code }).FirstOrDefaultAsync(ct);
        if (strand is null)
        {
            return ServiceResult.Fail("Strand: choose a strand of the track.");
        }

        var lrn = (input.Lrn ?? "").Trim();
        if (lrn.Length > 0 && await db.Students.AnyAsync(s => s.Lrn == lrn, ct))
        {
            return ServiceResult.Fail("A student with this LRN is enrolled in the school already.");
        }

        var year = clock.GetUtcNow().Year;
        var count = await db.StudentApplications.CountAsync(x => x.Reference.StartsWith($"APP-{year}-"), ct);
        var application = new StudentApplication
        {
            Reference = $"APP-{year}-{count + 1:0000}",
            FirstName = NameCase.Title(input.FirstName),
            MiddleName = NullIfBlank(NameCase.Title(input.MiddleName)),
            LastName = NameCase.Title(input.LastName),
            Suffix = NullIfBlank(input.Suffix),
            BirthDate = input.BirthDate!.Value,
            Sex = input.Sex,
            CivilStatus = input.CivilStatus.Trim(),
            Nationality = input.Nationality.Trim(),
            Religion = input.Religion.Trim(),
            Lrn = lrn.Length == 0 ? null : lrn,
            Email = email,
            MobileNumber = PhoneNumbers.Normalize(input.MobileNumber)!,
            Address = input.Address.Trim(),
            Province = input.Province.Trim(),
            City = input.City.Trim(),
            Barangay = input.Barangay.Trim(),
            ZipCode = input.ZipCode.Trim(),
            GradeLevel = 11,
            AcademicYear = input.AcademicYear.Trim(),
            TermNumber = input.TermNumber,
            TrackId = input.TrackId,
            StrandId = input.StrandId,
            FatherName = NullIfBlank(NameCase.Title(input.FatherName)),
            FatherContact = Phone(input.FatherContact),
            MotherName = NullIfBlank(NameCase.Title(input.MotherName)),
            MotherContact = Phone(input.MotherContact),
            GuardianName = NameCase.Title(input.GuardianName),
            GuardianRelationship = input.GuardianRelationship.Trim(),
            GuardianContact = Phone(input.GuardianContact)!,
            GuardianAddress = input.GuardianAddress.Trim(),
            VisitAtUtc = PhilippineTime.ToUtc(input.VisitAtLocal!.Value),
        };
        foreach (var f in files)
        {
            application.Files.Add(new ApplicationFile { Kind = f.Kind, Name = SafeName(f.Name), ContentType = f.ContentType, SizeBytes = f.Data.Length, Data = f.Data });
        }

        db.StudentApplications.Add(application);
        await db.SaveChangesAsync(ct);

        // the admission officers of Grade 11 (the application is for Grade 11) and the registrar are told; the officers also get the day of the visit
        var visit = PhilippineTime.FromUtc(application.VisitAtUtc!.Value);
        var officers = await db.AdmissionOfficers.AsNoTracking().Where(o => o.User.IsActive && o.GradeLevel == 11).Select(o => o.UserId).ToListAsync(ct);
        var registrars = await db.Users.AsNoTracking().Where(u => u.IsActive && u.RoleId == AppRoles.RegistrarId).Select(u => u.Id).ToListAsync(ct);
        await notifications.AddForManyAsync(officers, "application", $"New student application: {application.FullName}",
            $"{application.Reference} · Grade 11 {strand.Code}. The student will come to the school on {PhilippineTime.FormatDateTime(application.VisitAtUtc.Value)}.", "admission/applications", ct);
        await notifications.AddForManyAsync(registrars, "application", $"New student application: {application.FullName}",
            $"{application.Reference} · Grade 11 {strand.Code}. The admission office will accept or decline it; the accepted students appear in Enrollment.", "registrar/enrollments", ct);
        _ = visit;
        return ServiceResult.Ok($"Your application was sent. Your reference is {application.Reference}. Come to the school on {PhilippineTime.FormatDateTime(application.VisitAtUtc.Value)} to confirm it.", application.Id);
    }

    private static string? NullIfBlank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static string? Phone(string? text) => string.IsNullOrWhiteSpace(text) ? null : PhoneNumbers.Normalize(text);

    private static string SafeName(string name)
    {
        var clean = new string(Path.GetFileName(name).Where(c => !char.IsControl(c) && c is not ('\\' or '/' or '"')).ToArray());
        return clean.Length is > 0 and <= 200 ? clean : "file" + Path.GetExtension(clean);
    }

    // ---------------------------------------------------------------- who may look

    private sealed record Viewer(string Role, byte? GradeLevel);

    private static async Task<Viewer?> ViewerAsync(AppDbContext db, int userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().Where(u => u.Id == userId && u.IsActive).Select(u => new { u.RoleId }).FirstOrDefaultAsync(ct);
        if (user is null)
        {
            return null;
        }

        if (user.RoleId == AppRoles.AdmissionId)
        {
            var grade = await db.AdmissionOfficers.AsNoTracking().Where(o => o.UserId == userId).Select(o => (byte?)o.GradeLevel).FirstOrDefaultAsync(ct);
            return grade is null ? null : new Viewer(AppRoles.Admission, grade);
        }

        return user.RoleId == AppRoles.RegistrarId ? new Viewer(AppRoles.Registrar, null) : user.RoleId == AppRoles.AdminId ? new Viewer(AppRoles.Admin, null) : null;
    }

    /// <summary>The grade level an admission officer works for (11 or 12), or null for anybody else.</summary>
    public async Task<byte?> GradeOfOfficerAsync(int userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.AdmissionOfficers.AsNoTracking().Where(o => o.UserId == userId && o.User.IsActive).Select(o => (byte?)o.GradeLevel).FirstOrDefaultAsync(ct);
    }

    // ---------------------------------------------------------------- the lists

    private static IQueryable<StudentApplication> Visible(AppDbContext db, Viewer viewer) => viewer.Role switch
    {
        // the admission office sees the applications of its grade level; the registrar only those the admission office accepted (and the ones that became students)
        AppRoles.Admission => db.StudentApplications.Where(a => a.GradeLevel == viewer.GradeLevel),
        AppRoles.Registrar => db.StudentApplications.Where(a => a.Status == ApplicationStates.Accepted || a.Status == ApplicationStates.Enrolled),
        AppRoles.Admin => db.StudentApplications,
        _ => db.StudentApplications.Where(_ => false),
    };

    public async Task<IReadOnlyList<ApplicationRow>> ListAsync(int userId, string? state = null, string? search = null, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (await ViewerAsync(db, userId, ct) is not { } viewer)
        {
            return [];
        }

        var query = Visible(db, viewer).AsNoTracking();
        if (!string.IsNullOrEmpty(state) && ApplicationStates.All.Contains(state))
        {
            query = query.Where(a => a.Status == state);
        }

        foreach (var token in (search ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(5))
        {
            var t = token;
            query = query.Where(a => a.FirstName.Contains(t) || a.LastName.Contains(t) || a.Reference.Contains(t) || a.Email.Contains(t) || (a.Lrn != null && a.Lrn.Contains(t)));
        }

        var rows = await query.OrderByDescending(a => a.Status == ApplicationStates.Pending).ThenByDescending(a => a.CreatedAtUtc).ThenByDescending(a => a.Id).Take(500)
            .Select(a => new { a.Id, a.Reference, a.FirstName, a.MiddleName, a.LastName, a.Suffix, Track = a.Track.Code, Strand = a.Strand.Code, a.GradeLevel, a.Status, a.CreatedAtUtc, a.VisitAtUtc, Files = a.Files.Count, a.Lrn })
            .ToListAsync(ct);
        return [.. rows.Select(a => new ApplicationRow(a.Id, a.Reference, UserDirectory.FullName(a.FirstName, a.MiddleName, a.LastName, a.Suffix), a.Track, a.Strand, a.GradeLevel, a.Status, a.CreatedAtUtc, a.VisitAtUtc, a.Files, a.Lrn))];
    }

    /// <summary>For the registrar: the students the admission office accepted and that are not enrolled yet.</summary>
    public async Task<IReadOnlyList<ApplicationRow>> ListAcceptedAsync(int userId, CancellationToken ct = default) =>
        (await ListAsync(userId, ApplicationStates.Accepted, null, ct));

    public async Task<ApplicationCounts> GetCountsAsync(int userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (await ViewerAsync(db, userId, ct) is not { } viewer)
        {
            return new ApplicationCounts(0, 0, 0, 0, 0);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var all = Visible(db, viewer).AsNoTracking();
        return new ApplicationCounts(
            await all.CountAsync(a => a.Status == ApplicationStates.Pending, ct),
            await all.CountAsync(a => a.Status == ApplicationStates.Accepted, ct),
            await all.CountAsync(a => a.Status == ApplicationStates.Declined, ct),
            await all.CountAsync(a => a.Status == ApplicationStates.Enrolled, ct),
            await all.CountAsync(a => a.Status == ApplicationStates.Pending && a.VisitAtUtc != null && a.VisitAtUtc >= now, ct));
    }

    public async Task<ApplicationAnalytics> GetAnalyticsAsync(int userId, CancellationToken ct = default)
    {
        var counts = await GetCountsAsync(userId, ct);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (await ViewerAsync(db, userId, ct) is not { } viewer)
        {
            return new ApplicationAnalytics(counts, [], [], []);
        }

        var all = Visible(db, viewer).AsNoTracking();
        var strands = await all.GroupBy(a => a.Strand.Code).Select(g => new { Label = g.Key, Count = g.Count() }).OrderByDescending(x => x.Count).ThenBy(x => x.Label).ToListAsync(ct);
        var cities = await all.GroupBy(a => a.City).Select(g => new { Label = g.Key, Count = g.Count() }).OrderByDescending(x => x.Count).ThenBy(x => x.Label).Take(8).ToListAsync(ct);
        var today = PhilippineTime.Today(clock);
        var since = PhilippineTime.ToUtc(today.AddDays(-13).ToDateTime(TimeOnly.MinValue));
        var days = (await all.Where(a => a.CreatedAtUtc >= since).Select(a => a.CreatedAtUtc).ToListAsync(ct)).GroupBy(PhilippineTime.DayOf).ToDictionary(g => g.Key, g => g.Count());
        return new ApplicationAnalytics(counts, strands.Select(x => (x.Label, x.Count)).ToList(),
            Enumerable.Range(0, 14).Select(n => today.AddDays(n - 13)).Select(d => (d, days.GetValueOrDefault(d))).ToList(), cities.Select(x => (x.Label, x.Count)).ToList());
    }

    /// <summary>The visits coming (pending applications), the soonest first, for the dashboard and the calendar of the admission office.</summary>
    public async Task<IReadOnlyList<ApplicationRow>> ListVisitsAsync(int userId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (await ViewerAsync(db, userId, ct) is not { } viewer || viewer.Role == AppRoles.Admin)
        {
            return [];
        }

        var rows = await Visible(db, viewer).AsNoTracking().Where(a => a.VisitAtUtc != null && a.VisitAtUtc >= fromUtc && a.VisitAtUtc < toUtc && a.Status != ApplicationStates.Declined)
            .OrderBy(a => a.VisitAtUtc).Take(300)
            .Select(a => new { a.Id, a.Reference, a.FirstName, a.MiddleName, a.LastName, a.Suffix, Track = a.Track.Code, Strand = a.Strand.Code, a.GradeLevel, a.Status, a.CreatedAtUtc, a.VisitAtUtc, Files = a.Files.Count, a.Lrn })
            .ToListAsync(ct);
        return [.. rows.Select(a => new ApplicationRow(a.Id, a.Reference, UserDirectory.FullName(a.FirstName, a.MiddleName, a.LastName, a.Suffix), a.Track, a.Strand, a.GradeLevel, a.Status, a.CreatedAtUtc, a.VisitAtUtc, a.Files, a.Lrn))];
    }

    // ---------------------------------------------------------------- one application

    public async Task<ApplicationDetail?> GetAsync(int userId, int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (await ViewerAsync(db, userId, ct) is not { } viewer)
        {
            return null;
        }

        var a = await Visible(db, viewer).AsNoTracking().Include(x => x.Track).Include(x => x.Strand).Include(x => x.Files).FirstOrDefaultAsync(x => x.Id == id, ct);
        return a is null ? null : await ToDetailAsync(db, a, ct);
    }

    /// <summary>The application of a student who is enrolled (the Info button of Student's Accounts): found by the student record it made, or by the LRN.</summary>
    public async Task<ApplicationDetail?> GetForStudentAsync(int userId, int studentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (await ViewerAsync(db, userId, ct) is not { } viewer)
        {
            return null;
        }

        var lrn = await db.Students.AsNoTracking().Where(s => s.Id == studentId).Select(s => s.Lrn).FirstOrDefaultAsync(ct);
        if (lrn is null)
        {
            return null;
        }

        // the admission office looks at the students of its own grade level, whatever the state of the application
        var query = viewer.Role == AppRoles.Admission ? db.StudentApplications.Where(a => a.GradeLevel == viewer.GradeLevel) : db.StudentApplications.Where(a => true);
        var a = await query.AsNoTracking().Include(x => x.Track).Include(x => x.Strand).Include(x => x.Files)
            .Where(x => x.EnrolledStudentId == studentId || (x.Lrn != null && x.Lrn == lrn))
            .OrderByDescending(x => x.EnrolledStudentId == studentId).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        return a is null ? null : await ToDetailAsync(db, a, ct);
    }

    private static async Task<ApplicationDetail> ToDetailAsync(AppDbContext db, StudentApplication a, CancellationToken ct)
    {
        var decidedBy = a.DecidedByUserId is { } by ? (await UserDirectory.LoadAsync(db, [by], ct)).GetValueOrDefault(by)?.Name : null;
        string Show(string? text) => string.IsNullOrWhiteSpace(text) ? "—" : text;
        (string, string) F(string label, string? value) => (label, Show(value));

        IReadOnlyList<(string, IReadOnlyList<(string, string)>)> sections =
        [
            ("1. Personal information", [F("First name", a.FirstName), F("Middle name", a.MiddleName), F("Last name", a.LastName), F("Suffix", a.Suffix), F("Date of birth", a.BirthDate.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture)),
                F("Sex", a.Sex), F("Civil status", a.CivilStatus), F("Nationality", a.Nationality), F("Religion", a.Religion), F("LRN", a.Lrn)]),
            ("2. Contact information", [F("Email address", a.Email), F("Mobile number", a.MobileNumber), F("Complete address", a.Address), F("Province", a.Province), F("City / municipality", a.City),
                F("Barangay", a.Barangay), F("ZIP code", a.ZipCode)]),
            ("3. Academic information", [F("Applying grade level", "Grade " + a.GradeLevel), F("Academic year", a.AcademicYear), F("Semester / term", SchoolCalendar.TermName(a.TermNumber)),
                F("Track", $"{a.Track.Code} · {a.Track.Name}"), F("Strand", $"{a.Strand.Code} · {a.Strand.Name}")]),
            ("4. Parent / guardian information", [F("Father's full name", a.FatherName), F("Father's contact number", a.FatherContact), F("Mother's full name", a.MotherName), F("Mother's contact number", a.MotherContact),
                F("Guardian's full name", a.GuardianName), F("Relationship to student", a.GuardianRelationship), F("Guardian's contact number", a.GuardianContact), F("Guardian's address", a.GuardianAddress)]),
        ];
        var files = a.Files.OrderBy(f => Array.IndexOf(ApplicationFileKinds.All, f.Kind)).Select(f => new ApplicationFileInfo(f.Id, f.Kind, ApplicationFileKinds.Display(f.Kind), f.Name, f.ContentType, f.SizeBytes)).ToList();
        return new ApplicationDetail(a.Id, a.Reference, a.Status, a.DecisionNote, a.CreatedAtUtc, a.VisitAtUtc, a.DecidedAtUtc, decidedBy, sections, files, a.FullName, a.Lrn);
    }

    /// <summary>A paper of an application, for the people who may look at the application (the admission office of that grade level, the registrar for the accepted ones, the administrator).</summary>
    public async Task<(string Name, string ContentType, byte[] Data)?> OpenFileAsync(int userId, int fileId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (await ViewerAsync(db, userId, ct) is not { } viewer)
        {
            return null;
        }

        var applicationId = await db.ApplicationFiles.AsNoTracking().Where(f => f.Id == fileId).Select(f => (int?)f.ApplicationId).FirstOrDefaultAsync(ct);
        if (applicationId is null)
        {
            return null;
        }

        // the registrar also opens the papers of a student who was enrolled from an application, whatever its state
        var allowed = await Visible(db, viewer).AnyAsync(a => a.Id == applicationId, ct);
        if (!allowed)
        {
            return null;
        }

        var f = await db.ApplicationFiles.AsNoTracking().Where(x => x.Id == fileId).Select(x => new { x.Name, x.ContentType, x.Data }).FirstAsync(ct);
        return (f.Name, f.ContentType, f.Data);
    }

    // ---------------------------------------------------------------- the decision

    /// <summary>The admission office accepts or declines a pending application of its grade level. The student is not told by the system (they have no account yet); the registrar is told of an accepted one.</summary>
    public async Task<ServiceResult> DecideAsync(int userId, int id, bool accept, string? note, CancellationToken ct = default)
    {
        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (note is { Length: > 500 })
        {
            return ServiceResult.Fail("The note can have at most 500 characters.");
        }

        if (!accept && note is null)
        {
            return ServiceResult.Fail("Write why the application is declined.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (await ViewerAsync(db, userId, ct) is not { Role: AppRoles.Admission } viewer)
        {
            return ServiceResult.Fail("Only the admission office accepts or declines an application.");
        }

        var application = await db.StudentApplications.Include(a => a.Strand).FirstOrDefaultAsync(a => a.Id == id && a.GradeLevel == viewer.GradeLevel, ct);
        if (application is null)
        {
            return ServiceResult.Fail("This application is not there.");
        }

        if (application.Status != ApplicationStates.Pending)
        {
            return ServiceResult.Fail($"This application was {application.Status.ToLowerInvariant()} already.");
        }

        application.Status = accept ? ApplicationStates.Accepted : ApplicationStates.Declined;
        application.DecisionNote = note;
        application.DecidedByUserId = userId;
        application.DecidedAtUtc = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);

        if (accept)
        {
            var registrars = await db.Users.AsNoTracking().Where(u => u.IsActive && u.RoleId == AppRoles.RegistrarId).Select(u => u.Id).ToListAsync(ct);
            await notifications.AddForManyAsync(registrars, "application", $"Accepted student: {application.FullName}",
                $"{application.Reference} · Grade {application.GradeLevel} {application.Strand.Code}. Open Enrollment and press Accepted students to enroll them.", "registrar/enrollments", ct);
        }

        return ServiceResult.Ok(accept ? $"{application.FullName} was accepted. The registrar can enroll the student." : $"{application.FullName} was declined.");
    }

    /// <summary>For the registrar: what the enrollment form is filled with when an accepted student is chosen. The phone number is written as the form writes it (09171234567).</summary>
    public async Task<ApplicationFill?> GetFillAsync(int userId, int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (await ViewerAsync(db, userId, ct) is not { Role: AppRoles.Registrar })
        {
            return null;
        }

        var a = await db.StudentApplications.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.Status == ApplicationStates.Accepted, ct);
        if (a is null)
        {
            return null;
        }

        var phone = a.MobileNumber.StartsWith("+63", StringComparison.Ordinal) ? "0" + a.MobileNumber[3..] : a.MobileNumber;
        return new ApplicationFill(a.Id, a.Lrn, a.FirstName, a.MiddleName, a.LastName, a.Suffix, phone, a.GradeLevel, a.TrackId, a.StrandId);
    }

    /// <summary>The registrar enrolled the student: the application is marked Enrolled (found by its number, or by the LRN of the student).</summary>
    public async Task MarkEnrolledAsync(int? applicationId, int studentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var lrn = await db.Students.AsNoTracking().Where(s => s.Id == studentId).Select(s => s.Lrn).FirstOrDefaultAsync(ct);
        var application = await db.StudentApplications.Where(a => a.Status == ApplicationStates.Accepted && (a.Id == applicationId || (lrn != null && a.Lrn == lrn))).OrderByDescending(a => a.Id == applicationId).FirstOrDefaultAsync(ct);
        if (application is null)
        {
            return;
        }

        application.Status = ApplicationStates.Enrolled;
        application.EnrolledStudentId = studentId;
        await db.SaveChangesAsync(ct);
    }
}
