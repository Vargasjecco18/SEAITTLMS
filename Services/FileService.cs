using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>A file in somebody's locker. <see cref="ShareText"/> says who it is shared with ("2 people, 1 group"), empty when it is private.</summary>
public sealed record FileRow(int Id, string Name, string ContentType, long SizeBytes, DateTime AtUtc, int ShareCount, string? ShareText);

/// <summary>A file somebody else shared with the person. <see cref="Via"/> is "person", "group" or "course"; <see cref="Where"/> names the group or the course.</summary>
public sealed record SharedFileRow(int Id, string Name, string ContentType, long SizeBytes, DateTime SharedAtUtc, int OwnerUserId, string OwnerName, string Via, string? Where);

public sealed record LockerUsage(long UsedBytes, long LimitBytes, int Files)
{
    public int Percent => LimitBytes <= 0 ? 0 : (int)Math.Min(100, UsedBytes * 100 / LimitBytes);
}

/// <summary>Who a file is shared with (account ids, group ids, teaching assignment ids).</summary>
public sealed record ShareTargets(IReadOnlyList<int> UserIds, IReadOnlyList<int> GroupIds, IReadOnlyList<int> AssignmentIds)
{
    public static readonly ShareTargets None = new([], [], []);
}

public sealed record CourseChoice(int AssignmentId, string Label);

public sealed record GroupChoice(int Id, string Name);

public sealed record ShareOptions(IReadOnlyList<PersonCard> People, IReadOnlyList<GroupChoice> Groups, IReadOnlyList<CourseChoice> Courses);

public sealed record FileDownload(string Name, string ContentType, byte[] Data);

