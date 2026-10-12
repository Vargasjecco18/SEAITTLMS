using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

// What the academic provider sees about the materials they sent: did the teacher open them, did the teacher post them, did the students open them.

/// <summary>Where a sent module is with the teacher who got it.</summary>
public static class DeliveryStates
{
    /// <summary>Sent, and the teacher has not opened it (not its text, not an activity of it, not posted).</summary>
    public const string NotOpened = "NotOpened";

    /// <summary>The teacher opened it, and it is still a draft.</summary>
    public const string Opened = "Opened";

    /// <summary>The teacher set a time in the future for the students to see it.</summary>
    public const string Scheduled = "Scheduled";

    /// <summary>The students can see it.</summary>
    public const string Posted = "Posted";

    public static readonly string[] All = [NotOpened, Opened, Scheduled, Posted];

    public static string Display(string state) => state switch
    {
        NotOpened => "Not opened",
        Opened => "Opened · draft",
        Scheduled => "Scheduled",
        Posted => "Posted",
        _ => state,
    };
}

/// <summary>One thing the provider sent to one teacher's course, with how far it got.</summary>
/// <param name="Audience">The students of the course.</param>
/// <param name="ModuleOpens">How many of them opened the module (or the syllabus). Only counts once it is posted.</param>
/// <param name="Items">Activities, quizzes and task performances in it that the students can see.</param>
/// <param name="ItemOpens">How many times (student, activity) pairs were opened.</param>
/// <param name="ItemExpected">Items × audience: how many opens there could be.</param>
public sealed record TrackingRow(
    int DeliveryId, int SubjectId, string SubjectName, string Title, bool IsSyllabus, int TeacherUserId, string TeacherName, string SectionLabel,
    DateTime SentAtUtc, string State, DateTime? OpenedAtUtc, DateTime? PostedAtUtc, int Audience, int ModuleOpens, int Items, int ItemOpens, int ItemExpected)
{
    public bool IsPosted => State == DeliveryStates.Posted;

    /// <summary>The percent of the students who opened the module, or null while it is not posted.</summary>
    public int? ModulePercent => IsPosted && Audience > 0 ? ProviderMath.Percent(ModuleOpens, Audience) : null;

    public int? ItemPercent => IsPosted && ItemExpected > 0 ? ProviderMath.Percent(ItemOpens, ItemExpected) : null;
}

public sealed record TrackingPage(IReadOnlyList<TrackingRow> Rows, int Total, int Page, int PageSize, int NotOpened, int Opened, int Scheduled, int Posted);

public sealed record TrackingFilter(string? Search = null, string? State = null, string? Kind = null, int Page = 1, int PageSize = 20);

public sealed record ItemTrack(int ItemId, string Kind, string Title, int Opens, int Submitted, int Audience);

public sealed record StudentTrack(int StudentId, int UserId, string Name, DateTime? ModuleOpenedAtUtc, int ItemsOpened, int ItemsVisible, int Submitted);

public sealed record DeliveryDetail(TrackingRow Row, IReadOnlyList<ItemTrack> Items, IReadOnlyList<StudentTrack> Students);

public sealed record TeacherTrack(
    int UserId, string Name, int Courses, int Received, int Opened, int Posted, int Waiting, int? LongestWaitingDays, DateTime? LastOpenedAtUtc, double? AverageHoursToOpen)
{
    public int? OpenedPercent => Received > 0 ? ProviderMath.Percent(Opened, Received) : null;
}

public sealed record StudentTrackRow(int UserId, int StudentId, string Name, string SectionLabel, int Available, int Opened, DateTime? LastOpenedAtUtc)
{
    public int? OpenedPercent => Available > 0 ? ProviderMath.Percent(Opened, Available) : null;
}

public sealed record StudentTrackPage(IReadOnlyList<StudentTrackRow> Rows, int Total, int Page, int PageSize);

public sealed record ProviderTeacherDetail(TeacherTrack Summary, IReadOnlyList<TrackingRow> Rows);

/// <summary>One thing a student can open that the provider sent: the module (or syllabus) or one of its activities.</summary>
public sealed record StudentOpenRow(string SubjectName, string ModuleTitle, string Title, string Kind, DateTime PostedAtUtc, DateTime? OpenedAtUtc, bool? Submitted);

public sealed record StudentDetail(int UserId, int StudentId, string Name, string SectionLabel, IReadOnlyList<StudentOpenRow> Rows)
{
    public int Opened => Rows.Count(r => r.OpenedAtUtc is not null);

