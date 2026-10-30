using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

public enum FriendState
{
    None,
    Friends,

    /// <summary>The signed-in person asked and waits for an answer.</summary>
    RequestSent,

    /// <summary>The other person asked the signed-in person.</summary>
    RequestReceived,
}

/// <summary>A person in a list of the Users page: who, what they are to the viewer ("11-BUSSENT-A", the courses they teach), and the friendship.</summary>
public sealed record PersonCard(int UserId, string Name, string Role, string? Detail, FriendState State = FriendState.None)
{
    public bool IsStudent => Role == AppRoles.Student;
}

public sealed record FriendRequests(IReadOnlyList<PersonCard> Received, IReadOnlyList<PersonCard> Sent);

/// <summary>
/// The people a student or a teacher can see in the Users page: a student sees the classmates (the students of the sections they are in),
/// all the students of the school, the teachers of their courses and their friends; a teacher sees their students, all the other teachers
/// and their friends. Friends can be
/// anyone of the two roles. The lists only read, the friendship itself is changed through <see cref="FriendService"/>.
/// </summary>
public sealed class PeopleService(IDbContextFactory<AppDbContext> dbFactory)
{
    private static readonly string[] FriendRoles = [AppRoles.Student, AppRoles.Teacher];

    // ----------------------------------------------------------------- the lists

    /// <summary>The students who are in a section of the student (the other students of their courses).</summary>
    public async Task<IReadOnlyList<PersonCard>> GetClassmatesAsync(int userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var studentId = await db.Students.AsNoTracking().Where(s => s.UserId == userId).Select(s => (int?)s.Id).FirstOrDefaultAsync(ct);
        if (studentId is null)
        {
            return [];
        }

        var mySections = await db.Enrollments.AsNoTracking()
            .Where(e => e.StudentId == studentId && e.Status == EnrollmentStatus.Enrolled)
            .Select(e => e.SectionId).Distinct().ToListAsync(ct);

        var rows = await db.Enrollments.AsNoTracking()
            .Where(e => mySections.Contains(e.SectionId) && e.Status == EnrollmentStatus.Enrolled && e.StudentId != studentId && e.Student.User.IsActive)
            .Select(e => new { e.Student.UserId, e.Student.FirstName, e.Student.MiddleName, e.Student.LastName, e.Student.NameExtension, e.GradeLevel, Strand = e.Strand.Code, Section = e.Section.Name })
            .ToListAsync(ct);

        var cards = rows
            .GroupBy(r => r.UserId)
            .Select(g =>
            {
                var r = g.First();
                return new PersonCard(r.UserId, UserDirectory.FullName(r.FirstName, r.MiddleName, r.LastName, r.NameExtension), AppRoles.Student,
                    string.Join(", ", g.Select(x => TeachingService.SectionLabel(x.GradeLevel, x.Strand, x.Section)).Distinct()));
            })
            .OrderBy(c => c.Name)
            .ToList();

        return await WithStatesAsync(db, userId, cards, ct);
    }

    /// <summary>Every other student of the school (active accounts), each with the section they are in. For the "Students" list of a student.</summary>
    public async Task<IReadOnlyList<PersonCard>> GetAllStudentsAsync(int userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var ids = await db.Students.AsNoTracking()
            .Where(s => s.UserId != userId && s.Status == StudentStatus.Active && s.User.IsActive)
            .Select(s => s.UserId).ToListAsync(ct);

        var cards = (await CardsAsync(db, ids, ct)).OrderBy(c => c.Name).ToList();
        return await WithStatesAsync(db, userId, cards, ct);
    }

    /// <summary>The teachers of the courses the student takes now, each with the courses they teach the student.</summary>
    public async Task<IReadOnlyList<PersonCard>> GetMyTeachersAsync(int userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var studentId = await db.Students.AsNoTracking().Where(s => s.UserId == userId).Select(s => (int?)s.Id).FirstOrDefaultAsync(ct);
        if (studentId is null)
        {
            return [];
        }

        var rows = await Roster.AssignmentsOf(db, studentId.Value).AsNoTracking()
            .Where(a => a.Teacher.User.IsActive)
            .Select(a => new { a.Teacher.UserId, a.Teacher.FirstName, a.Teacher.MiddleName, a.Teacher.LastName, a.Teacher.NameExtension, Subject = a.Subject.Name })
            .ToListAsync(ct);

        var cards = rows
            .GroupBy(r => r.UserId)
            .Select(g =>
            {
                var r = g.First();
                return new PersonCard(r.UserId, UserDirectory.FullName(r.FirstName, r.MiddleName, r.LastName, r.NameExtension), AppRoles.Teacher,
                    string.Join(", ", g.Select(x => x.Subject).Distinct().OrderBy(x => x)));
            })
            .OrderBy(c => c.Name)
            .ToList();

        return await WithStatesAsync(db, userId, cards, ct);
    }

