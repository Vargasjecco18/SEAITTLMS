using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

public sealed record GroupSummary(int Id, string Name, string? Description, int Members, bool IsOwner, string OwnerName, int Posts, DateTime LastActivityUtc, bool IsAutomatic = false);

public sealed record GroupMemberView(int UserId, string Name, string Role, bool IsOwner, DateTime JoinedAtUtc);

public sealed record GroupPostView(int Id, int AuthorUserId, string AuthorName, string AuthorRole, string Body, DateTime AtUtc, bool CanDelete, IReadOnlyList<GroupPostView> Replies);

public sealed record GroupDetail(
    int Id, string Name, string? Description, int? OwnerUserId, string OwnerName, bool IsOwner, DateTime CreatedAtUtc,
    IReadOnlyList<GroupMemberView> Members, IReadOnlyList<GroupPostView> Posts, int TotalPosts, bool IsAutomatic = false);

/// <summary>
/// Groups of students and teachers. The system makes some by itself and keeps who is in them (see <see cref="SyncAutomaticAsync"/>): for a
/// student "Grade 11 students" (their grade level), the students of their course (strand) and "All students"; for a teacher "Grade 11
/// teachers" (each grade level they teach in) and "All teachers". Nobody owns those and nobody leaves them.
/// People can also make their own group (a study group, a project team, a club): whoever makes it owns it and can add and remove members,
/// change its name and delete it; every member can write on its wall, share files with it, and leave. A group is only seen by its members.
/// People can only be added when they are people the owner can see in the Users page (classmates, teachers, students, friends).
/// </summary>
public sealed class GroupService(IDbContextFactory<AppDbContext> dbFactory, PeopleService people, NotificationService notifications, TimeProvider clock)
{
    public const int MaxMembers = 100;
    public const int MaxGroupsOwned = 20;
    public const int MaxPostLength = 2000;

    /// <summary>The automatic groups are looked at again at most this often by one circuit (one visitor).</summary>
    private static readonly TimeSpan SyncEvery = TimeSpan.FromSeconds(20);

    private static readonly SemaphoreSlim SyncGate = new(1, 1);
    private DateTime lastSyncUtc = DateTime.MinValue;

    // ----------------------------------------------------------------- read

    /// <summary>The groups of the person: the automatic ones first (by kind), then the groups people made (the ones with the latest news first).</summary>
    public async Task<IReadOnlyList<GroupSummary>> ListMineAsync(int userId, CancellationToken ct = default)
    {
        if (clock.GetUtcNow().UtcDateTime - lastSyncUtc >= SyncEvery)
        {
            await SyncAutomaticAsync(ct: ct);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var groups = await db.StudyGroupMembers.AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => new
            {
                m.Group.Id, m.Group.Name, m.Group.Description, m.Group.OwnerUserId, m.Group.CreatedAtUtc, m.Group.AutoKind, m.Group.AutoKey,
                Members = m.Group.Members.Count,
                Posts = m.Group.Posts.Count,
                LastPost = m.Group.Posts.Max(p => (DateTime?)p.CreatedAtUtc),
            })
            .ToListAsync(ct);

        var owners = await UserDirectory.LoadAsync(db, groups.Where(g => g.OwnerUserId is not null).Select(g => g.OwnerUserId!.Value).ToList(), ct);
        var summaries = groups
            .Select(g => (Group: g, Summary: new GroupSummary(g.Id, g.Name, g.Description, g.Members, g.OwnerUserId == userId,
                g.OwnerUserId is { } owner && owners.TryGetValue(owner, out var o) ? o.Name : "", g.Posts, g.LastPost ?? g.CreatedAtUtc, g.AutoKind is not null)))
            .ToList();

        return summaries.Where(x => x.Group.AutoKind is not null)
            .OrderBy(x => Array.IndexOf(AutoGroupKinds.All, x.Group.AutoKind)).ThenBy(x => x.Group.AutoKey)
            .Concat(summaries.Where(x => x.Group.AutoKind is null).OrderByDescending(x => x.Summary.LastActivityUtc))
            .Select(x => x.Summary)
            .ToList();
    }