    public int? OpenedPercent => Rows.Count > 0 ? ProviderMath.Percent(Opened, Rows.Count) : null;
}

public sealed record ProviderOverview(
    int LibrarySyllabi, int LibraryModules, int LibraryItems, int Sent, int TeachersReached, int TeachersWithCourses,
    int NotOpened, int Opened, int Scheduled, int Posted, int WaitingDays3,
    int StudentModuleOpens, int StudentModuleExpected, int StudentItemOpens, int StudentItemExpected,
    IReadOnlyList<CourseWithoutSyllabus> WithoutSyllabus, IReadOnlyList<TrackingRow> Waiting, IReadOnlyList<TrackingRow> RecentlyOpened)
{
    public int TeacherOpenedPercent => Sent == 0 ? 0 : ProviderMath.Percent(Opened + Scheduled + Posted, Sent);

    public int PostedPercent => Sent == 0 ? 0 : ProviderMath.Percent(Posted + Scheduled, Sent);

    public int StudentModulePercent => ProviderMath.Percent(StudentModuleOpens, StudentModuleExpected);

    public int StudentItemPercent => ProviderMath.Percent(StudentItemOpens, StudentItemExpected);
}

public sealed record CourseTrack(int SubjectId, string Name, int Sent, int Teachers, int TeacherOpened, int Posted, int ModuleOpens, int ModuleExpected, int ItemOpens, int ItemExpected)
{
    public int TeacherOpenedPercent => ProviderMath.Percent(TeacherOpened, Sent);

    public int PostedPercent => ProviderMath.Percent(Posted, Sent);

    public int? ModulePercent => ModuleExpected > 0 ? ProviderMath.Percent(ModuleOpens, ModuleExpected) : null;

    public int? ItemPercent => ItemExpected > 0 ? ProviderMath.Percent(ItemOpens, ItemExpected) : null;
}

/// <param name="Kind">"Syllabus", "Module", or the kind of the activity.</param>
public sealed record KindTrack(string Kind, int Sent, int Opens, int Expected)
{
    public int? Percent => Expected > 0 ? ProviderMath.Percent(Opens, Expected) : null;
}

public sealed record SectionTrack(string SectionLabel, int Students, int Opens, int Expected)
{
    public int? Percent => Expected > 0 ? ProviderMath.Percent(Opens, Expected) : null;
}

public sealed record WeekTrack(DateOnly WeekStart, int Sent, int OpenedByTeacher, int Posted);

public sealed record ProviderAnalytics(
    int Sent, int TeacherOpenedPercent, int PostedPercent, double? AverageHoursToOpen, double? AverageHoursToPost, int StudentModulePercent, int StudentItemPercent,
    IReadOnlyList<CourseTrack> ByCourse, IReadOnlyList<KindTrack> ByKind, IReadOnlyList<SectionTrack> BySection, IReadOnlyList<WeekTrack> Weekly,
    IReadOnlyList<TeacherTrack> SlowestTeachers, IReadOnlyList<TrackingRow> LeastOpened);

internal static class ProviderMath
{
    public static int Percent(int part, int whole) => whole <= 0 ? 0 : (int)Math.Round(100.0 * part / whole, MidpointRounding.AwayFromZero);
}

/// <summary>
/// The academic provider's monitoring: for everything they sent to the teachers, whether the teacher opened it, posted it, and whether the students
/// opened it. The pages that show it are for the academic provider. The numbers come from three records: the delivery (when the teacher first opened their
/// copy), the module views (a student opened a module or a syllabus) and the item views (a student opened an activity, a quiz or a task performance).
/// </summary>
public sealed class ProviderService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider clock, MaterialService materials)
{
    public const int PageSize = 20;

    // ======================================================================= the facts (everything is counted from these)

    private sealed record ItemFact(int Id, string Kind, string Title, DateTime? VisibleFromUtc);

    private sealed class DeliveryFact
    {
        public int Id { get; init; }
        public bool IsSyllabus { get; init; }
        public string Title { get; init; } = "";
        public int SubjectId { get; init; }
        public string SubjectName { get; init; } = "";
        public int AssignmentId { get; init; }
        public int TeacherUserId { get; init; }
        public string TeacherName { get; init; } = "";
        public int SectionId { get; init; }
        public string SectionLabel { get; init; } = "";
        public int ModuleId { get; init; }
        public DateTime SentAtUtc { get; init; }
        public DateTime? OpenedAtUtc { get; init; }
        public DateTime? VisibleFromUtc { get; init; }
        public List<ItemFact> Items { get; } = [];

