using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>What a file is attached to.</summary>
public enum AttachmentOwnerKind
{
    /// <summary>A syllabus or a module of the library of the academic provider.</summary>
    MaterialModule,

    /// <summary>An activity, quiz or task performance of the library.</summary>
    MaterialItem,

    /// <summary>A syllabus or a module of a teacher's course.</summary>
    Module,

    /// <summary>An activity, quiz or task performance of a teacher's course.</summary>
    Item,

    /// <summary>The work a student sends for an activity, quiz or task performance (the work is sent as files).</summary>
    Submission,

    /// <summary>A file sent in a message (the sender and the person it is for open it).</summary>
    Message,
}

public readonly record struct AttachmentOwner(AttachmentOwnerKind Kind, int Id);

/// <summary>A file of a course as the Resources page lists it: what it is, who sent it, and where it belongs ("BUSINESS ACCOUNTING · Module 1" or "... · 11-BUSSENT-A · Journal entries").</summary>
/// <param name="From">Who sent it: the teacher of the course, the student ("You" for the student's own files).</param>
public sealed record CourseFileRow(int Id, string Name, long SizeBytes, DateTime CreatedAtUtc, string From, string Where, bool IsSlides = false);

/// <param name="IsSlides">A slide file (a PowerPoint or a PDF) of a lesson, listed apart from the other files of the module.</param>
public sealed record AttachmentRow(int Id, string Name, long SizeBytes, bool CanPreview, DateTime AtUtc, bool IsSlides = false);

/// <summary>A file chosen in a form, not saved yet.</summary>
public sealed record NewAttachment(string Name, string? ContentType, byte[] Data);

/// <summary>A file to hand to the browser: its name, the type it is served as and whether the browser may show it (otherwise it is saved).</summary>
public sealed record AttachmentDownload(string Name, string ContentType, bool Inline, byte[] Data);

/// <summary>
/// The files that go with a syllabus, a module, or an activity, quiz or task performance. The academic provider attaches files to the library and they
/// are copied to each teacher's draft when the library is sent (see <see cref="MaterialService"/>); a teacher can add and remove files in their own
/// copy and in what they write. The students of a course open them once the module is posted, and send their own work as files (the teacher of the course opens
/// those). A file opens in the browser (PDF, pictures, text, sound
/// and video; see <see cref="FilePreview"/>) or is saved. Programs and scripts are not accepted. Every change checks again, in the database, who is asking.
/// </summary>
public sealed class AttachmentService(IDbContextFactory<AppDbContext> dbFactory, SystemSettingsService settings, TimeProvider clock)
{
    /// <summary>The most files one syllabus, module or item can have.</summary>
    public const int MaxPerOwner = 10;

    // ======================================================================= reading

    public async Task<IReadOnlyList<AttachmentRow>> ListAsync(AttachmentOwner owner, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return (await RowsOf(db, owner.Kind, [owner.Id]).ToListAsync(ct)).Select(r => WithPreview(r.Row)).ToList();
    }