    /// <summary>A group with its members and the latest posts. Null when it does not exist or the person is not a member.</summary>
    public async Task<GroupDetail?> GetAsync(int userId, int groupId, int takePosts = 30, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var group = await db.StudyGroups.AsNoTracking()
            .Where(g => g.Id == groupId && g.Members.Any(m => m.UserId == userId))
            .Select(g => new { g.Id, g.Name, g.Description, g.OwnerUserId, g.CreatedAtUtc, g.AutoKind })
            .FirstOrDefaultAsync(ct);
        if (group is null)
        {
            return null;
        }

        var members = await db.StudyGroupMembers.AsNoTracking().Where(m => m.GroupId == groupId)
            .Select(m => new { m.UserId, m.JoinedAtUtc }).ToListAsync(ct);

        var topPosts = await db.GroupPosts.AsNoTracking()
            .Where(p => p.GroupId == groupId && p.ParentPostId == null)
            .OrderByDescending(p => p.CreatedAtUtc).ThenByDescending(p => p.Id)
            .Take(Math.Clamp(takePosts, 1, 200))
            .Select(p => new { p.Id, p.AuthorUserId, p.Body, p.CreatedAtUtc })
            .ToListAsync(ct);
        var topIds = topPosts.Select(p => p.Id).ToList();
        var replies = await db.GroupPosts.AsNoTracking()
            .Where(p => p.ParentPostId != null && topIds.Contains(p.ParentPostId.Value))
            .OrderBy(p => p.CreatedAtUtc).ThenBy(p => p.Id)
            .Select(p => new { p.Id, p.ParentPostId, p.AuthorUserId, p.Body, p.CreatedAtUtc })
            .ToListAsync(ct);
        var totalPosts = await db.GroupPosts.CountAsync(p => p.GroupId == groupId && p.ParentPostId == null, ct);

        var names = await UserDirectory.LoadAsync(db, members.Select(m => m.UserId).Concat(topPosts.Select(p => p.AuthorUserId)).Concat(replies.Select(r => r.AuthorUserId))
            .Concat(group.OwnerUserId is { } ownerId ? [ownerId] : []).ToList(), ct);
        PersonRef Who(int id) => names.TryGetValue(id, out var p) ? p : new PersonRef(id, "Former member", AppRoles.Student);

        var isOwner = group.OwnerUserId == userId;
        GroupPostView Post(int id, int authorId, string body, DateTime at, IReadOnlyList<GroupPostView> children) =>
            new(id, authorId, Who(authorId).Name, Who(authorId).Role, body, at, authorId == userId || isOwner, children);

        var posts = topPosts
            .Select(p => Post(p.Id, p.AuthorUserId, p.Body, p.CreatedAtUtc,
                replies.Where(r => r.ParentPostId == p.Id).Select(r => Post(r.Id, r.AuthorUserId, r.Body, r.CreatedAtUtc, [])).ToList()))
            .ToList();

        return new GroupDetail(
            group.Id, group.Name, group.Description, group.OwnerUserId, group.OwnerUserId is { } owner ? Who(owner).Name : "", isOwner, group.CreatedAtUtc,
            members.Select(m => new GroupMemberView(m.UserId, Who(m.UserId).Name, Who(m.UserId).Role, m.UserId == group.OwnerUserId, m.JoinedAtUtc))
                .OrderByDescending(m => m.IsOwner).ThenBy(m => m.Name).ToList(),
            posts, totalPosts, group.AutoKind is not null);
    }