        /// <summary>When the teacher opened it: the first open, or (for what was posted before opens were kept) the time they posted it.</summary>
        public DateTime? TeacherOpenedAt => OpenedAtUtc ?? VisibleFromUtc;

        public string State(DateTime now) => VisibleFromUtc is not { } v ? (OpenedAtUtc is null ? DeliveryStates.NotOpened : DeliveryStates.Opened)
            : v <= now ? DeliveryStates.Posted : DeliveryStates.Scheduled;

        public bool IsPosted(DateTime now) => VisibleFromUtc is { } v && v <= now;

        /// <summary>The activities that the students can open now.</summary>
        public IEnumerable<ItemFact> VisibleItems(DateTime now) => Items.Where(i => i.VisibleFromUtc is null || i.VisibleFromUtc <= now);
    }

    private sealed class Snapshot
    {
        public DateTime Now { get; init; }
        public List<DeliveryFact> Deliveries { get; init; } = [];
        public Dictionary<(int SectionId, int SubjectId), HashSet<int>> Roster { get; init; } = [];
        public Dictionary<(int ModuleId, int StudentId), DateTime> ModuleViews { get; init; } = [];
        public Dictionary<(int ItemId, int StudentId), DateTime> ItemViews { get; init; } = [];
        public HashSet<(int ItemId, int StudentId)> Submitted { get; init; } = [];

        public HashSet<int> StudentsOf(DeliveryFact d) => Roster.TryGetValue((d.SectionId, d.SubjectId), out var set) ? set : [];

        public int ModuleOpens(DeliveryFact d) => StudentsOf(d).Count(s => ModuleViews.ContainsKey((d.ModuleId, s)));

        public int ItemOpens(DeliveryFact d)
        {
            var students = StudentsOf(d);
            return d.VisibleItems(Now).Sum(i => students.Count(s => ItemViews.ContainsKey((i.Id, s))));
        }

        public TrackingRow Row(DeliveryFact d)
        {
            var state = d.State(Now);
            var posted = state == DeliveryStates.Posted;
            var students = StudentsOf(d);
            var visible = d.VisibleItems(Now).Count();
            return new TrackingRow(
                d.Id, d.SubjectId, d.SubjectName, d.Title, d.IsSyllabus, d.TeacherUserId, d.TeacherName, d.SectionLabel, d.SentAtUtc, state,
                d.TeacherOpenedAt, posted ? d.VisibleFromUtc : null, students.Count, posted ? ModuleOpens(d) : 0, posted ? visible : 0,
                posted ? ItemOpens(d) : 0, posted ? visible * students.Count : 0);
        }
    }