    /// <summary>The files of many syllabi, modules or items at once, by the id of the owner.</summary>
    public async Task<IReadOnlyDictionary<int, IReadOnlyList<AttachmentRow>>> ListForAsync(AttachmentOwnerKind kind, IReadOnlyCollection<int> ownerIds, CancellationToken ct = default)
    {
        if (ownerIds.Count == 0)
        {
            return new Dictionary<int, IReadOnlyList<AttachmentRow>>();
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await RowsOf(db, kind, ownerIds).ToListAsync(ct);
        return rows.GroupBy(r => r.OwnerId).ToDictionary(g => g.Key, g => (IReadOnlyList<AttachmentRow>)g.Select(r => WithPreview(r.Row)).ToList());
    }

    /// <summary>
    /// The files that go with the courses of a person, newest first. A student gets the files the teachers sent to their courses (of modules and assignments that are posted, slides
    /// included) and the files the student sent with their own work. A teacher gets the files the students sent with their work and the files the teacher attached to their own courses.
    /// </summary>
    public async Task<IReadOnlyList<CourseFileRow>> GetCourseFilesAsync(int userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var role = await db.Users.AsNoTracking().Where(u => u.Id == userId && u.IsActive).Select(u => (int?)u.RoleId).FirstOrDefaultAsync(ct);
        var rows = new List<CourseFileRow>();

        if (role == AppRoles.StudentId)
        {
            var studentId = await db.Students.AsNoTracking().Where(s => s.UserId == userId).Select(s => (int?)s.Id).FirstOrDefaultAsync(ct);
            if (studentId is null)
            {
                return [];
            }

            var assignmentIds = await Roster.AssignmentsOf(db, studentId.Value).Select(a => a.Id).ToListAsync(ct);

            var moduleFiles = await db.Attachments.AsNoTracking()
                .Where(a => a.ModuleId != null && assignmentIds.Contains(a.Module!.AssignmentId) && a.Module.VisibleFromUtc != null && a.Module.VisibleFromUtc <= now)
                .Select(a => new { a.Id, a.Name, a.SizeBytes, a.CreatedAtUtc, a.IsSlides, Subject = a.Module!.Assignment.Subject.Name, Title = a.Module.Title, Teacher = a.Module.Assignment.Teacher.FirstName + " " + a.Module.Assignment.Teacher.LastName })
                .ToListAsync(ct);
            rows.AddRange(moduleFiles.Select(f => new CourseFileRow(f.Id, f.Name, f.SizeBytes, f.CreatedAtUtc, f.Teacher, $"{f.Subject} · {f.Title}", f.IsSlides)));

            var itemFiles = await db.Attachments.AsNoTracking()
                .Where(a => a.ItemId != null && assignmentIds.Contains(a.Item!.Module.AssignmentId) && a.Item.Module.VisibleFromUtc != null && a.Item.Module.VisibleFromUtc <= now
                            && (a.Item.VisibleFromUtc == null || a.Item.VisibleFromUtc <= now))
                .Select(a => new { a.Id, a.Name, a.SizeBytes, a.CreatedAtUtc, Subject = a.Item!.Module.Assignment.Subject.Name, Title = a.Item.Title, Teacher = a.Item.Module.Assignment.Teacher.FirstName + " " + a.Item.Module.Assignment.Teacher.LastName })
                .ToListAsync(ct);
            rows.AddRange(itemFiles.Select(f => new CourseFileRow(f.Id, f.Name, f.SizeBytes, f.CreatedAtUtc, f.Teacher, $"{f.Subject} · {f.Title}")));

            var mine = await db.Attachments.AsNoTracking()
                .Where(a => a.SubmissionId != null && a.Submission!.StudentId == studentId)
                .Select(a => new { a.Id, a.Name, a.SizeBytes, a.CreatedAtUtc, Subject = a.Submission!.Item.Module.Assignment.Subject.Name, Title = a.Submission.Item.Title })
                .ToListAsync(ct);
            rows.AddRange(mine.Select(f => new CourseFileRow(f.Id, f.Name, f.SizeBytes, f.CreatedAtUtc, "You", $"{f.Subject} · {f.Title}")));
        }
        else if (role == AppRoles.TeacherId)
        {
            var teacherId = await db.Teachers.AsNoTracking().Where(t => t.UserId == userId).Select(t => (int?)t.Id).FirstOrDefaultAsync(ct);
            if (teacherId is null)
            {
                return [];
            }

            var sent = await db.Attachments.AsNoTracking()
                .Where(a => a.SubmissionId != null && a.Submission!.Item.Module.Assignment.TeacherId == teacherId)
                .Select(a => new
                {
                    a.Id, a.Name, a.SizeBytes, a.CreatedAtUtc, Subject = a.Submission!.Item.Module.Assignment.Subject.Name, Title = a.Submission.Item.Title,
                    Grade = a.Submission.Item.Module.Assignment.Section.GradeLevel, Strand = a.Submission.Item.Module.Assignment.Section.Strand.Code, Section = a.Submission.Item.Module.Assignment.Section.Name,
                    Student = a.Submission.Student.FirstName + " " + a.Submission.Student.LastName,
                })
                .ToListAsync(ct);
            rows.AddRange(sent.Select(f => new CourseFileRow(f.Id, f.Name, f.SizeBytes, f.CreatedAtUtc, f.Student, $"{f.Subject} · {TeachingService.SectionLabel(f.Grade, f.Strand, f.Section)} · {f.Title}")));

            var own = await db.Attachments.AsNoTracking()
                .Where(a => (a.ModuleId != null && a.Module!.Assignment.TeacherId == teacherId) || (a.ItemId != null && a.Item!.Module.Assignment.TeacherId == teacherId))
                .Select(a => new
                {
                    a.Id, a.Name, a.SizeBytes, a.CreatedAtUtc, a.IsSlides,
                    Subject = a.ModuleId != null ? a.Module!.Assignment.Subject.Name : a.Item!.Module.Assignment.Subject.Name,
                    Title = a.ModuleId != null ? a.Module!.Title : a.Item!.Title,
                })
                .ToListAsync(ct);
            rows.AddRange(own.Select(f => new CourseFileRow(f.Id, f.Name, f.SizeBytes, f.CreatedAtUtc, "You", $"{f.Subject} · {f.Title}", f.IsSlides)));
        }

        return rows.OrderByDescending(r => r.CreatedAtUtc).ThenBy(r => r.Name).ToList();
    }

    private sealed record OwnedRow(int OwnerId, AttachmentRow Row);

    private static IQueryable<OwnedRow> RowsOf(AppDbContext db, AttachmentOwnerKind kind, IReadOnlyCollection<int> ids)
    {
        var all = db.Attachments.AsNoTracking();
        var query = kind switch
        {
            AttachmentOwnerKind.MaterialModule => all.Where(a => a.MaterialModuleId != null && ids.Contains(a.MaterialModuleId.Value)).Select(a => new { Owner = a.MaterialModuleId!.Value, a }),
            AttachmentOwnerKind.MaterialItem => all.Where(a => a.MaterialItemId != null && ids.Contains(a.MaterialItemId.Value)).Select(a => new { Owner = a.MaterialItemId!.Value, a }),
            AttachmentOwnerKind.Module => all.Where(a => a.ModuleId != null && ids.Contains(a.ModuleId.Value)).Select(a => new { Owner = a.ModuleId!.Value, a }),
            AttachmentOwnerKind.Submission => all.Where(a => a.SubmissionId != null && ids.Contains(a.SubmissionId.Value)).Select(a => new { Owner = a.SubmissionId!.Value, a }),
            AttachmentOwnerKind.Message => all.Where(a => a.MessageId != null && ids.Contains(a.MessageId.Value)).Select(a => new { Owner = a.MessageId!.Value, a }),
            _ => all.Where(a => a.ItemId != null && ids.Contains(a.ItemId.Value)).Select(a => new { Owner = a.ItemId!.Value, a }),
        };

        return query.OrderBy(x => x.a.CreatedAtUtc).ThenBy(x => x.a.Id)
            .Select(x => new OwnedRow(x.Owner, new AttachmentRow(x.a.Id, x.a.Name, x.a.SizeBytes, false, x.a.CreatedAtUtc, x.a.IsSlides)));
    }

    /// <summary>The same list with the "can the browser show it" flag, which depends on the name (EF can't ask that).</summary>
    public static AttachmentRow WithPreview(AttachmentRow row) => row with { CanPreview = FilePreview.CanPreview(row.Name) };

    // ======================================================================= adding and removing

    /// <summary>The kinds of file a lesson's slides can be: a PowerPoint (or an OpenDocument or Keynote presentation) or a PDF.</summary>
    public static readonly string[] SlideExtensions = [".ppt", ".pptx", ".pps", ".ppsx", ".odp", ".key", ".pdf"];

    public static bool IsSlideFile(string fileName) => SlideExtensions.Contains(Path.GetExtension(fileName).ToLowerInvariant());

    /// <summary>Checks the files chosen in a form (names, programs, empty, too big, too many) before anything is saved. Gives the problems, or none.</summary>
    public IReadOnlyList<string> Check(IReadOnlyList<NewAttachment> files, int alreadyThere = 0, bool slides = false)
    {
        var maxBytes = settings.Current.MaxFileMegabytes * 1024L * 1024;
        var errors = new List<string>();
        foreach (var file in files)
        {
            var name = FileService.CleanName(file.Name);
            if (name is null)
            {
                errors.Add("A file has no name.");
            }
            else if (FileService.IsBlocked(name))
            {
                errors.Add($"{name}: programs and scripts can't be attached.");
            }
            else if (slides && !IsSlideFile(name))
            {
                errors.Add($"{name}: slides must be a PowerPoint file (.ppt, .pptx) or a PDF.");
            }
            else if (file.Data.Length == 0)
            {
                errors.Add($"{name} is empty.");
            }
            else if (file.Data.Length > maxBytes)
            {
                errors.Add($"{name} is bigger than {settings.Current.MaxFileMegabytes} MB.");
            }
        }

        if (alreadyThere + files.Count > MaxPerOwner)
        {
            errors.Add($"At most {MaxPerOwner} files can be attached here (there are {alreadyThere} already).");
        }

        return errors;
    }

    /// <param name="slides">The files are the slides of a module (only a module or a syllabus has slides).</param>
    public async Task<ServiceResult> AddAsync(int userId, AttachmentOwner owner, IReadOnlyList<NewAttachment> files, CancellationToken ct = default, bool slides = false)
    {
        if (files.Count == 0)
        {
            return ServiceResult.Ok("No file.");
        }

        var errors = Check(files, slides: slides).Where(e => !e.StartsWith("At most", StringComparison.Ordinal)).ToList();
        if (slides && owner.Kind is not (AttachmentOwnerKind.Module or AttachmentOwnerKind.MaterialModule))
        {
            return ServiceResult.Fail("Slides go with a module or a syllabus.");
        }

        if (errors.Count > 0)
        {
            return ServiceResult.Fail(errors);
        }

        var clean = files.Select(f => (Name: FileService.CleanName(f.Name)!, File: f)).ToList();

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await CanManageAsync(db, userId, owner, ct))
        {
            return ServiceResult.Fail("You can't attach files here.");
        }

        var existing = await CountAsync(db, owner, slides, ct);
        if (existing + clean.Count > MaxPerOwner)
        {
            return ServiceResult.Fail($"At most {MaxPerOwner} files can be attached here (there are {existing} already).");
        }

        foreach (var (name, file) in clean)
        {
            var attachment = new Attachment
            {
                Name = name, ContentType = CleanType(file.ContentType), SizeBytes = file.Data.Length, UploadedByUserId = userId, IsSlides = slides,
                Content = new AttachmentContent { Data = file.Data },
            };
            switch (owner.Kind)
            {
                case AttachmentOwnerKind.MaterialModule: attachment.MaterialModuleId = owner.Id; break;
                case AttachmentOwnerKind.MaterialItem: attachment.MaterialItemId = owner.Id; break;
                case AttachmentOwnerKind.Module: attachment.ModuleId = owner.Id; break;
                case AttachmentOwnerKind.Submission: attachment.SubmissionId = owner.Id; break;
                case AttachmentOwnerKind.Message: attachment.MessageId = owner.Id; break;
                default: attachment.ItemId = owner.Id; break;
            }

            db.Attachments.Add(attachment);
        }

        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok(clean.Count == 1 ? $"{clean[0].Name} was attached." : $"{clean.Count} files were attached.");
    }