    /// <summary>The students in the courses of the teacher, each with the courses (and sections) they share.</summary>
    public async Task<IReadOnlyList<PersonCard>> GetMyStudentsAsync(int userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var teacherId = await db.Teachers.AsNoTracking().Where(t => t.UserId == userId).Select(t => (int?)t.Id).FirstOrDefaultAsync(ct);
        if (teacherId is null)
        {
            return [];
        }

        var courses = await db.TeachingAssignments.AsNoTracking().Where(a => a.TeacherId == teacherId)
            .Select(a => new { a.SectionId, a.SubjectId, Subject = a.Subject.Name, Grade = a.Section.GradeLevel, Strand = a.Section.Strand.Code, Section = a.Section.Name })
            .ToListAsync(ct);

        var people = new Dictionary<int, (string Name, List<string> Courses)>();
        foreach (var course in courses)
        {
            var students = await Roster.Enrollments(db, course.SectionId, course.SubjectId).AsNoTracking()
                .Where(e => e.Student.User.IsActive)
                .Select(e => new { e.Student.UserId, e.Student.FirstName, e.Student.MiddleName, e.Student.LastName, e.Student.NameExtension })
                .ToListAsync(ct);
            foreach (var s in students)
            {
                if (!people.TryGetValue(s.UserId, out var entry))
                {
                    entry = (UserDirectory.FullName(s.FirstName, s.MiddleName, s.LastName, s.NameExtension), []);
                    people[s.UserId] = entry;
                }

                entry.Courses.Add($"{course.Subject} · {TeachingService.SectionLabel(course.Grade, course.Strand, course.Section)}");
            }
        }

        var cards = people
            .Select(kv => new PersonCard(kv.Key, kv.Value.Name, AppRoles.Student, string.Join("; ", kv.Value.Courses.Distinct())))
            .OrderBy(c => c.Name)
            .ToList();

        return await WithStatesAsync(db, userId, cards, ct);
    }

    /// <summary>The students of one course of the teacher (the course menu: Students). Empty when the course is not theirs.</summary>
    public async Task<IReadOnlyList<PersonCard>> GetCourseStudentsAsync(int teacherUserId, int assignmentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var course = await db.TeachingAssignments.AsNoTracking().Where(a => a.Id == assignmentId && a.Teacher.UserId == teacherUserId)
            .Select(a => new { a.SectionId, a.SubjectId, Grade = a.Section.GradeLevel, Strand = a.Section.Strand.Code, Section = a.Section.Name })
            .FirstOrDefaultAsync(ct);
        if (course is null)
        {
            return [];
        }

        var label = TeachingService.SectionLabel(course.Grade, course.Strand, course.Section);
        var rows = await Roster.Enrollments(db, course.SectionId, course.SubjectId).AsNoTracking()
            .Where(e => e.Student.User.IsActive)
            .Select(e => new { e.Student.UserId, e.Student.FirstName, e.Student.MiddleName, e.Student.LastName, e.Student.NameExtension })
            .ToListAsync(ct);

        var cards = rows
            .Select(r => new PersonCard(r.UserId, UserDirectory.FullName(r.FirstName, r.MiddleName, r.LastName, r.NameExtension), AppRoles.Student, label))
            .OrderBy(c => c.Name)
            .ToList();

        return await WithStatesAsync(db, teacherUserId, cards, ct);
    }

    /// <summary>The teacher of one course of the student (the course menu: Teachers). Empty when the student does not take the course.</summary>
    public async Task<IReadOnlyList<PersonCard>> GetCourseTeachersAsync(int studentUserId, int assignmentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var studentId = await db.Students.AsNoTracking().Where(s => s.UserId == studentUserId).Select(s => (int?)s.Id).FirstOrDefaultAsync(ct);
        if (studentId is null)
        {
            return [];
        }

        var rows = await Roster.AssignmentsOf(db, studentId.Value).AsNoTracking()
            .Where(a => a.Id == assignmentId && a.Teacher.User.IsActive)
            .Select(a => new { a.Teacher.UserId, a.Teacher.FirstName, a.Teacher.MiddleName, a.Teacher.LastName, a.Teacher.NameExtension, Subject = a.Subject.Name })
            .ToListAsync(ct);

        var cards = rows
            .Select(r => new PersonCard(r.UserId, UserDirectory.FullName(r.FirstName, r.MiddleName, r.LastName, r.NameExtension), AppRoles.Teacher, r.Subject))
            .OrderBy(c => c.Name)
            .ToList();

        return await WithStatesAsync(db, studentUserId, cards, ct);
    }