    /// <summary>Loads what the deliveries of this school year have been through. <paramref name="narrow"/> keeps only some deliveries.</summary>
    private async Task<Snapshot> LoadAsync(Func<IQueryable<MaterialDelivery>, IQueryable<MaterialDelivery>>? narrow = null, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var yearId = await MaterialService.CurrentYearIdAsync(db, ct);

        var query = db.MaterialDeliveries.AsNoTracking().Where(d => d.Assignment.Section.SchoolYearId == yearId);
        if (narrow is not null)
        {
            query = narrow(query);
        }

        var rows = await query.OrderByDescending(d => d.CreatedAtUtc).ThenByDescending(d => d.Id)
            .Select(d => new
            {
                d.Id, d.MaterialModule.IsSyllabus, d.MaterialModule.Title, d.MaterialModule.SubjectId, SubjectName = d.MaterialModule.Subject.Name,
                d.AssignmentId, TeacherUserId = d.Assignment.Teacher.UserId, d.Assignment.Teacher.FirstName, d.Assignment.Teacher.MiddleName,
                d.Assignment.Teacher.LastName, d.Assignment.Teacher.NameExtension, d.Assignment.SectionId, d.Assignment.Section.GradeLevel,
                Strand = d.Assignment.Section.Strand.Code, SectionName = d.Assignment.Section.Name, d.ModuleId, d.CreatedAtUtc, d.OpenedAtUtc,
                VisibleFromUtc = d.Module.VisibleFromUtc,
            })
            .ToListAsync(ct);

        var facts = rows.Select(r => new DeliveryFact
        {
            Id = r.Id, IsSyllabus = r.IsSyllabus, Title = r.Title, SubjectId = r.SubjectId, SubjectName = r.SubjectName, AssignmentId = r.AssignmentId,
            TeacherUserId = r.TeacherUserId, TeacherName = UserDirectory.FullName(r.FirstName, r.MiddleName, r.LastName, r.NameExtension),
            SectionId = r.SectionId, SectionLabel = TeachingService.SectionLabel(r.GradeLevel, r.Strand, r.SectionName), ModuleId = r.ModuleId,
            SentAtUtc = r.CreatedAtUtc, OpenedAtUtc = r.OpenedAtUtc, VisibleFromUtc = r.VisibleFromUtc,
        }).ToList();

        var moduleIds = facts.Select(f => f.ModuleId).ToList();
        var items = moduleIds.Count == 0 ? [] : await db.ModuleItems.AsNoTracking().Where(i => moduleIds.Contains(i.ModuleId))
            .OrderBy(i => i.Position).ThenBy(i => i.Id)
            .Select(i => new { i.Id, i.ModuleId, i.Kind, i.Title, i.VisibleFromUtc }).ToListAsync(ct);
        var byModule = facts.ToDictionary(f => f.ModuleId);
        foreach (var i in items)
        {
            byModule[i.ModuleId].Items.Add(new ItemFact(i.Id, i.Kind, i.Title, i.VisibleFromUtc));
        }

        var sectionIds = facts.Select(f => f.SectionId).Distinct().ToList();
        var pairs = sectionIds.Count == 0 ? [] : await db.Enrollments.AsNoTracking()
            .Where(e => e.Status == EnrollmentStatus.Enrolled && sectionIds.Contains(e.SectionId))
            .SelectMany(e => e.EnrolledSubjects.Select(x => new { e.SectionId, x.SubjectId, e.StudentId }))
            .Distinct().ToListAsync(ct);
        var roster = new Dictionary<(int, int), HashSet<int>>();
        foreach (var p in pairs)
        {
            if (!roster.TryGetValue((p.SectionId, p.SubjectId), out var set))
            {
                roster[(p.SectionId, p.SubjectId)] = set = [];
            }

            set.Add(p.StudentId);
        }

        var itemIds = items.Select(i => i.Id).ToList();
        var moduleViews = moduleIds.Count == 0 ? [] : await db.ModuleViews.AsNoTracking().Where(v => moduleIds.Contains(v.ModuleId))
            .Select(v => new { v.ModuleId, v.StudentId, v.FirstOpenedAtUtc }).ToListAsync(ct);
        var itemViews = itemIds.Count == 0 ? [] : await db.ItemViews.AsNoTracking().Where(v => itemIds.Contains(v.ItemId))
            .Select(v => new { v.ItemId, v.StudentId, v.FirstOpenedAtUtc }).ToListAsync(ct);
        var submitted = itemIds.Count == 0 ? [] : await db.Submissions.AsNoTracking().Where(s => itemIds.Contains(s.ItemId) && s.Status != SubmissionStatus.InProgress)
            .Select(s => new { s.ItemId, s.StudentId }).ToListAsync(ct);

        return new Snapshot
        {
            Now = now, Deliveries = facts, Roster = roster,
            ModuleViews = moduleViews.ToDictionary(v => (v.ModuleId, v.StudentId), v => v.FirstOpenedAtUtc),
            ItemViews = itemViews.ToDictionary(v => (v.ItemId, v.StudentId), v => v.FirstOpenedAtUtc),
            Submitted = submitted.Select(s => (s.ItemId, s.StudentId)).ToHashSet(),
        };
    }

    // ======================================================================= dashboard

    public async Task<ProviderOverview> GetOverviewAsync(int listSize = 6, CancellationToken ct = default)
    {
        var snap = await LoadAsync(ct: ct);
        var library = await materials.GetOverviewAsync(0, ct);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var syllabi = await db.MaterialModules.CountAsync(m => m.IsSyllabus, ct);
        var yearId = await MaterialService.CurrentYearIdAsync(db, ct);
        var withCourses = await db.TeachingAssignments.AsNoTracking().Where(a => a.Section.SchoolYearId == yearId).Select(a => a.TeacherId).Distinct().CountAsync(ct);

        var rows = snap.Deliveries.Select(snap.Row).ToList();
        var posted = rows.Where(r => r.IsPosted).ToList();
        var waiting = rows.Where(r => r.State == DeliveryStates.NotOpened).OrderBy(r => r.SentAtUtc).ToList();

        return new ProviderOverview(
            syllabi, library.LibraryModules - syllabi, library.LibraryItems, rows.Count, rows.Select(r => r.TeacherUserId).Distinct().Count(), withCourses,
            waiting.Count, rows.Count(r => r.State == DeliveryStates.Opened), rows.Count(r => r.State == DeliveryStates.Scheduled), posted.Count,
            waiting.Count(r => (snap.Now - r.SentAtUtc).TotalDays >= 3),
            posted.Sum(r => r.ModuleOpens), posted.Sum(r => r.Audience), posted.Sum(r => r.ItemOpens), posted.Sum(r => r.ItemExpected),
            library.WithoutSyllabus, waiting.Take(listSize).ToList(),
            rows.Where(r => r.OpenedAtUtc is not null && r.State != DeliveryStates.NotOpened).OrderByDescending(r => r.OpenedAtUtc).Take(listSize).ToList());
    }