    public async Task<bool> IsMemberAsync(int userId, int groupId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.StudyGroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserId == userId, ct);
    }

    // ----------------------------------------------------------------- the group itself

    public async Task<ServiceResult> CreateAsync(int userId, string? name, string? description, IReadOnlyCollection<int> memberUserIds, CancellationToken ct = default)
    {
        var errors = Validate(name, description);
        if (errors.Count > 0)
        {
            return ServiceResult.Fail(errors);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsStudentOrTeacherAsync(db, userId, ct))
        {
            return ServiceResult.Fail("Only students and teachers have groups.");
        }

        if (await db.StudyGroups.CountAsync(g => g.OwnerUserId == userId, ct) >= MaxGroupsOwned)
        {
            return ServiceResult.Fail($"You already have {MaxGroupsOwned} groups. Delete one you don't need first.");
        }

        var reachable = (await people.GetReachableAsync(userId, ct)).Select(p => p.UserId).ToHashSet();
        var invited = memberUserIds.Distinct().Where(id => id != userId && reachable.Contains(id)).Take(MaxMembers - 1).ToList();

        var group = new StudyGroup { Name = Clean(name!), Description = string.IsNullOrWhiteSpace(description) ? null : Clean(description), OwnerUserId = userId };
        var now = clock.GetUtcNow().UtcDateTime;
        group.Members.Add(new StudyGroupMember { UserId = userId, JoinedAtUtc = now, AddedByUserId = userId });
        foreach (var id in invited)
        {
            group.Members.Add(new StudyGroupMember { UserId = id, JoinedAtUtc = now, AddedByUserId = userId });
        }

        db.StudyGroups.Add(group);
        await db.SaveChangesAsync(ct);

        await TellAddedAsync(db, userId, group.Id, group.Name, invited, ct);
        return ServiceResult.Ok($"The group {group.Name} was made.", group.Id);
    }

    public async Task<ServiceResult> UpdateAsync(int userId, int groupId, string? name, string? description, CancellationToken ct = default)
    {
        var errors = Validate(name, description);
        if (errors.Count > 0)
        {
            return ServiceResult.Fail(errors);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var group = await db.StudyGroups.FirstOrDefaultAsync(g => g.Id == groupId && g.OwnerUserId == userId, ct);
        if (group is null)
        {
            return ServiceResult.Fail("Only the person who made the group can change it.");
        }

        group.Name = Clean(name!);
        group.Description = string.IsNullOrWhiteSpace(description) ? null : Clean(description);
        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok("The group was saved.");
    }

    public async Task<ServiceResult> DeleteAsync(int userId, int groupId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var deleted = await db.StudyGroups.Where(g => g.Id == groupId && g.OwnerUserId == userId).ExecuteDeleteAsync(ct);
        return deleted > 0 ? ServiceResult.Ok("The group was deleted.") : ServiceResult.Fail("Only the person who made the group can delete it.");
    }

    // ----------------------------------------------------------------- members

    public async Task<ServiceResult> AddMembersAsync(int userId, int groupId, IReadOnlyCollection<int> userIds, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var group = await db.StudyGroups.AsNoTracking().Where(g => g.Id == groupId && g.OwnerUserId == userId)
            .Select(g => new { g.Id, g.Name, Members = g.Members.Select(m => m.UserId).ToList() }).FirstOrDefaultAsync(ct);
        if (group is null)
        {
            return ServiceResult.Fail("Only the person who made the group can add people.");
        }

        var reachable = (await people.GetReachableAsync(userId, ct)).Select(p => p.UserId).ToHashSet();
        var add = userIds.Distinct().Where(id => !group.Members.Contains(id) && reachable.Contains(id)).ToList();
        if (add.Count == 0)
        {
            return ServiceResult.Fail("Choose people who are not in the group yet.");
        }

        if (group.Members.Count + add.Count > MaxMembers)
        {
            return ServiceResult.Fail($"A group can have at most {MaxMembers} people.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var id in add)
        {
            db.StudyGroupMembers.Add(new StudyGroupMember { GroupId = groupId, UserId = id, JoinedAtUtc = now, AddedByUserId = userId });
        }

        await db.SaveChangesAsync(ct);
        await TellAddedAsync(db, userId, groupId, group.Name, add, ct);
        return ServiceResult.Ok(add.Count == 1 ? "1 person was added." : $"{add.Count} people were added.");
    }

    public async Task<ServiceResult> RemoveMemberAsync(int userId, int groupId, int memberUserId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await db.StudyGroups.AnyAsync(g => g.Id == groupId && g.OwnerUserId == userId, ct))
        {
            return ServiceResult.Fail("Only the person who made the group can remove people.");
        }

        if (memberUserId == userId)
        {
            return ServiceResult.Fail("The owner can't be removed. Delete the group instead.");
        }

        var removed = await db.StudyGroupMembers.Where(m => m.GroupId == groupId && m.UserId == memberUserId).ExecuteDeleteAsync(ct);
        if (removed == 0)
        {
            return ServiceResult.Fail("This person is not in the group.");
        }

        // What the person shared with the group is not shared any more.
        await db.SharedFiles.Where(s => s.GroupId == groupId && s.File.OwnerUserId == memberUserId).ExecuteDeleteAsync(ct);
        return ServiceResult.Ok("The person was removed.");
    }

    /// <summary>A member leaves. When the owner leaves, the member who has been there longest becomes the owner; when nobody is left, the group is deleted.</summary>
    public async Task<ServiceResult> LeaveAsync(int userId, int groupId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var group = await db.StudyGroups.FirstOrDefaultAsync(g => g.Id == groupId && g.Members.Any(m => m.UserId == userId), ct);
        if (group is null)
        {
            return ServiceResult.Fail("You are not in this group.");
        }

        if (group.AutoKind is not null)
        {
            return ServiceResult.Fail("You are in this group automatically. You can't leave it.");
        }

        await db.StudyGroupMembers.Where(m => m.GroupId == groupId && m.UserId == userId).ExecuteDeleteAsync(ct);
        await db.SharedFiles.Where(s => s.GroupId == groupId && s.File.OwnerUserId == userId).ExecuteDeleteAsync(ct);

        if (group.OwnerUserId == userId)
        {
            var next = await db.StudyGroupMembers.Where(m => m.GroupId == groupId).OrderBy(m => m.JoinedAtUtc).ThenBy(m => m.UserId)
                .Select(m => (int?)m.UserId).FirstOrDefaultAsync(ct);
            if (next is null)
            {
                await db.StudyGroups.Where(g => g.Id == groupId).ExecuteDeleteAsync(ct);
                return ServiceResult.Ok("You left. Nobody was left, so the group was deleted.");
            }

            group.OwnerUserId = next.Value;
            await db.SaveChangesAsync(ct);
        }

        return ServiceResult.Ok("You left the group.");
    }

    // ----------------------------------------------------------------- the wall

    public async Task<ServiceResult> PostAsync(int userId, int groupId, string? body, int? replyToPostId = null, CancellationToken ct = default)
    {
        var text = (body ?? "").Trim();
        if (text.Length == 0)
        {
            return ServiceResult.Fail("Write something first.");
        }

        if (text.Length > MaxPostLength)
        {
            return ServiceResult.Fail($"A post can have at most {MaxPostLength:N0} characters.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var group = await db.StudyGroups.AsNoTracking().Where(g => g.Id == groupId && g.Members.Any(m => m.UserId == userId))
            .Select(g => new { g.Name, g.AutoKind, Members = g.Members.Select(m => m.UserId).ToList() }).FirstOrDefaultAsync(ct);
        if (group is null)
        {
            return ServiceResult.Fail("You are not in this group.");
        }

        if (replyToPostId is { } parentId && !await db.GroupPosts.AnyAsync(p => p.Id == parentId && p.GroupId == groupId && p.ParentPostId == null, ct))
        {
            return ServiceResult.Fail("The post you answer is no longer there.");
        }

        var post = new GroupPost { GroupId = groupId, AuthorUserId = userId, Body = text, ParentPostId = replyToPostId };
        db.GroupPosts.Add(post);
        await db.SaveChangesAsync(ct);

        // The others are told about a new post (not about every answer, that would be too many, and not in an automatic group: hundreds of people are in those).
        if (replyToPostId is null && group.AutoKind is null)
        {
            var author = (await UserDirectory.LoadAsync(db, [userId], ct)).GetValueOrDefault(userId)?.Name ?? "Somebody";
            await notifications.AddForManyAsync(group.Members.Where(id => id != userId), "group", $"{author} posted in {group.Name}",
                text.Length <= 140 ? text : text[..140] + "…", $"groups/{groupId}", ct);
        }

        return ServiceResult.Ok(replyToPostId is null ? "Posted." : "Replied.", post.Id);
    }

    public async Task<ServiceResult> DeletePostAsync(int userId, int groupId, int postId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var isOwner = await db.StudyGroups.AnyAsync(g => g.Id == groupId && g.OwnerUserId == userId, ct);
        var deleted = await db.GroupPosts.Where(p => p.Id == postId && p.GroupId == groupId && (p.AuthorUserId == userId || isOwner)).ExecuteDeleteAsync(ct);
        return deleted > 0 ? ServiceResult.Ok("The post was deleted.") : ServiceResult.Fail("You can only delete your own posts.");
    }

    // ----------------------------------------------------------------- the automatic groups

    /// <summary>
    /// Makes the automatic groups that are missing and puts the right people in them (and takes out those who no longer belong), from the
    /// enrollments (students) and the courses of the teachers (the grade levels they teach in, this school year). The posts and files of a
    /// group stay. It looks again at most every 20 seconds for one visitor, unless <paramref name="force"/> is set.
    /// </summary>
    public async Task SyncAutomaticAsync(bool force = false, CancellationToken ct = default)
    {
        if (!force && clock.GetUtcNow().UtcDateTime - lastSyncUtc < SyncEvery)
        {
            return;
        }

        await SyncGate.WaitAsync(ct);
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            // ---- who belongs where
            var enrolled = await db.Enrollments.AsNoTracking()
                .Where(e => e.Status == EnrollmentStatus.Enrolled && e.Student.Status == StudentStatus.Active && e.Student.User.IsActive)
                .Select(e => new { e.Student.UserId, e.GradeLevel, e.StrandId, Strand = e.Strand.Code })
                .ToListAsync(ct);

            var yearId = await db.Terms.AsNoTracking().Where(t => t.IsCurrent).Select(t => (int?)t.SchoolYearId).FirstOrDefaultAsync(ct)
                ?? await db.SchoolYears.AsNoTracking().OrderByDescending(y => y.StartDate).Select(y => (int?)y.Id).FirstOrDefaultAsync(ct);
            var taught = await db.TeachingAssignments.AsNoTracking()
                .Where(a => a.Teacher.User.IsActive && (yearId == null || a.Section.SchoolYearId == yearId))
                .Select(a => new { a.Teacher.UserId, Grade = (int)a.Section.GradeLevel })
                .Distinct().ToListAsync(ct);
            var teacherUsers = await db.Teachers.AsNoTracking().Where(t => t.User.IsActive).Select(t => t.UserId).ToListAsync(ct);

            // kind, key -> name, description, members
            var wanted = new Dictionary<(string Kind, int Key), (string Name, string Description, HashSet<int> Members)>();
            void Want(string kind, int key, string name, string description, IEnumerable<int> members)
            {
                var set = members.ToHashSet();
                if (set.Count > 0)
                {
                    wanted[(kind, key)] = (name, description, set);
                }
            }

            var studentUsers = enrolled.Select(e => e.UserId).Distinct().ToList();
            Want(AutoGroupKinds.AllStudents, 0, "All students", "Every student of the school.", studentUsers);
            foreach (var grade in enrolled.Select(e => (int)e.GradeLevel).Distinct())
            {
                Want(AutoGroupKinds.GradeStudents, grade, $"Grade {grade} students", $"The students of Grade {grade}.", enrolled.Where(e => e.GradeLevel == grade).Select(e => e.UserId));
            }

            foreach (var strand in enrolled.GroupBy(e => e.StrandId))
            {
                var code = strand.First().Strand;
                Want(AutoGroupKinds.CourseStudents, strand.Key, $"{code} students", $"The students of the same course ({code}), Grade 11 and Grade 12.", strand.Select(e => e.UserId));
            }

            Want(AutoGroupKinds.AllTeachers, 0, "All teachers", "Every teacher of the school.", teacherUsers);
            foreach (var grade in taught.Select(t => t.Grade).Distinct())
            {
                Want(AutoGroupKinds.GradeTeachers, grade, $"Grade {grade} teachers", $"The teachers who teach Grade {grade}.", taught.Where(t => t.Grade == grade).Select(t => t.UserId));
            }

            // ---- what is there now
            var autoGroups = await db.StudyGroups.Where(g => g.AutoKind != null).Include(g => g.Members).ToListAsync(ct);
            var now = clock.GetUtcNow().UtcDateTime;
            foreach (var ((kind, key), (name, description, members)) in wanted)
            {
                var group = autoGroups.FirstOrDefault(g => g.AutoKind == kind && g.AutoKey == key);
                if (group is null)
                {
                    group = new StudyGroup { AutoKind = kind, AutoKey = key, Name = name, Description = description };
                    db.StudyGroups.Add(group);
                    autoGroups.Add(group);
                }
                else
                {
                    group.Name = name;
                    group.Description = description;
                }

                foreach (var gone in group.Members.Where(m => !members.Contains(m.UserId)).ToList())
                {
                    group.Members.Remove(gone);
                    db.StudyGroupMembers.Remove(gone);
                }

                var already = group.Members.Select(m => m.UserId).ToHashSet();
                foreach (var userId in members.Where(id => !already.Contains(id)))
                {
                    group.Members.Add(new StudyGroupMember { UserId = userId, JoinedAtUtc = now });
                }
            }

            // a group nobody belongs to any more (no student of that grade or course): its members go, the group and its wall stay
            foreach (var group in autoGroups.Where(g => g.Id != 0 && !wanted.ContainsKey((g.AutoKind!, g.AutoKey ?? 0))))
            {
                db.StudyGroupMembers.RemoveRange(group.Members);
            }

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // another visitor made the same group a moment ago; the next look finds it
            }

            lastSyncUtc = now;
        }
        finally
        {
            SyncGate.Release();
        }
    }

    // ----------------------------------------------------------------- helpers

    private static List<string> Validate(string? name, string? description)
    {
        var errors = new List<string>();
        var clean = Clean(name ?? "");
        if (clean.Length is < 2 or > 100)
        {
            errors.Add("Give the group a name of 2 to 100 characters.");
        }

        if ((description ?? "").Trim().Length > 500)
        {
            errors.Add("The description can have at most 500 characters.");
        }

        return errors;
    }

    private static string Clean(string text) => string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static async Task<bool> IsStudentOrTeacherAsync(AppDbContext db, int userId, CancellationToken ct) =>
        await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.IsActive && (u.RoleId == AppRoles.StudentId || u.RoleId == AppRoles.TeacherId), ct);

    private async Task TellAddedAsync(AppDbContext db, int ownerUserId, int groupId, string groupName, IReadOnlyCollection<int> added, CancellationToken ct)
    {
        if (added.Count == 0)
        {
            return;
        }

        var owner = (await UserDirectory.LoadAsync(db, [ownerUserId], ct)).GetValueOrDefault(ownerUserId)?.Name ?? "Somebody";
        await notifications.AddForManyAsync(added, "group", $"{owner} added you to the group {groupName}", null, $"groups/{groupId}", ct);
    }
}
