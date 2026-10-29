using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Data.Queries;

public sealed record StrandCount(string Code, string Track, int Count);

/// <summary>How many students are enrolled in one section for the current term. <paramref name="Label"/> is like 11-BUSSENT-A.</summary>
public sealed record SectionCount(string Label, int Count);

public sealed record EnrollmentRow(
    int Id,
    string StudentName,
    string Lrn,
    byte GradeLevel,
    string Track,
    string Strand,
    string Section,
    string Term,
    string SchoolYear,
    string? Subjects,
    string Status,
    DateTime EnrolledAtUtc,
    int StudentId = 0,
    int? NextEnrollmentId = null,
    string? Phone = null,
    decimal? FinalGrade = null,
    bool StudentActive = true);

/// <summary>Everything the registrar's dashboard shows.</summary>
/// <param name="TermLabel">For example "1st Term · 2026-2027". Null when no term is marked as current.</param>
public sealed record RegistrarOverview(
    string? TermLabel,
    int EnrolledThisTerm,
    int EnrolledToday,
    int StudentsTotal,
    int SectionsThisYear,
    IReadOnlyList<StrandCount> ByStrand,
    IReadOnlyList<EnrollmentRow> Recent,
    NotEnrolledPage? NotEnrolled = null,
    IReadOnlyList<SectionCount>? BySection = null)
{
    /// <param name="startOfTodayUtc">When today began (in Philippine time), as a UTC moment.</param>
    public static async Task<RegistrarOverview> LoadAsync(AppDbContext db, DateTime startOfTodayUtc, int recent = 8, CancellationToken ct = default)
    {
        var term = await db.Terms.AsNoTracking()
            .Where(t => t.IsCurrent)
            .Select(t => new { t.Id, t.Name, t.SchoolYearId, SchoolYear = t.SchoolYear.Name })
            .FirstOrDefaultAsync(ct);

        var studentsTotal = await db.Students.CountAsync(ct);
        var enrolledToday = await db.Enrollments.CountAsync(e => e.CreatedAtUtc >= startOfTodayUtc, ct);
        var latest = await EnrollmentList.LoadAsync(db, new EnrollmentFilter(null, null, null), 1, recent, ct);
        var notEnrolled = await NotEnrolledList.LoadAsync(db, null, 1, 6, ct);

        if (term is null)
        {
            return new RegistrarOverview(null, 0, enrolledToday, studentsTotal, 0, [], latest.Items, notEnrolled);
        }

        var enrolledThisTerm = await db.Enrollments.CountAsync(e => e.TermId == term.Id, ct);
        var sections = await db.Sections.CountAsync(s => s.SchoolYearId == term.SchoolYearId, ct);

        var perStrand = await db.Enrollments.AsNoTracking()
            .Where(e => e.TermId == term.Id)
            .GroupBy(e => e.StrandId)
            .Select(g => new { StrandId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var strands = await db.Strands.AsNoTracking().Where(s => s.IsActive)
            .OrderBy(s => s.Track.Id).ThenBy(s => s.Id)
            .Select(s => new { s.Id, s.Code, Track = s.Track.Code })
            .ToListAsync(ct);
        var byStrand = strands
            .Select(s => new StrandCount(s.Code, s.Track, perStrand.FirstOrDefault(p => p.StrandId == s.Id)?.Count ?? 0))
            .ToList();

        // Every section of this school year with how many students of it are enrolled for the current term.
        var perSection = await db.Enrollments.AsNoTracking()
            .Where(e => e.TermId == term.Id)
            .GroupBy(e => e.SectionId)
            .Select(g => new { SectionId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var sectionRows = await db.Sections.AsNoTracking()
            .Where(s => s.SchoolYearId == term.SchoolYearId)
            .OrderBy(s => s.GradeLevel).ThenBy(s => s.Strand.Code).ThenBy(s => s.Name)
            .Select(s => new { s.Id, s.GradeLevel, Strand = s.Strand.Code, s.Name })
            .ToListAsync(ct);
        var bySection = sectionRows
            .Select(s => new SectionCount($"{s.GradeLevel}-{s.Strand}-{s.Name}", perSection.FirstOrDefault(p => p.SectionId == s.Id)?.Count ?? 0))
            .ToList();

        return new RegistrarOverview($"{term.Name} · {term.SchoolYear}", enrolledThisTerm, enrolledToday, studentsTotal, sections, byStrand, latest.Items, notEnrolled, bySection);
    }
}

/// <param name="NextTerm">"enrolled" or "none": only students who are (or are not) enrolled for the next term. Empty for all.</param>
public sealed record EnrollmentFilter(string? Search, int? TermId, int? StrandId, string? NextTerm = null);

/// <param name="NextTerm">The term after the current one, or null when there is none.</param>
public sealed record EnrollmentPage(IReadOnlyList<EnrollmentRow> Items, int Total, int Page, int PageSize, TermRef? NextTerm = null)
{
    public int PageCount => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

/// <summary>The enrollments, newest first, for the registrar's list and the dashboard.</summary>
public static class EnrollmentList
{
    /// <param name="withFinalGrade">Also work out the final grade of each enrollment (the registrar's student list shows it).</param>
    public static async Task<EnrollmentPage> LoadAsync(AppDbContext db, EnrollmentFilter filter, int page, int pageSize, CancellationToken ct = default, bool withFinalGrade = false)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Enrollments.AsNoTracking().AsQueryable();
        var next = await TermLookup.NextAsync(db, ct);

        // Every word typed must match somewhere: the name or the LRN.
        foreach (var word in (filter.Search ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(6))
        {
            var w = word;
            query = query.Where(e => e.FirstName.Contains(w) || (e.MiddleName != null && e.MiddleName.Contains(w)) || e.LastName.Contains(w) || e.Lrn.Contains(w));
        }

        if (filter.TermId is { } termId)
        {
            query = query.Where(e => e.TermId == termId);
        }

        if (filter.StrandId is { } strandId)
        {
            query = query.Where(e => e.StrandId == strandId);
        }

        // Who is enrolled for the next term (the registrar can unenroll them) and who still has to be enrolled.
        if (next is not null && filter.NextTerm is "enrolled" or "none")
        {
            var nextId = next.Id;
            query = filter.NextTerm == "enrolled"
                ? query.Where(e => db.Enrollments.Any(n => n.StudentId == e.StudentId && n.TermId == nextId && n.Status == EnrollmentStatus.Enrolled))
                : query.Where(e => !db.Enrollments.Any(n => n.StudentId == e.StudentId && n.TermId == nextId && n.Status == EnrollmentStatus.Enrolled));
        }

        var total = await query.CountAsync(ct);

        // A page number past the end (an old link, or the last row was removed) shows the last page.
        page = Math.Min(page, Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)));

        var rows = await query
            .OrderByDescending(e => e.CreatedAtUtc).ThenByDescending(e => e.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(e => new
            {
                e.Id, e.StudentId, e.FirstName, e.MiddleName, e.LastName, e.Lrn, e.GradeLevel,
                Track = e.Track.Code, Strand = e.Strand.Code, e.NameExtension,
                Section = e.Section.Name, Term = e.Term.Name, SchoolYear = e.Term.SchoolYear.Name,
                e.Subjects, e.Status, e.CreatedAtUtc, Phone = e.Student.User.PhoneNumber, StudentActive = e.Student.Status == StudentStatus.Active,
            })
            .ToListAsync(ct);

        var nextEnrollments = new Dictionary<int, int>();
        if (next is not null)
        {
            var studentIds = rows.Select(r => r.StudentId).Distinct().ToList();
            nextEnrollments = (await db.Enrollments.AsNoTracking()
                    .Where(e => e.TermId == next.Id && e.Status == EnrollmentStatus.Enrolled && studentIds.Contains(e.StudentId))
                    .Select(e => new { e.StudentId, e.Id })
                    .ToListAsync(ct))
                .ToDictionary(e => e.StudentId, e => e.Id);
        }

        var finalGrades = withFinalGrade ? await FinalGrades.LoadAsync(db, rows.Select(r => r.Id).ToList(), ct) : new Dictionary<int, decimal>();

        var items = rows.Select(r => new EnrollmentRow(
            r.Id,
            string.Join(' ', new[] { r.FirstName, r.MiddleName, r.LastName, r.NameExtension }.Where(p => !string.IsNullOrWhiteSpace(p))),
            r.Lrn, r.GradeLevel, r.Track, r.Strand,
            $"{r.GradeLevel}-{r.Strand}-{r.Section}", r.Term, r.SchoolYear, r.Subjects, r.Status.ToString(), r.CreatedAtUtc,
            r.StudentId, nextEnrollments.TryGetValue(r.StudentId, out var nextId) ? nextId : null, r.Phone,
            finalGrades.TryGetValue(r.Id, out var finalGrade) ? finalGrade : null, r.StudentActive)).ToList();

        return new EnrollmentPage(items, total, page, pageSize, next);
    }
}