    // ======================================================================= tracking

    public async Task<TrackingPage> ListTrackingAsync(TrackingFilter filter, CancellationToken ct = default)
    {
        var snap = await LoadAsync(ct: ct);
        var all = snap.Deliveries.Select(snap.Row).ToList();

        IEnumerable<TrackingRow> rows = all;
        foreach (var token in (filter.Search ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var t = token;
            rows = rows.Where(r => r.SubjectName.Contains(t, StringComparison.OrdinalIgnoreCase) || r.Title.Contains(t, StringComparison.OrdinalIgnoreCase)
                                   || r.TeacherName.Contains(t, StringComparison.OrdinalIgnoreCase) || r.SectionLabel.Contains(t, StringComparison.OrdinalIgnoreCase));
        }

        if (filter.Kind == "Syllabus")
        {
            rows = rows.Where(r => r.IsSyllabus);
        }
        else if (filter.Kind == "Module")
        {
            rows = rows.Where(r => !r.IsSyllabus);
        }

        var counted = rows.ToList();
        var shown = DeliveryStates.All.Contains(filter.State ?? "") ? counted.Where(r => r.State == filter.State).ToList() : counted;
        var page = Math.Max(1, filter.Page);
        var size = Math.Clamp(filter.PageSize, 5, 100);
        return new TrackingPage(
            shown.Skip((page - 1) * size).Take(size).ToList(), shown.Count, page, size,
            counted.Count(r => r.State == DeliveryStates.NotOpened), counted.Count(r => r.State == DeliveryStates.Opened),
            counted.Count(r => r.State == DeliveryStates.Scheduled), counted.Count(r => r.State == DeliveryStates.Posted));
    }

    public async Task<DeliveryDetail?> GetDeliveryAsync(int deliveryId, CancellationToken ct = default)
    {
        var snap = await LoadAsync(q => q.Where(d => d.Id == deliveryId), ct);
        var fact = snap.Deliveries.FirstOrDefault();
        if (fact is null)
        {
            return null;
        }

        var row = snap.Row(fact);
        var students = snap.StudentsOf(fact);
        var visible = fact.VisibleItems(snap.Now).ToList();
        var items = visible.Select(i => new ItemTrack(
            i.Id, i.Kind, i.Title, students.Count(s => snap.ItemViews.ContainsKey((i.Id, s))), students.Count(s => snap.Submitted.Contains((i.Id, s))), students.Count)).ToList();

        var people = new List<StudentTrack>();
        if (row.IsPosted && students.Count > 0)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var names = await db.Students.AsNoTracking().Where(s => students.Contains(s.Id))
                .Select(s => new { s.Id, s.UserId, s.FirstName, s.MiddleName, s.LastName, s.NameExtension }).ToListAsync(ct);
            foreach (var s in names)
            {
                people.Add(new StudentTrack(
                    s.Id, s.UserId, UserDirectory.FullName(s.FirstName, s.MiddleName, s.LastName, s.NameExtension),
                    snap.ModuleViews.TryGetValue((fact.ModuleId, s.Id), out var at) ? at : null,
                    visible.Count(i => snap.ItemViews.ContainsKey((i.Id, s.Id))), visible.Count, visible.Count(i => snap.Submitted.Contains((i.Id, s.Id)))));
            }

            // Those who have not opened it come first: they are the ones to look at.
            people = people.OrderBy(p => p.ModuleOpenedAtUtc is not null).ThenBy(p => p.ItemsOpened).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        return new DeliveryDetail(row, items, people);
    }

    // ======================================================================= users: teachers

    private static TeacherTrack TeacherSummary(Snapshot snap, int userId, string name, int courses, IReadOnlyList<DeliveryFact> sent)
    {
        var opened = sent.Where(d => d.State(snap.Now) != DeliveryStates.NotOpened).ToList();
        var waiting = sent.Where(d => d.State(snap.Now) == DeliveryStates.NotOpened).ToList();
        var hours = opened.Where(d => d.TeacherOpenedAt is not null).Select(d => Math.Max(0, (d.TeacherOpenedAt!.Value - d.SentAtUtc).TotalHours)).ToList();
        return new TeacherTrack(
            userId, name, courses, sent.Count, opened.Count, sent.Count(d => d.IsPosted(snap.Now)), waiting.Count,
            waiting.Count == 0 ? null : (int)(snap.Now - waiting.Min(d => d.SentAtUtc)).TotalDays,
            opened.Count == 0 ? null : opened.Max(d => d.TeacherOpenedAt), hours.Count == 0 ? null : hours.Average());
    }

    /// <summary>Every teacher who has a course this school year, with what they were sent and what they did with it.</summary>
    public async Task<IReadOnlyList<TeacherTrack>> ListTeachersAsync(string? search = null, CancellationToken ct = default)
    {
        var snap = await LoadAsync(ct: ct);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var yearId = await MaterialService.CurrentYearIdAsync(db, ct);
        var teachers = await db.TeachingAssignments.AsNoTracking().Where(a => a.Section.SchoolYearId == yearId)
            .GroupBy(a => new { a.Teacher.UserId, a.Teacher.FirstName, a.Teacher.MiddleName, a.Teacher.LastName, a.Teacher.NameExtension })
            .Select(g => new { g.Key.UserId, g.Key.FirstName, g.Key.MiddleName, g.Key.LastName, g.Key.NameExtension, Courses = g.Count() })
            .ToListAsync(ct);

        var result = new List<TeacherTrack>();
        foreach (var t in teachers)
        {
            var name = UserDirectory.FullName(t.FirstName, t.MiddleName, t.LastName, t.NameExtension);
            result.Add(TeacherSummary(snap, t.UserId, name, t.Courses, snap.Deliveries.Where(d => d.TeacherUserId == t.UserId).ToList()));
        }

        IEnumerable<TeacherTrack> list = result;
        foreach (var token in (search ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var s = token;
            list = list.Where(t => t.Name.Contains(s, StringComparison.OrdinalIgnoreCase));
        }

        // Those with something waiting come first, then by name.
        return list.OrderByDescending(t => t.Waiting).ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<ProviderTeacherDetail?> GetTeacherAsync(int teacherUserId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var yearId = await MaterialService.CurrentYearIdAsync(db, ct);
        var teacher = await db.Teachers.AsNoTracking().Where(t => t.UserId == teacherUserId)
            .Select(t => new { t.Id, t.FirstName, t.MiddleName, t.LastName, t.NameExtension }).FirstOrDefaultAsync(ct);
        if (teacher is null)
        {
            return null;
        }

        var courses = await db.TeachingAssignments.AsNoTracking().CountAsync(a => a.TeacherId == teacher.Id && a.Section.SchoolYearId == yearId, ct);

        var snap = await LoadAsync(q => q.Where(d => d.Assignment.Teacher.UserId == teacherUserId), ct);
        var name = UserDirectory.FullName(teacher.FirstName, teacher.MiddleName, teacher.LastName, teacher.NameExtension);
        return new ProviderTeacherDetail(TeacherSummary(snap, teacherUserId, name, courses, snap.Deliveries), snap.Deliveries.Select(snap.Row).ToList());
    }

    // ======================================================================= users: students

    /// <summary>Per student: of the posted materials sent by the provider (modules, syllabi, activities), how many they opened.</summary>
    public async Task<StudentTrackPage> ListStudentsAsync(string? search = null, int page = 1, CancellationToken ct = default)
    {
        var snap = await LoadAsync(ct: ct);
        var stats = new Dictionary<int, (string? Section, int Available, int Opened, DateTime? Last)>();
        foreach (var d in snap.Deliveries.Where(d => d.IsPosted(snap.Now)))
        {
            var visible = d.VisibleItems(snap.Now).ToList();
            foreach (var s in snap.StudentsOf(d))
            {
                stats.TryGetValue(s, out var current);
                var available = current.Available + 1 + visible.Count;
                var opened = current.Opened;
                var last = current.Last;
                if (snap.ModuleViews.TryGetValue((d.ModuleId, s), out var m))
                {
                    opened++;
                    last = last is null || m > last ? m : last;
                }

                foreach (var i in visible)
                {
                    if (snap.ItemViews.TryGetValue((i.Id, s), out var at))
                    {
                        opened++;
                        last = last is null || at > last ? at : last;
                    }
                }

                stats[s] = (current.Section ?? d.SectionLabel, available, opened, last);
            }
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var ids = stats.Keys.ToList();
        var names = ids.Count == 0 ? [] : await db.Students.AsNoTracking().Where(s => ids.Contains(s.Id))
            .Select(s => new { s.Id, s.UserId, s.FirstName, s.MiddleName, s.LastName, s.NameExtension }).ToListAsync(ct);

        IEnumerable<StudentTrackRow> rows = names.Select(n =>
        {
            var x = stats[n.Id];
            return new StudentTrackRow(n.UserId, n.Id, UserDirectory.FullName(n.FirstName, n.MiddleName, n.LastName, n.NameExtension), x.Section ?? "", x.Available, x.Opened, x.Last);
        });
        foreach (var token in (search ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var t = token;
            rows = rows.Where(r => r.Name.Contains(t, StringComparison.OrdinalIgnoreCase) || r.SectionLabel.Contains(t, StringComparison.OrdinalIgnoreCase));
        }

        // The students who opened the least come first.
        var list = rows.OrderBy(r => r.OpenedPercent ?? 0).ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();
        var p = Math.Max(1, page);
        return new StudentTrackPage(list.Skip((p - 1) * PageSize).Take(PageSize).ToList(), list.Count, p, PageSize);
    }

    public async Task<StudentDetail?> GetStudentAsync(int studentUserId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var student = await db.Students.AsNoTracking().Where(s => s.UserId == studentUserId)
            .Select(s => new { s.Id, s.FirstName, s.MiddleName, s.LastName, s.NameExtension }).FirstOrDefaultAsync(ct);
        if (student is null)
        {
            return null;
        }

        // The deliveries of the courses the student takes now.
        var pairs = await db.Enrollments.AsNoTracking().Where(e => e.StudentId == student.Id && e.Status == EnrollmentStatus.Enrolled)
            .SelectMany(e => e.EnrolledSubjects.Select(x => new { e.SectionId, x.SubjectId })).ToListAsync(ct);
        var sections = pairs.Select(p => p.SectionId).Distinct().ToList();
        var snap = await LoadAsync(q => q.Where(d => sections.Contains(d.Assignment.SectionId)), ct);
        var mine = snap.Deliveries.Where(d => snap.StudentsOf(d).Contains(student.Id) && d.IsPosted(snap.Now)).OrderBy(d => d.SubjectName).ThenBy(d => d.VisibleFromUtc).ToList();

        var rows = new List<StudentOpenRow>();
        foreach (var d in mine)
        {
            rows.Add(new StudentOpenRow(d.SubjectName, d.Title, d.Title, d.IsSyllabus ? "Syllabus" : "Module", d.VisibleFromUtc!.Value,
                snap.ModuleViews.TryGetValue((d.ModuleId, student.Id), out var at) ? at : null, null));
            foreach (var i in d.VisibleItems(snap.Now))
            {
                rows.Add(new StudentOpenRow(d.SubjectName, d.Title, i.Title, ItemKinds.Display(i.Kind), d.VisibleFromUtc!.Value,
                    snap.ItemViews.TryGetValue((i.Id, student.Id), out var seen) ? seen : null, snap.Submitted.Contains((i.Id, student.Id))));
            }
        }

        var label = mine.FirstOrDefault()?.SectionLabel ?? "";
        return new StudentDetail(studentUserId, student.Id, UserDirectory.FullName(student.FirstName, student.MiddleName, student.LastName, student.NameExtension), label, rows);
    }

    // ======================================================================= analytics

    public async Task<ProviderAnalytics> GetAnalyticsAsync(CancellationToken ct = default)
    {
        var snap = await LoadAsync(ct: ct);
        var now = snap.Now;
        var facts = snap.Deliveries;
        var rows = facts.Select(snap.Row).ToList();
        var posted = rows.Where(r => r.IsPosted).ToList();

        var openedHours = facts.Where(d => d.TeacherOpenedAt is not null).Select(d => Math.Max(0, (d.TeacherOpenedAt!.Value - d.SentAtUtc).TotalHours)).ToList();
        var postedHours = facts.Where(d => d.IsPosted(now)).Select(d => Math.Max(0, (d.VisibleFromUtc!.Value - d.SentAtUtc).TotalHours)).ToList();

        var byCourse = facts.GroupBy(d => (d.SubjectId, d.SubjectName)).Select(g =>
        {
            var r = g.Select(snap.Row).ToList();
            var p = r.Where(x => x.IsPosted).ToList();
            return new CourseTrack(
                g.Key.SubjectId, g.Key.SubjectName, g.Count(), g.Select(d => d.TeacherUserId).Distinct().Count(), r.Count(x => x.State != DeliveryStates.NotOpened), p.Count,
                p.Sum(x => x.ModuleOpens), p.Sum(x => x.Audience), p.Sum(x => x.ItemOpens), p.Sum(x => x.ItemExpected));
        }).OrderBy(c => c.ModulePercent ?? 101).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();

        // By kind: the syllabus and the modules count the students who opened them; each kind of activity counts the students who opened it.
        var kinds = new List<KindTrack>
        {
            new("Syllabus", rows.Count(r => r.IsSyllabus), posted.Where(r => r.IsSyllabus).Sum(r => r.ModuleOpens), posted.Where(r => r.IsSyllabus).Sum(r => r.Audience)),
            new("Module", rows.Count(r => !r.IsSyllabus), posted.Where(r => !r.IsSyllabus).Sum(r => r.ModuleOpens), posted.Where(r => !r.IsSyllabus).Sum(r => r.Audience)),
        };
        foreach (var kind in ItemKinds.All)
        {
            var sent = 0; var opens = 0; var expected = 0;
            foreach (var d in facts)
            {
                var isPosted = d.IsPosted(now);
                var students = snap.StudentsOf(d);
                foreach (var i in d.Items.Where(i => i.Kind == kind))
                {
                    sent++;
                    if (isPosted && (i.VisibleFromUtc is null || i.VisibleFromUtc <= now))
                    {
                        opens += students.Count(s => snap.ItemViews.ContainsKey((i.Id, s)));
                        expected += students.Count;
                    }
                }
            }

            kinds.Add(new KindTrack(ItemKinds.Display(kind), sent, opens, expected));
        }

        var bySection = posted.GroupBy(r => r.SectionLabel).Select(g =>
            new SectionTrack(g.Key, g.Max(r => r.Audience), g.Sum(r => r.ModuleOpens) + g.Sum(r => r.ItemOpens), g.Sum(r => r.Audience) + g.Sum(r => r.ItemExpected)))
            .OrderBy(s => s.Percent ?? 101).ThenBy(s => s.SectionLabel, StringComparer.OrdinalIgnoreCase).ToList();

        var thisWeek = WeekStart(DateOnly.FromDateTime(now));
        var weekly = Enumerable.Range(0, 8).Select(n => thisWeek.AddDays(-7 * (7 - n))).Select(start =>
        {
            var end = start.AddDays(7);
            var sentThen = facts.Where(d => DateOnly.FromDateTime(d.SentAtUtc) >= start && DateOnly.FromDateTime(d.SentAtUtc) < end).ToList();
            return new WeekTrack(start, sentThen.Count, sentThen.Count(d => d.State(now) != DeliveryStates.NotOpened), sentThen.Count(d => d.IsPosted(now)));
        }).ToList();

        var teachers = facts.GroupBy(d => (d.TeacherUserId, d.TeacherName))
            .Select(g => TeacherSummary(snap, g.Key.TeacherUserId, g.Key.TeacherName, g.Select(d => d.AssignmentId).Distinct().Count(), g.ToList()))
            .Where(t => t.Waiting > 0).OrderByDescending(t => t.LongestWaitingDays).ThenByDescending(t => t.Waiting).Take(8).ToList();

        var least = posted.Where(r => r.Audience > 0 && r.ModulePercent is not null).OrderBy(r => r.ModulePercent).ThenBy(r => r.SubjectName, StringComparer.OrdinalIgnoreCase).Take(8).ToList();

        return new ProviderAnalytics(
            rows.Count, rows.Count == 0 ? 0 : ProviderMath.Percent(rows.Count(r => r.State != DeliveryStates.NotOpened), rows.Count),
            rows.Count == 0 ? 0 : ProviderMath.Percent(rows.Count(r => r.State is DeliveryStates.Posted or DeliveryStates.Scheduled), rows.Count),
            openedHours.Count == 0 ? null : openedHours.Average(), postedHours.Count == 0 ? null : postedHours.Average(),
            ProviderMath.Percent(posted.Sum(r => r.ModuleOpens), posted.Sum(r => r.Audience)), ProviderMath.Percent(posted.Sum(r => r.ItemOpens), posted.Sum(r => r.ItemExpected)),
            byCourse, kinds, bySection, weekly, teachers, least);
    }

    private static DateOnly WeekStart(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
}