/// <summary>
/// The locker: the files of every student and teacher. A file is private to its owner until the owner shares it with people they can see in the
/// Users page, with a group they are in, or (a teacher) with the students of one of their courses. The people it is shared with can open it
/// and keep a copy in their own locker; they can't change it or share it on. The bytes are in the database (so a backup has them),
/// the size of a file and of a locker are the ones of System Settings.
/// </summary>
public sealed class FileService(
    IDbContextFactory<AppDbContext> dbFactory, PeopleService people, NotificationService notifications, SystemSettingsService settings)
{
    /// <summary>Programs and scripts are not kept (they are the usual way a computer is harmed).</summary>
    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".msi", ".bat", ".cmd", ".com", ".scr", ".vbs", ".vbe", ".ps1", ".jar", ".apk", ".dll", ".sh", ".lnk", ".reg", ".hta",
    };

    public static bool IsBlocked(string fileName) => BlockedExtensions.Contains(Path.GetExtension(fileName));

    // ----------------------------------------------------------------- the locker

    public async Task<IReadOnlyList<FileRow>> GetLockerAsync(int userId, string? search = null, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var query = db.StoredFiles.AsNoTracking().Where(f => f.OwnerUserId == userId);
        foreach (var token in (search ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(4))
        {
            var t = token;
            query = query.Where(f => f.Name.Contains(t));
        }

        var rows = await query.OrderByDescending(f => f.CreatedAtUtc).ThenByDescending(f => f.Id)
            .Select(f => new
            {
                f.Id, f.Name, f.ContentType, f.SizeBytes, f.CreatedAtUtc,
                People = f.Shares.Count(s => s.SharedWithUserId != null),
                Groups = f.Shares.Count(s => s.GroupId != null),
                Courses = f.Shares.Count(s => s.AssignmentId != null),
            })
            .ToListAsync(ct);

        return rows.Select(r => new FileRow(r.Id, r.Name, r.ContentType, r.SizeBytes, r.CreatedAtUtc, r.People + r.Groups + r.Courses,
            ShareText(r.People, r.Groups, r.Courses))).ToList();
    }

    private static string? ShareText(int peopleCount, int groups, int courses)
    {
        var parts = new List<string>();
        if (peopleCount > 0) parts.Add(peopleCount == 1 ? "1 person" : $"{peopleCount} people");
        if (groups > 0) parts.Add(groups == 1 ? "1 group" : $"{groups} groups");
        if (courses > 0) parts.Add(courses == 1 ? "1 course" : $"{courses} courses");
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    public async Task<LockerUsage> GetUsageAsync(int userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var used = await db.StoredFiles.Where(f => f.OwnerUserId == userId).SumAsync(f => (long?)f.SizeBytes, ct) ?? 0;
        var count = await db.StoredFiles.CountAsync(f => f.OwnerUserId == userId, ct);
        return new LockerUsage(used, settings.Current.LockerMegabytes * 1024L * 1024, count);
    }

    /// <summary>Puts a file in the locker. The stream is read up to the biggest size allowed (not more), so a huge file can't fill the memory.</summary>
    public async Task<ServiceResult> UploadAsync(int userId, string? fileName, string? contentType, Stream content, CancellationToken ct = default)
    {
        var name = CleanName(fileName);
        if (name is null)
        {
            return ServiceResult.Fail("This file has no name.");
        }

        if (IsBlocked(name))
        {
            return ServiceResult.Fail($"{name}: programs and scripts can't be kept here.");
        }

        var maxBytes = settings.Current.MaxFileMegabytes * 1024L * 1024;

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.IsActive && (u.RoleId == AppRoles.StudentId || u.RoleId == AppRoles.TeacherId), ct))
        {
            return ServiceResult.Fail("Only students and teachers have a locker.");
        }

        // Read at most one byte more than allowed: enough to know it is too big.
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(chunk, ct)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > maxBytes)
            {
                return ServiceResult.Fail($"{name} is bigger than {settings.Current.MaxFileMegabytes} MB.");
            }
        }

        if (buffer.Length == 0)
        {
            return ServiceResult.Fail($"{name} is empty.");
        }

        var used = await db.StoredFiles.Where(f => f.OwnerUserId == userId).SumAsync(f => (long?)f.SizeBytes, ct) ?? 0;
        var limit = settings.Current.LockerMegabytes * 1024L * 1024;
        if (used + buffer.Length > limit)
        {
            return ServiceResult.Fail($"Your locker is full ({settings.Current.LockerMegabytes} MB). Delete a file to make room for {name}.");
        }

        var file = new StoredFile
        {
            OwnerUserId = userId, Name = name, ContentType = CleanType(contentType), SizeBytes = buffer.Length,
            Content = new StoredFileContent { Data = buffer.ToArray() },
        };
        db.StoredFiles.Add(file);
        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok($"{name} was added to your locker.", file.Id);
    }

    public async Task<ServiceResult> RenameAsync(int userId, int fileId, string? newName, CancellationToken ct = default)
    {
        var name = CleanName(newName);
        if (name is null)
        {
            return ServiceResult.Fail("Give the file a name.");
        }

        if (IsBlocked(name))
        {
            return ServiceResult.Fail("Programs and scripts can't be kept here.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var file = await db.StoredFiles.FirstOrDefaultAsync(f => f.Id == fileId && f.OwnerUserId == userId, ct);
        if (file is null)
        {
            return ServiceResult.Fail("This file is no longer in your locker.");
        }

        file.Name = name;
        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok("The file was renamed.");
    }

    public async Task<ServiceResult> DeleteAsync(int userId, int fileId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var deleted = await db.StoredFiles.Where(f => f.Id == fileId && f.OwnerUserId == userId).ExecuteDeleteAsync(ct);
        return deleted > 0 ? ServiceResult.Ok("The file was deleted. The people it was shared with can't open it any more.") : ServiceResult.Fail("This file is no longer in your locker.");
    }

    // ----------------------------------------------------------------- sharing

    public async Task<ShareTargets> GetSharesAsync(int userId, int fileId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await db.StoredFiles.AnyAsync(f => f.Id == fileId && f.OwnerUserId == userId, ct))
        {
            return ShareTargets.None;
        }

        var shares = await db.SharedFiles.AsNoTracking().Where(s => s.FileId == fileId).Select(s => new { s.SharedWithUserId, s.GroupId, s.AssignmentId }).ToListAsync(ct);
        return new ShareTargets(
            shares.Where(s => s.SharedWithUserId != null).Select(s => s.SharedWithUserId!.Value).ToList(),
            shares.Where(s => s.GroupId != null).Select(s => s.GroupId!.Value).ToList(),
            shares.Where(s => s.AssignmentId != null).Select(s => s.AssignmentId!.Value).ToList());
    }

    /// <summary>Shares one file with a group, or stops sharing it with the group, leaving the rest of its sharing as it is.</summary>
    public async Task<ServiceResult> ShareWithGroupAsync(int userId, int fileId, int groupId, bool share, CancellationToken ct = default)
    {
        var current = await GetSharesAsync(userId, fileId, ct);
        var groups = current.GroupIds.Where(g => g != groupId).ToList();
        if (share)
        {
            groups.Add(groupId);
        }

        return await SetSharesAsync(userId, fileId, current with { GroupIds = groups }, ct);
    }

    /// <summary>Who the person can share with: the people they can see (Users page), their groups and, for a teacher, their courses.</summary>
    public async Task<ShareOptions> GetShareOptionsAsync(int userId, CancellationToken ct = default)
    {
        var reachable = await people.GetReachableAsync(userId, ct);

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var groups = await db.StudyGroupMembers.AsNoTracking().Where(m => m.UserId == userId)
            .OrderBy(m => m.Group.Name).Select(m => new GroupChoice(m.GroupId, m.Group.Name)).ToListAsync(ct);

        var courses = (await db.TeachingAssignments.AsNoTracking().Where(a => a.Teacher.UserId == userId)
                .Select(a => new { a.Id, Subject = a.Subject.Name, a.Section.GradeLevel, Strand = a.Section.Strand.Code, Section = a.Section.Name })
                .ToListAsync(ct))
            .Select(a => new CourseChoice(a.Id, $"{a.Subject} · {TeachingService.SectionLabel(a.GradeLevel, a.Strand, a.Section)}"))
            .OrderBy(c => c.Label).ToList();

        return new ShareOptions(reachable, groups, courses);
    }

    /// <summary>Makes the sharing of a file exactly this. Only what is new is told to the people.</summary>
    public async Task<ServiceResult> SetSharesAsync(int userId, int fileId, ShareTargets targets, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var file = await db.StoredFiles.AsNoTracking().Where(f => f.Id == fileId && f.OwnerUserId == userId).Select(f => new { f.Id, f.Name }).FirstOrDefaultAsync(ct);
        if (file is null)
        {
            return ServiceResult.Fail("This file is no longer in your locker.");
        }

        var options = await GetShareOptionsAsync(userId, ct);
        var okUsers = options.People.Select(p => p.UserId).ToHashSet();
        var okGroups = options.Groups.Select(g => g.Id).ToHashSet();
        var okCourses = options.Courses.Select(c => c.AssignmentId).ToHashSet();

        var wantUsers = targets.UserIds.Distinct().Where(okUsers.Contains).ToHashSet();
        var wantGroups = targets.GroupIds.Distinct().Where(okGroups.Contains).ToHashSet();
        var wantCourses = targets.AssignmentIds.Distinct().Where(okCourses.Contains).ToHashSet();

        var current = await db.SharedFiles.Where(s => s.FileId == fileId).ToListAsync(ct);
        var removeRows = current.Where(s =>
            (s.SharedWithUserId is { } u && !wantUsers.Contains(u)) || (s.GroupId is { } g && !wantGroups.Contains(g)) || (s.AssignmentId is { } a && !wantCourses.Contains(a))).ToList();
        db.SharedFiles.RemoveRange(removeRows);

        var haveUsers = current.Where(s => s.SharedWithUserId != null).Select(s => s.SharedWithUserId!.Value).ToHashSet();
        var haveGroups = current.Where(s => s.GroupId != null).Select(s => s.GroupId!.Value).ToHashSet();
        var haveCourses = current.Where(s => s.AssignmentId != null).Select(s => s.AssignmentId!.Value).ToHashSet();

        var newUsers = wantUsers.Where(u => !haveUsers.Contains(u)).ToList();
        var newGroups = wantGroups.Where(g => !haveGroups.Contains(g)).ToList();
        var newCourses = wantCourses.Where(c => !haveCourses.Contains(c)).ToList();
        db.SharedFiles.AddRange(newUsers.Select(u => new SharedFile { FileId = fileId, SharedWithUserId = u }));
        db.SharedFiles.AddRange(newGroups.Select(g => new SharedFile { FileId = fileId, GroupId = g }));
        db.SharedFiles.AddRange(newCourses.Select(c => new SharedFile { FileId = fileId, AssignmentId = c }));
        await db.SaveChangesAsync(ct);

        // Who is told: the new people, the members of the new groups, the students of the new courses.
        var me = (await UserDirectory.LoadAsync(db, [userId], ct)).GetValueOrDefault(userId)?.Name ?? "Somebody";
        if (newUsers.Count > 0)
        {
            await notifications.AddForManyAsync(newUsers, "file", $"{me} shared a file with you", file.Name, "resources?tab=uploaded", ct);
        }

        foreach (var groupId in newGroups)
        {
            var members = await db.StudyGroupMembers.Where(m => m.GroupId == groupId && m.UserId != userId).Select(m => m.UserId).ToListAsync(ct);
            var groupName = options.Groups.First(g => g.Id == groupId).Name;
            await notifications.AddForManyAsync(members, "file", $"{me} shared a file in {groupName}", file.Name, $"groups/{groupId}", ct);
        }

        foreach (var assignmentId in newCourses)
        {
            var a = await db.TeachingAssignments.AsNoTracking().Where(x => x.Id == assignmentId).Select(x => new { x.SectionId, x.SubjectId }).FirstAsync(ct);
            var students = await Roster.Enrollments(db, a.SectionId, a.SubjectId).Select(e => e.Student.UserId).Distinct().ToListAsync(ct);
            var label = options.Courses.First(c => c.AssignmentId == assignmentId).Label;
            await notifications.AddForManyAsync(students, "file", $"{me} shared a file for {label}", file.Name, "resources?tab=uploaded", ct);
        }

        var total = wantUsers.Count + wantGroups.Count + wantCourses.Count;
        return ServiceResult.Ok(total == 0 ? "The file is private again." : "The file is shared.");
    }

    // ----------------------------------------------------------------- files from others

    /// <summary>The files other people shared with the person: directly, with a group they are in, or with a course they take (or teach).</summary>
    public async Task<IReadOnlyList<SharedFileRow>> GetSharedWithMeAsync(int userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var courseIds = await MyAssignmentIdsAsync(db, userId, ct);

        var rows = await db.SharedFiles.AsNoTracking()
            .Where(s => s.File.OwnerUserId != userId
                && (s.SharedWithUserId == userId
                    || (s.GroupId != null && db.StudyGroupMembers.Any(m => m.GroupId == s.GroupId && m.UserId == userId))
                    || (s.AssignmentId != null && courseIds.Contains(s.AssignmentId.Value))))
            .OrderByDescending(s => s.CreatedAtUtc).ThenByDescending(s => s.Id)
            .Select(s => new
            {
                s.FileId, s.File.Name, s.File.ContentType, s.File.SizeBytes, s.CreatedAtUtc, s.File.OwnerUserId,
                s.SharedWithUserId, s.GroupId, GroupName = s.Group != null ? s.Group.Name : null,
                s.AssignmentId, Subject = s.Assignment != null ? s.Assignment.Subject.Name : null,
                Grade = s.Assignment != null ? (byte?)s.Assignment.Section.GradeLevel : null,
                Strand = s.Assignment != null ? s.Assignment.Section.Strand.Code : null,
                Section = s.Assignment != null ? s.Assignment.Section.Name : null,
            })
            .Take(500)
            .ToListAsync(ct);

        var owners = await UserDirectory.LoadAsync(db, rows.Select(r => r.OwnerUserId).ToList(), ct);

        // A file that reaches the person in several ways is listed once (the first way: the newest share).
        return rows
            .GroupBy(r => r.FileId)
            .Select(g => g.First())
            .Select(r => new SharedFileRow(
                r.FileId, r.Name, r.ContentType, r.SizeBytes, r.CreatedAtUtc, r.OwnerUserId,
                owners.TryGetValue(r.OwnerUserId, out var o) ? o.Name : "Former member",
                r.SharedWithUserId is not null ? "person" : r.GroupId is not null ? "group" : "course",
                r.GroupName ?? (r.Subject is null ? null : $"{r.Subject} · {TeachingService.SectionLabel(r.Grade!.Value, r.Strand!, r.Section!)}")))
            .ToList();
    }

    /// <summary>The files shared with a group (only a member sees them).</summary>
    public async Task<IReadOnlyList<SharedFileRow>> GetGroupFilesAsync(int userId, int groupId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await db.StudyGroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserId == userId, ct))
        {
            return [];
        }

        var rows = await db.SharedFiles.AsNoTracking().Where(s => s.GroupId == groupId)
            .OrderByDescending(s => s.CreatedAtUtc).ThenByDescending(s => s.Id)
            .Select(s => new { s.FileId, s.File.Name, s.File.ContentType, s.File.SizeBytes, s.CreatedAtUtc, s.File.OwnerUserId })
            .ToListAsync(ct);
        var owners = await UserDirectory.LoadAsync(db, rows.Select(r => r.OwnerUserId).ToList(), ct);
        return rows.Select(r => new SharedFileRow(r.FileId, r.Name, r.ContentType, r.SizeBytes, r.CreatedAtUtc, r.OwnerUserId,
            owners.TryGetValue(r.OwnerUserId, out var o) ? o.Name : "Former member", "group", null)).ToList();
    }

    // ----------------------------------------------------------------- opening

    /// <summary>The file to download, when the person may open it: they own it, or it was shared with them.</summary>
    public async Task<FileDownload?> OpenAsync(int userId, int fileId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await CanReadAsync(db, userId, fileId, ct))
        {
            return null;
        }

        var file = await db.StoredFiles.AsNoTracking().Where(f => f.Id == fileId).Select(f => new { f.Name, f.ContentType, Data = f.Content!.Data }).FirstOrDefaultAsync(ct);
        return file is null ? null : new FileDownload(file.Name, file.ContentType, file.Data);
    }

    /// <summary>Keeps a copy of a file that was shared with the person in their own locker (it counts for the size of their locker).</summary>
    public async Task<ServiceResult> SaveCopyAsync(int userId, int fileId, CancellationToken ct = default)
    {
        var source = await OpenAsync(userId, fileId, ct);
        if (source is null)
        {
            return ServiceResult.Fail("This file is not shared with you any more.");
        }

        using var stream = new MemoryStream(source.Data, writable: false);
        var result = await UploadAsync(userId, source.Name, source.ContentType, stream, ct);
        return result.Succeeded ? ServiceResult.Ok($"{source.Name} was copied to your locker.", result.Id) : result;
    }

    internal static async Task<bool> CanReadAsync(AppDbContext db, int userId, int fileId, CancellationToken ct)
    {
        if (await db.StoredFiles.AnyAsync(f => f.Id == fileId && f.OwnerUserId == userId, ct))
        {
            return true;
        }

        var courseIds = await MyAssignmentIdsAsync(db, userId, ct);
        return await db.SharedFiles.AsNoTracking().AnyAsync(s => s.FileId == fileId
            && (s.SharedWithUserId == userId
                || (s.GroupId != null && db.StudyGroupMembers.Any(m => m.GroupId == s.GroupId && m.UserId == userId))
                || (s.AssignmentId != null && courseIds.Contains(s.AssignmentId.Value))), ct);
    }

    /// <summary>The courses (teaching assignments) the person takes, or teaches.</summary>
    private static async Task<List<int>> MyAssignmentIdsAsync(AppDbContext db, int userId, CancellationToken ct)
    {
        var studentId = await db.Students.AsNoTracking().Where(s => s.UserId == userId).Select(s => (int?)s.Id).FirstOrDefaultAsync(ct);
        if (studentId is not null)
        {
            return await Roster.AssignmentsOf(db, studentId.Value).Select(a => a.Id).ToListAsync(ct);
        }

        return await db.TeachingAssignments.AsNoTracking().Where(a => a.Teacher.UserId == userId).Select(a => a.Id).ToListAsync(ct);
    }

    // ----------------------------------------------------------------- helpers

    /// <summary>The name of a file as it is kept: no folders, no control characters, at most 255 characters. Null when nothing is left.</summary>
    public static string? CleanName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var name = Path.GetFileName(fileName.Replace('\\', '/'));
        name = new string(name.Where(c => !char.IsControl(c) && c != '"' && c != '<' && c != '>' && c != '|' && c != ':' && c != '*' && c != '?').ToArray()).Trim().Trim('.');
        if (name.Length == 0)
        {
            return null;
        }

        if (name.Length > 255)
        {
            var extension = Path.GetExtension(name);
            name = name[..Math.Max(1, 255 - extension.Length)] + extension;
        }

        return name;
    }

    private static string CleanType(string? contentType)
    {
        var type = (contentType ?? "").Trim().ToLowerInvariant();
        return type.Length is 0 or > 150 || !type.Contains('/') ? "application/octet-stream" : type;
    }
}