    public async Task<ServiceResult> RemoveAsync(int userId, IReadOnlyCollection<int> attachmentIds, CancellationToken ct = default)
    {
        if (attachmentIds.Count == 0)
        {
            return ServiceResult.Ok("Nothing to remove.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await db.Attachments.Where(a => attachmentIds.Contains(a.Id))
            .Select(a => new { a.Id, a.MaterialModuleId, a.MaterialItemId, a.ModuleId, a.ItemId, a.SubmissionId, a.MessageId }).ToListAsync(ct);

        var removable = new List<int>();
        foreach (var row in rows)
        {
            if (row.MessageId is not null)
            {
                continue; // A message stays as it was sent: its files are not taken away afterwards.
            }

            var owner = row.MaterialModuleId is { } mm ? new AttachmentOwner(AttachmentOwnerKind.MaterialModule, mm)
                : row.MaterialItemId is { } mi ? new AttachmentOwner(AttachmentOwnerKind.MaterialItem, mi)
                : row.ModuleId is { } m ? new AttachmentOwner(AttachmentOwnerKind.Module, m)
                : row.SubmissionId is { } sub ? new AttachmentOwner(AttachmentOwnerKind.Submission, sub)
                : new AttachmentOwner(AttachmentOwnerKind.Item, row.ItemId ?? 0);
            if (await CanManageAsync(db, userId, owner, ct))
            {
                removable.Add(row.Id);
            }
        }

        if (removable.Count == 0)
        {
            return ServiceResult.Fail("You can't remove those files.");
        }

        await db.Attachments.Where(a => removable.Contains(a.Id)).ExecuteDeleteAsync(ct);
        return ServiceResult.Ok(removable.Count == 1 ? "The file was removed." : $"{removable.Count} files were removed.");
    }

    // ======================================================================= opening

    /// <summary>
    /// The file, when the person may open it: the provider opens the files of the library; a teacher opens the files of their own courses; a student
    /// opens the files of a module that is posted (and of an item that appears) in a course they take. The files of a student's work are opened by the student
    /// and by the teacher of the course.
    /// </summary>
    public async Task<AttachmentDownload?> OpenAsync(int userId, int attachmentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var a = await db.Attachments.AsNoTracking().Where(x => x.Id == attachmentId)
            .Select(x => new { x.Id, x.Name, x.MaterialModuleId, x.MaterialItemId, x.ModuleId, x.ItemId, x.SubmissionId, x.MessageId, Data = x.Content!.Data }).FirstOrDefaultAsync(ct);
        if (a is null || !await CanReadAsync(db, userId, a.MaterialModuleId, a.MaterialItemId, a.ModuleId, a.ItemId, a.SubmissionId, a.MessageId, ct))
        {
            return null;
        }

        // A teacher who opens a file of what the provider sent has opened it (the provider's tracking shows it).
        if (a.ModuleId is not null || a.ItemId is not null)
        {
            var moduleId = a.ModuleId ?? await db.ModuleItems.AsNoTracking().Where(i => i.Id == a.ItemId).Select(i => (int?)i.ModuleId).FirstOrDefaultAsync(ct);
            if (moduleId is { } id && await db.Teachers.AsNoTracking().AnyAsync(t => t.UserId == userId, ct))
            {
                var now = clock.GetUtcNow().UtcDateTime;
                await db.MaterialDeliveries.Where(d => d.ModuleId == id && d.OpenedAtUtc == null).ExecuteUpdateAsync(set => set.SetProperty(d => d.OpenedAtUtc, now), ct);
            }
        }

        var (type, inline) = FilePreview.Decide(a.Name, a.Data);
        return new AttachmentDownload(a.Name, type, inline, a.Data);
    }

    // ======================================================================= who may

    private async Task<bool> CanReadAsync(AppDbContext db, int userId, int? materialModuleId, int? materialItemId, int? moduleId, int? itemId, int? submissionId, int? messageId, CancellationToken ct)
    {
        var role = await db.Users.AsNoTracking().Where(u => u.Id == userId && u.IsActive).Select(u => (int?)u.RoleId).FirstOrDefaultAsync(ct);
        if (role is null)
        {
            return false;
        }

        if (materialModuleId is not null || materialItemId is not null)
        {
            return role == AppRoles.AcademicProviderId;
        }

        // A file of a message: the person who sent it and the person it was sent to.
        if (messageId is { } mid2)
        {
            return await db.Messages.AsNoTracking().AnyAsync(m => m.Id == mid2 && (m.SenderUserId == userId || m.RecipientUserId == userId), ct);
        }

        // The work of a student: the student who sent it and the teacher of the course.
        if (submissionId is { } sid)
        {
            var work = await db.Submissions.AsNoTracking().Where(x => x.Id == sid).Select(x => new { StudentUserId = x.Student.UserId, x.Item.Module.AssignmentId }).FirstOrDefaultAsync(ct);
            if (work is null)
            {
                return false;
            }

            return role == AppRoles.StudentId
                ? work.StudentUserId == userId
                : role == AppRoles.TeacherId && await db.TeachingAssignments.AsNoTracking().AnyAsync(x => x.Id == work.AssignmentId && x.Teacher.UserId == userId, ct);
        }

        // A teacher's copy: find the course (teaching assignment) and when it appears.
        int assignmentId;
        DateTime? moduleVisible;
        DateTime? itemVisible = null;
        if (moduleId is { } mid)
        {
            var m = await db.Modules.AsNoTracking().Where(x => x.Id == mid).Select(x => new { x.AssignmentId, x.VisibleFromUtc }).FirstOrDefaultAsync(ct);
            if (m is null)
            {
                return false;
            }

            assignmentId = m.AssignmentId;
            moduleVisible = m.VisibleFromUtc;
        }
        else if (itemId is { } iid)
        {
            var i = await db.ModuleItems.AsNoTracking().Where(x => x.Id == iid)
                .Select(x => new { x.Module.AssignmentId, ModuleVisible = x.Module.VisibleFromUtc, x.VisibleFromUtc }).FirstOrDefaultAsync(ct);
            if (i is null)
            {
                return false;
            }

            assignmentId = i.AssignmentId;
            moduleVisible = i.ModuleVisible;
            itemVisible = i.VisibleFromUtc;
        }
        else
        {
            return false;
        }

        if (role == AppRoles.TeacherId)
        {
            return await db.TeachingAssignments.AsNoTracking().AnyAsync(x => x.Id == assignmentId && x.Teacher.UserId == userId, ct);
        }

        if (role == AppRoles.StudentId)
        {
            var now = clock.GetUtcNow().UtcDateTime;
            if (moduleVisible is not { } shown || shown > now || (itemVisible is { } itemShown && itemShown > now))
            {
                return false;
            }

            var studentId = await db.Students.AsNoTracking().Where(s => s.UserId == userId).Select(s => (int?)s.Id).FirstOrDefaultAsync(ct);
            return studentId is not null && await Roster.AssignmentsOf(db, studentId.Value).AnyAsync(x => x.Id == assignmentId, ct);
        }

        return false;
    }

    private static async Task<bool> CanManageAsync(AppDbContext db, int userId, AttachmentOwner owner, CancellationToken ct)
    {
        var role = await db.Users.AsNoTracking().Where(u => u.Id == userId && u.IsActive).Select(u => (int?)u.RoleId).FirstOrDefaultAsync(ct);
        switch (owner.Kind)
        {
            case AttachmentOwnerKind.MaterialModule:
                return role == AppRoles.AcademicProviderId && await db.MaterialModules.AsNoTracking().AnyAsync(m => m.Id == owner.Id, ct);
            case AttachmentOwnerKind.MaterialItem:
                return role == AppRoles.AcademicProviderId && await db.MaterialItems.AsNoTracking().AnyAsync(i => i.Id == owner.Id, ct);
            case AttachmentOwnerKind.Module:
                return role == AppRoles.TeacherId && await db.Modules.AsNoTracking().AnyAsync(m => m.Id == owner.Id && m.Assignment.Teacher.UserId == userId, ct);
            case AttachmentOwnerKind.Message:
                // The files of a message are added by the person who sent it (and are not changed afterwards).
                return role is not null && await db.Messages.AsNoTracking().AnyAsync(m => m.Id == owner.Id && m.SenderUserId == userId, ct);
            case AttachmentOwnerKind.Submission:
                // The student changes the files of their own work until it is graded.
                return role == AppRoles.StudentId && await db.Submissions.AsNoTracking().AnyAsync(s => s.Id == owner.Id && s.Student.UserId == userId && s.Status != SubmissionStatus.Graded, ct);
            default:
                return role == AppRoles.TeacherId && await db.ModuleItems.AsNoTracking().AnyAsync(i => i.Id == owner.Id && i.Module.Assignment.Teacher.UserId == userId, ct);
        }
    }

    /// <summary>How many files (or how many slides) the owner has. The files and the slides of a module each have their own limit.</summary>
    private static async Task<int> CountAsync(AppDbContext db, AttachmentOwner owner, bool slides, CancellationToken ct) => owner.Kind switch
    {
        AttachmentOwnerKind.MaterialModule => await db.Attachments.CountAsync(a => a.MaterialModuleId == owner.Id && a.IsSlides == slides, ct),
        AttachmentOwnerKind.MaterialItem => await db.Attachments.CountAsync(a => a.MaterialItemId == owner.Id, ct),
        AttachmentOwnerKind.Module => await db.Attachments.CountAsync(a => a.ModuleId == owner.Id && a.IsSlides == slides, ct),
        AttachmentOwnerKind.Submission => await db.Attachments.CountAsync(a => a.SubmissionId == owner.Id, ct),
        AttachmentOwnerKind.Message => await db.Attachments.CountAsync(a => a.MessageId == owner.Id, ct),
        _ => await db.Attachments.CountAsync(a => a.ItemId == owner.Id, ct),
    };

    private static string CleanType(string? contentType)
    {
        var type = (contentType ?? "").Split(';')[0].Trim();
        return type.Length is > 0 and <= 100 && type.Contains('/') && !type.Any(char.IsControl) ? type : "application/octet-stream";
    }
}