    /// <summary>The other teachers of the school, with the courses each of them teaches.</summary>
    public async Task<IReadOnlyList<PersonCard>> GetColleaguesAsync(int userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var teachers = await db.Teachers.AsNoTracking()
            .Where(t => t.UserId != userId && t.User.IsActive)
            .Select(t => new { t.Id, t.UserId, t.FirstName, t.MiddleName, t.LastName, t.NameExtension })
            .ToListAsync(ct);

        var subjects = (await db.TeachingAssignments.AsNoTracking().Select(a => new { a.TeacherId, Subject = a.Subject.Name }).ToListAsync(ct))
            .GroupBy(a => a.TeacherId)
            .ToDictionary(g => g.Key, g => g.Select(a => a.Subject).Distinct().OrderBy(s => s).ToList());

        var cards = teachers
            .Select(t =>
            {
                var list = subjects.TryGetValue(t.Id, out var names) ? names : [];
                var detail = list.Count == 0 ? "Teacher" : list.Count <= 2 ? string.Join(", ", list) : $"{string.Join(", ", list.Take(2))} +{list.Count - 2}";
                return new PersonCard(t.UserId, UserDirectory.FullName(t.FirstName, t.MiddleName, t.LastName, t.NameExtension), AppRoles.Teacher, detail);
            })
            .OrderBy(c => c.Name)
            .ToList();

        return await WithStatesAsync(db, userId, cards, ct);
    }

    /// <summary>The people who are friends with the person (accepted).</summary>
    public async Task<IReadOnlyList<PersonCard>> GetFriendsAsync(int userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var ids = await FriendIdsAsync(db, userId, ct);
        var cards = await CardsAsync(db, ids, ct);
        return cards.Select(c => c with { State = FriendState.Friends }).OrderBy(c => c.Name).ToList();
    }

    /// <summary>The requests waiting for the person's answer, and the ones they sent.</summary>
    public async Task<FriendRequests> GetRequestsAsync(int userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await db.Friendships.AsNoTracking()
            .Where(f => f.Status == FriendshipStates.Pending && (f.RequesterUserId == userId || f.AddresseeUserId == userId))
            .Select(f => new { f.RequesterUserId, f.AddresseeUserId, f.CreatedAtUtc })
            .OrderByDescending(f => f.CreatedAtUtc)
            .ToListAsync(ct);

        var received = rows.Where(r => r.AddresseeUserId == userId).Select(r => r.RequesterUserId).ToList();
        var sent = rows.Where(r => r.RequesterUserId == userId).Select(r => r.AddresseeUserId).ToList();
        var cards = (await CardsAsync(db, received.Concat(sent).ToList(), ct)).ToDictionary(c => c.UserId);

        return new FriendRequests(
            received.Where(cards.ContainsKey).Select(id => cards[id] with { State = FriendState.RequestReceived }).ToList(),
            sent.Where(cards.ContainsKey).Select(id => cards[id] with { State = FriendState.RequestSent }).ToList());
    }

    /// <summary>
    /// Looks for students and teachers by name, anywhere in the school (to find a friend). At least two letters; the person themselves is left out.
    /// </summary>
    public async Task<IReadOnlyList<PersonCard>> FindAsync(int userId, string? search, int take = 20, CancellationToken ct = default)
    {
        var tokens = (search ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(4).ToList();
        if (tokens.Count == 0 || tokens.Sum(t => t.Length) < 2)
        {
            return [];
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var students = db.Students.AsNoTracking().Where(s => s.UserId != userId && s.User.IsActive);
        var teachers = db.Teachers.AsNoTracking().Where(t => t.UserId != userId && t.User.IsActive);
        foreach (var token in tokens)
        {
            var t = token;
            students = students.Where(s => s.FirstName.Contains(t) || s.LastName.Contains(t) || (s.MiddleName != null && s.MiddleName.Contains(t)));
            teachers = teachers.Where(x => x.FirstName.Contains(t) || x.LastName.Contains(t) || (x.MiddleName != null && x.MiddleName.Contains(t)));
        }

        var found = (await students.OrderBy(s => s.LastName).ThenBy(s => s.FirstName).Take(take)
                .Select(s => new { s.UserId, s.FirstName, s.MiddleName, s.LastName, s.NameExtension }).ToListAsync(ct))
            .Select(s => new PersonCard(s.UserId, UserDirectory.FullName(s.FirstName, s.MiddleName, s.LastName, s.NameExtension), AppRoles.Student, "Student"))
            .Concat((await teachers.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).Take(take)
                .Select(x => new { x.UserId, x.FirstName, x.MiddleName, x.LastName, x.NameExtension }).ToListAsync(ct))
                .Select(x => new PersonCard(x.UserId, UserDirectory.FullName(x.FirstName, x.MiddleName, x.LastName, x.NameExtension), AppRoles.Teacher, "Teacher")))
            .OrderBy(c => c.Name)
            .Take(take)
            .ToList();

        return await WithStatesAsync(db, userId, found, ct);
    }

    // ----------------------------------------------------------------- who can be added to a group or given a file

    /// <summary>
    /// The people the person can add to a group or share a file with: for a student the classmates, their teachers and their friends; for a
    /// teacher their students, the other teachers and their friends.
    /// </summary>
    public async Task<IReadOnlyList<PersonCard>> GetReachableAsync(int userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var role = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.RoleId).FirstOrDefaultAsync(ct);

        var lists = new List<IReadOnlyList<PersonCard>> { await GetFriendsAsync(userId, ct) };
        if (role == AppRoles.StudentId)
        {
            lists.Add(await GetClassmatesAsync(userId, ct));
            lists.Add(await GetMyTeachersAsync(userId, ct));
        }
        else if (role == AppRoles.TeacherId)
        {
            lists.Add(await GetMyStudentsAsync(userId, ct));
            lists.Add(await GetColleaguesAsync(userId, ct));
        }

        return lists.SelectMany(l => l).GroupBy(c => c.UserId).Select(g => g.First()).OrderBy(c => c.Name).ToList();
    }

    /// <summary>The students (classmates, students of the teacher, friends who are students) that are online, for the dashboard.</summary>
    public async Task<IReadOnlyList<PersonCard>> GetOnlineStudentsAsync(int userId, PresenceService presence, CancellationToken ct = default)
    {
        var reachable = await GetReachableAsync(userId, ct);
        var students = reachable.Where(c => c.IsStudent).ToList();
        var online = presence.OnlineAmong(students.Select(s => s.UserId));
        return students.Where(s => online.Contains(s.UserId)).ToList();
    }

    // ----------------------------------------------------------------- helpers

    private static async Task<List<int>> FriendIdsAsync(AppDbContext db, int userId, CancellationToken ct) =>
        await db.Friendships.AsNoTracking()
            .Where(f => f.Status == FriendshipStates.Accepted && (f.RequesterUserId == userId || f.AddresseeUserId == userId))
            .Select(f => f.RequesterUserId == userId ? f.AddresseeUserId : f.RequesterUserId)
            .ToListAsync(ct);

    /// <summary>Cards (name, role, a short detail) for accounts, in no special order. Only students and teachers who are active.</summary>
    private static async Task<List<PersonCard>> CardsAsync(AppDbContext db, IReadOnlyCollection<int> userIds, CancellationToken ct)
    {
        var active = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id) && u.IsActive && (u.RoleId == AppRoles.StudentId || u.RoleId == AppRoles.TeacherId))
            .Select(u => u.Id).ToListAsync(ct);
        var people = await UserDirectory.LoadAsync(db, active, ct);

        // A student's section is the one of their enrollment now.
        var sections = await db.Enrollments.AsNoTracking()
            .Where(e => e.Status == EnrollmentStatus.Enrolled && active.Contains(e.Student.UserId))
            .Select(e => new { e.Student.UserId, e.GradeLevel, Strand = e.Strand.Code, Section = e.Section.Name })
            .ToListAsync(ct);
        var sectionOf = sections.GroupBy(s => s.UserId)
            .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(s => TeachingService.SectionLabel(s.GradeLevel, s.Strand, s.Section)).Distinct()));

        return people.Values
            .Select(p => new PersonCard(p.UserId, p.Name, p.Role, p.Role == AppRoles.Student ? sectionOf.GetValueOrDefault(p.UserId, "Student") : "Teacher"))
            .ToList();
    }

    /// <summary>Puts on each card whether the person is a friend, was asked, or asked.</summary>
    internal static async Task<IReadOnlyList<PersonCard>> WithStatesAsync(AppDbContext db, int userId, IReadOnlyList<PersonCard> cards, CancellationToken ct)
    {
        if (cards.Count == 0)
        {
            return cards;
        }

        var ids = cards.Select(c => c.UserId).ToList();
        var relations = await db.Friendships.AsNoTracking()
            .Where(f => (f.RequesterUserId == userId && ids.Contains(f.AddresseeUserId)) || (f.AddresseeUserId == userId && ids.Contains(f.RequesterUserId)))
            .Select(f => new { f.RequesterUserId, f.AddresseeUserId, f.Status })
            .ToListAsync(ct);

        var states = new Dictionary<int, FriendState>();
        foreach (var r in relations)
        {
            var other = r.RequesterUserId == userId ? r.AddresseeUserId : r.RequesterUserId;
            states[other] = r.Status == FriendshipStates.Accepted ? FriendState.Friends
                : r.RequesterUserId == userId ? FriendState.RequestSent : FriendState.RequestReceived;
        }

        return cards.Select(c => states.TryGetValue(c.UserId, out var state) ? c with { State = state } : c).ToList();
    }
}
