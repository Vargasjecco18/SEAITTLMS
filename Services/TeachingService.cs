using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Components.Shared;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>One subject a teacher teaches to one section, with the numbers shown on its card.</summary>
/// <param name="Activities">How many activities, quizzes and task performances the course has (in all its modules).</param>
public sealed record TeacherSubjectCard(
    int AssignmentId, int SubjectId, string SubjectName, int SectionId, string SectionLabel,
    int Students, int ModulesPosted, int ModulesTotal, int ToGrade, string? Meets = null,
    int Activities = 0, int Quizzes = 0, int TaskPerformances = 0, string? StrandCode = null, string? StrandName = null);

/// <summary>One activity, quiz or task performance in the list a card opens: what a student still has to do, or what a teacher still has to check.</summary>
/// <param name="Note">"Due Oct 9, 2026, 5:00 PM", or "3 to check".</param>
public sealed record CourseWorkItem(int ItemId, string Kind, string Title, string ModuleTitle, DateTime? DueAtUtc, int ToCheck = 0);

/// <summary>
/// One subject a student takes (from their enrollment), with the numbers shown on its card and in the list.
/// <paramref name="IsCompleted"/>: the term of the subject is over (the page calls those "Completed", the others "Enrolled").
/// <paramref name="AttendancePercent"/>: the days the student came (present or late) out of the days marked, or null when none is marked.
/// <paramref name="NextDueUtc"/>: the earliest deadline of the work not handed in yet (it may be past).
/// <paramref name="Meets"/>: when the class meets, like "MWF · 8:00 AM – 9:30 AM" (empty until the academic coordinator sets it).
/// </summary>
public sealed record StudentSubjectCard(
    int SubjectId, string SubjectName, string TermName, bool IsCurrentTerm, int? AssignmentId, string? TeacherName, string SectionLabel,
    int ModulesVisible, int ModulesUnopened, int ItemsOpen, int ItemsDone, decimal? AveragePercent,
    bool IsCompleted = false, decimal? AttendancePercent = null, DateTime? NextDueUtc = null, string? Meets = null,
    int Activities = 0, int Quizzes = 0, int TaskPerformances = 0, string? StrandCode = null, string? StrandName = null)
{
    /// <summary>The modules the student has opened.</summary>
    public int ProgressDone => ModulesVisible - ModulesUnopened;

    /// <summary>The modules the teacher has posted.</summary>
    public int ProgressTotal => ModulesVisible;

    /// <summary>How far along the student is in the course: the modules they opened, out of the modules that are posted. Null when no module is posted yet.</summary>
    public int? ProgressPercent => AssignmentId is null || ProgressTotal == 0 ? null : (int)Math.Round(ProgressDone * 100.0 / ProgressTotal);

    public string ProgressText => $"{ProgressDone} of {ProgressTotal} {(ProgressTotal == 1 ? "module" : "modules")} opened";
}

/// <summary>A teacher's subject as the administrator sees it.</summary>
public sealed record AssignmentRow(int Id, int SubjectId, string SubjectName, int SectionId, string SectionLabel, int Students, int Modules);

/// <summary>A section the administrator can give to a teacher, with the subjects its strand has at its grade level.</summary>
public sealed record SectionChoice(int SectionId, string Label, IReadOnlyList<SubjectChoice> Subjects, int StrandId = 0);

public sealed record SubjectChoice(int SubjectId, string Name);

public sealed record RemovalImpact(string SubjectName, string SectionLabel, int Modules, int Items, int Submissions);

/// <summary>
/// Who teaches what. A subject of a section has one teacher, set by the administrator. Students see the subjects they are
/// enrolled in; the ones that have a teacher can have modules.
/// </summary>
public sealed class TeachingService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider clock)
{
    public static string SectionLabel(byte gradeLevel, string strandCode, string name) => $"{gradeLevel}-{strandCode}-{name}";

    // ------------------------------------------------------------------ the administrator

    public async Task<IReadOnlyList<AssignmentRow>> ListForTeacherUserAsync(int teacherUserId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var rows = await db.TeachingAssignments.AsNoTracking()
            .Where(a => a.Teacher.UserId == teacherUserId)
            .OrderBy(a => a.Section.GradeLevel).ThenBy(a => a.Section.Strand.Code).ThenBy(a => a.Section.Name).ThenBy(a => a.Subject.Name)
            .Select(a => new
            {
                a.Id, a.SubjectId, SubjectName = a.Subject.Name, a.SectionId, a.Section.GradeLevel, StrandCode = a.Section.Strand.Code,
                SectionName = a.Section.Name,
                Modules = a.Modules.Count,
            })
            .ToListAsync(ct);

        var result = new List<AssignmentRow>();
        foreach (var r in rows)
        {
            var students = await Roster.StudentIds(db, r.SectionId, r.SubjectId).CountAsync(ct);
            result.Add(new AssignmentRow(r.Id, r.SubjectId, r.SubjectName, r.SectionId, SectionLabel(r.GradeLevel, r.StrandCode, r.SectionName), students, r.Modules));
        }

        return result;
    }

    /// <summary>The sections that exist, each with the subjects of its strand and grade level (any term).</summary>
    public async Task<IReadOnlyList<SectionChoice>> GetAssignableAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var sections = await db.Sections.AsNoTracking()
            .OrderByDescending(s => s.SchoolYear.StartDate).ThenBy(s => s.GradeLevel).ThenBy(s => s.Strand.Code).ThenBy(s => s.Name)
            .Select(s => new { s.Id, s.StrandId, s.GradeLevel, StrandCode = s.Strand.Code, s.Name, Year = s.SchoolYear.Name })
            .ToListAsync(ct);

        var curriculum = await db.StrandSubjects.AsNoTracking()
            .Select(x => new { x.StrandId, x.GradeLevel, x.SubjectId, x.Subject.Name })
            .ToListAsync(ct);

        return sections.Select(s => new SectionChoice(
            s.Id,
            $"{SectionLabel(s.GradeLevel, s.StrandCode, s.Name)} · {s.Year}",
            curriculum.Where(c => c.StrandId == s.StrandId && c.GradeLevel == s.GradeLevel)
                .Select(c => new SubjectChoice(c.SubjectId, c.Name)).DistinctBy(c => c.SubjectId).OrderBy(c => c.Name).ToList(),
            s.StrandId))
            .Where(s => s.Subjects.Count > 0)
            .ToList();
    }

    public async Task<ServiceResult> AssignAsync(int teacherUserId, int sectionId, int subjectId, int actingUserId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        if (!await IsActiveAdminAsync(db, actingUserId, ct))
        {
            return ServiceResult.Fail("Only an administrator can give courses to teachers.");
        }

        var teacher = await db.Teachers.AsNoTracking().Where(t => t.UserId == teacherUserId).Select(t => new { t.Id, t.User.IsActive }).FirstOrDefaultAsync(ct);
        if (teacher is null)
        {
            return ServiceResult.Fail("This teacher no longer exists.");
        }

        var section = await db.Sections.AsNoTracking().Where(s => s.Id == sectionId).Select(s => new { s.Id, s.StrandId, s.GradeLevel, s.Name, StrandCode = s.Strand.Code }).FirstOrDefaultAsync(ct);
        var subject = await db.Subjects.AsNoTracking().Where(s => s.Id == subjectId).Select(s => new { s.Id, s.Name }).FirstOrDefaultAsync(ct);
        if (section is null || subject is null)
        {
            return ServiceResult.Fail("Choose a section and a course.");
        }

        // The subject must be one that the strand has at the grade level of the section.
        if (!await db.StrandSubjects.AnyAsync(x => x.StrandId == section.StrandId && x.GradeLevel == section.GradeLevel && x.SubjectId == subjectId, ct))
        {
            return ServiceResult.Fail($"{subject.Name} is not one of the courses of {section.StrandCode} in grade {section.GradeLevel}.");
        }

        var existing = await db.TeachingAssignments.AsNoTracking().Where(a => a.SectionId == sectionId && a.SubjectId == subjectId)
            .Select(a => new { a.TeacherId, Name = a.Teacher.FirstName + " " + a.Teacher.LastName }).FirstOrDefaultAsync(ct);
        if (existing is not null)
        {
            return ServiceResult.Fail(existing.TeacherId == teacher.Id
                ? "This teacher already teaches that course to that section."
                : $"{subject.Name} of that section is already taught by {existing.Name}. Remove it from them first.");
        }

        // When the course already has class times, the teacher must be free then.
        var meeting = await db.CourseSchedules.AsNoTracking().Where(c => c.SectionId == sectionId && c.SubjectId == subjectId).FirstOrDefaultAsync(ct);
        if (meeting is not null && await CourseScheduleService.TeacherConflictAsync(db, teacher.Id, sectionId, subjectId, meeting.Days, meeting.StartTime, meeting.EndTime, ct) is { } conflict)
        {
            return ServiceResult.Fail(conflict);
        }

        db.TeachingAssignments.Add(new TeachingAssignment { TeacherId = teacher.Id, SectionId = sectionId, SubjectId = subjectId });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Fail("Somebody just gave that course to a teacher. Reload and look again.");
        }

        return ServiceResult.Ok($"{subject.Name} of {SectionLabel(section.GradeLevel, section.StrandCode, section.Name)} was added.");
    }

    public async Task<RemovalImpact?> GetRemovalImpactAsync(int assignmentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var a = await db.TeachingAssignments.AsNoTracking().Where(x => x.Id == assignmentId)
            .Select(x => new { SubjectName = x.Subject.Name, x.Section.GradeLevel, StrandCode = x.Section.Strand.Code, SectionName = x.Section.Name })
            .FirstOrDefaultAsync(ct);
        if (a is null)
        {
            return null;
        }

        return new RemovalImpact(
            a.SubjectName, SectionLabel(a.GradeLevel, a.StrandCode, a.SectionName),
            await db.Modules.CountAsync(m => m.AssignmentId == assignmentId, ct),
            await db.ModuleItems.CountAsync(i => i.Module.AssignmentId == assignmentId, ct),
            await db.Submissions.CountAsync(s => s.Item.Module.AssignmentId == assignmentId, ct));
    }

    /// <summary>Takes a subject away from a teacher. Its modules, activities and grades are deleted with it.</summary>
    public async Task<ServiceResult> RemoveAsync(int assignmentId, int actingUserId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        if (!await IsActiveAdminAsync(db, actingUserId, ct))
        {
            return ServiceResult.Fail("Only an administrator can take courses away from teachers.");
        }

        var deleted = await db.TeachingAssignments.Where(a => a.Id == assignmentId).ExecuteDeleteAsync(ct);
        return deleted == 0 ? ServiceResult.Fail("This course was already removed.") : ServiceResult.Ok("The course was removed.");
    }

    private static async Task<bool> IsActiveAdminAsync(AppDbContext db, int userId, CancellationToken ct) =>
        await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.IsActive && u.RoleId == AppRoles.AdminId, ct);

    // ------------------------------------------------------------------ the teacher

    public async Task<IReadOnlyList<TeacherSubjectCard>> GetTeacherCardsAsync(int teacherId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;

        var rows = await db.TeachingAssignments.AsNoTracking()
            .Where(a => a.TeacherId == teacherId)
            .OrderBy(a => a.Section.GradeLevel).ThenBy(a => a.Section.Strand.Code).ThenBy(a => a.Section.Name).ThenBy(a => a.Subject.Name)
            .Select(a => new
            {
                a.Id, a.SubjectId, SubjectName = a.Subject.Name, a.SectionId, a.Section.GradeLevel, StrandCode = a.Section.Strand.Code, StrandName = a.Section.Strand.Name, SectionName = a.Section.Name,
                Total = a.Modules.Count, Posted = a.Modules.Count(m => m.VisibleFromUtc != null && m.VisibleFromUtc <= now),
                ToGrade = db.Submissions.Count(s => s.Item.Module.AssignmentId == a.Id && s.Status == SubmissionStatus.Submitted && s.LateDecision != LateDecisions.Declined),
            })
            .ToListAsync(ct);

        var sectionIds = rows.Select(r => r.SectionId).Distinct().ToList();
        var meetings = await db.CourseSchedules.AsNoTracking().Where(c => sectionIds.Contains(c.SectionId)).ToListAsync(ct);

        // What each course holds: the activities, quizzes and task performances of all its modules.
        var assignmentIds = rows.Select(r => r.Id).ToList();
        var kinds = await db.ModuleItems.AsNoTracking()
            .Where(i => assignmentIds.Contains(i.Module.AssignmentId))
            .GroupBy(i => new { i.Module.AssignmentId, i.Kind })
            .Select(g => new { g.Key.AssignmentId, g.Key.Kind, Count = g.Count() })
            .ToListAsync(ct);
        int KindCount(int assignmentId, string kind) => kinds.FirstOrDefault(k => k.AssignmentId == assignmentId && k.Kind == kind)?.Count ?? 0;

        var cards = new List<TeacherSubjectCard>();
        foreach (var r in rows)
        {
            var students = await Roster.StudentIds(db, r.SectionId, r.SubjectId).CountAsync(ct);
            var meets = meetings.FirstOrDefault(m => m.SectionId == r.SectionId && m.SubjectId == r.SubjectId);
            cards.Add(new TeacherSubjectCard(r.Id, r.SubjectId, r.SubjectName, r.SectionId, SectionLabel(r.GradeLevel, r.StrandCode, r.SectionName), students, r.Posted, r.Total, r.ToGrade,
                meets is null ? null : new MeetingInfo(meets.Days, meets.StartTime, meets.EndTime).Describe(),
                KindCount(r.Id, ItemKinds.Activity) + KindCount(r.Id, ItemKinds.Seatwork) + KindCount(r.Id, ItemKinds.Recitation), KindCount(r.Id, ItemKinds.Quiz), KindCount(r.Id, ItemKinds.TaskPerformance), r.StrandCode, r.StrandName));
        }

        return cards;
    }

    /// <summary>What a teacher still has to check in a course: the work handed in and not graded yet, one line for each activity, quiz or task performance.</summary>
    public async Task<IReadOnlyList<CourseWorkItem>> GetTeacherToCheckAsync(int teacherId, int assignmentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await db.TeachingAssignments.AsNoTracking().AnyAsync(a => a.Id == assignmentId && a.TeacherId == teacherId, ct))
        {
            return [];
        }

        var rows = await db.Submissions.AsNoTracking()
            .Where(s => s.Item.Module.AssignmentId == assignmentId && s.Status == SubmissionStatus.Submitted && s.LateDecision != LateDecisions.Declined)
            .GroupBy(s => new { s.ItemId, s.Item.Kind, s.Item.Title, ModuleTitle = s.Item.Module.Title, s.Item.DueAtUtc, s.Item.Position })
            .Select(g => new { g.Key.ItemId, g.Key.Kind, g.Key.Title, g.Key.ModuleTitle, g.Key.DueAtUtc, Count = g.Count() })
            .ToListAsync(ct);
        return rows.OrderBy(r => r.ModuleTitle).ThenBy(r => r.Title)
            .Select(r => new CourseWorkItem(r.ItemId, r.Kind, r.Title, r.ModuleTitle, r.DueAtUtc, r.Count))
            .ToList();
    }

    /// <summary>What a student still has to do in a course: the posted activities, quizzes and task performances that are not handed in yet (nearest deadline first).</summary>
    public async Task<IReadOnlyList<CourseWorkItem>> GetStudentToDoAsync(int studentId, int assignmentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;

        // Only a course the student is enrolled in.
        if (!await Roster.AssignmentsOf(db, studentId).AnyAsync(a => a.Id == assignmentId, ct))
        {
            return [];
        }

        var items = await db.ModuleItems.AsNoTracking()
            .Where(i => i.Module.AssignmentId == assignmentId && i.Module.VisibleFromUtc != null && i.Module.VisibleFromUtc <= now
                        && (i.VisibleFromUtc == null || i.VisibleFromUtc <= now)
                        && !db.Submissions.Any(s => s.ItemId == i.Id && s.StudentId == studentId && s.Status != SubmissionStatus.InProgress))
            .Select(i => new { i.Id, i.Kind, i.Title, ModuleTitle = i.Module.Title, i.DueAtUtc })
            .ToListAsync(ct);
        return items.OrderBy(i => i.DueAtUtc == null).ThenBy(i => i.DueAtUtc).ThenBy(i => i.Title)
            .Select(i => new CourseWorkItem(i.Id, i.Kind, i.Title, i.ModuleTitle, i.DueAtUtc))
            .ToList();
    }

    // ------------------------------------------------------------------ the student

    /// <summary>Every subject the student is enrolled in, the ones of the current term first.</summary>
    public async Task<IReadOnlyList<StudentSubjectCard>> GetStudentCardsAsync(int studentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;

        var enrolled = await db.EnrollmentSubjects.AsNoTracking()
            .Where(x => x.Enrollment.StudentId == studentId && x.Enrollment.Status != EnrollmentStatus.Dropped)
            .Select(x => new
            {
                x.SubjectId, SubjectName = x.Subject.Name, x.Enrollment.SectionId, x.Enrollment.GradeLevel, StrandCode = x.Enrollment.Strand.Code, StrandName = x.Enrollment.Strand.Name,
                SectionName = x.Enrollment.Section.Name, TermName = x.Enrollment.Term.Name, x.Enrollment.Term.IsCurrent, x.Enrollment.Term.StartDate,
                x.Enrollment.Term.EndDate, EnrollmentStatus = x.Enrollment.Status,
            })
            .ToListAsync(ct);

        var sectionIds = enrolled.Select(e => e.SectionId).Distinct().ToList();
        var assignments = await db.TeachingAssignments.AsNoTracking()
            .Where(a => sectionIds.Contains(a.SectionId))
            .Select(a => new { a.Id, a.SectionId, a.SubjectId, Teacher = a.Teacher.FirstName + " " + a.Teacher.LastName })
            .ToListAsync(ct);

        var assignmentIds = assignments.Select(a => a.Id).ToList();
        var modules = await db.Modules.AsNoTracking()
            .Where(m => assignmentIds.Contains(m.AssignmentId) && m.VisibleFromUtc != null && m.VisibleFromUtc <= now)
            .Select(m => new { m.Id, m.AssignmentId })
            .ToListAsync(ct);
        var opened = (await db.ModuleViews.AsNoTracking().Where(v => v.StudentId == studentId).Select(v => v.ModuleId).ToListAsync(ct)).ToHashSet();

        var items = await db.ModuleItems.AsNoTracking()
            .Where(i => assignmentIds.Contains(i.Module.AssignmentId) && i.Module.VisibleFromUtc != null && i.Module.VisibleFromUtc <= now
                        && (i.VisibleFromUtc == null || i.VisibleFromUtc <= now))
            .Select(i => new { i.Id, i.Module.AssignmentId, i.MaxScore, i.DueAtUtc, i.Kind })
            .ToListAsync(ct);
        var attendance = await db.AttendanceRecords.AsNoTracking()
            .Where(r => r.StudentId == studentId && assignmentIds.Contains(r.AssignmentId))
            .Select(r => new { r.AssignmentId, r.Status })
            .ToListAsync(ct);
        var today = PhilippineTime.Today(clock);
        var mine = await db.Submissions.AsNoTracking()
            .Where(s => s.StudentId == studentId && assignmentIds.Contains(s.Item.Module.AssignmentId))
            .Select(s => new { s.ItemId, s.Status, s.Score })
            .ToListAsync(ct);

        var enrolledSectionIds = enrolled.Select(e => e.SectionId).Distinct().ToList();
        var meetings = await db.CourseSchedules.AsNoTracking().Where(c => enrolledSectionIds.Contains(c.SectionId)).ToListAsync(ct);

        return enrolled
            .OrderByDescending(e => e.IsCurrent).ThenByDescending(e => e.StartDate).ThenBy(e => e.SubjectName)
            .Select(e =>
            {
                var a = assignments.FirstOrDefault(x => x.SectionId == e.SectionId && x.SubjectId == e.SubjectId);
                var mods = a is null ? [] : modules.Where(m => m.AssignmentId == a.Id).ToList();
                var its = a is null ? [] : items.Where(i => i.AssignmentId == a.Id).ToList();
                var done = its.Count(i => mine.Any(s => s.ItemId == i.Id && s.Status != SubmissionStatus.InProgress));
                var graded = its.Where(i => mine.Any(s => s.ItemId == i.Id && s.Status == SubmissionStatus.Graded && s.Score != null))
                    .Select(i => mine.First(s => s.ItemId == i.Id).Score!.Value / i.MaxScore * 100).ToList();
                var nextDue = its.Where(i => i.DueAtUtc != null && !mine.Any(s => s.ItemId == i.Id && s.Status != SubmissionStatus.InProgress))
                    .Select(i => i.DueAtUtc).Min();
                var marked = a is null ? [] : attendance.Where(r => r.AssignmentId == a.Id).ToList();
                var came = marked.Count(r => r.Status is AttendanceStatus.Present or AttendanceStatus.Late);

                return new StudentSubjectCard(
                    e.SubjectId, e.SubjectName, e.TermName, e.IsCurrent, a?.Id, a?.Teacher,
                    SectionLabel(e.GradeLevel, e.StrandCode, e.SectionName),
                    mods.Count, mods.Count(m => !opened.Contains(m.Id)), its.Count - done, done,
                    graded.Count == 0 ? null : Math.Round(graded.Average(), 1),
                    IsCompleted: e.EnrollmentStatus == EnrollmentStatus.Completed || (!e.IsCurrent && e.EndDate < today),
                    AttendancePercent: marked.Count == 0 ? null : Math.Round(came * 100m / marked.Count, 1),
                    NextDueUtc: nextDue,
                    Meets: meetings.FirstOrDefault(m => m.SectionId == e.SectionId && m.SubjectId == e.SubjectId) is { } m ? new MeetingInfo(m.Days, m.StartTime, m.EndTime).Describe() : null,
                    Activities: its.Count(i => i.Kind is ItemKinds.Activity or ItemKinds.Seatwork or ItemKinds.Recitation), Quizzes: its.Count(i => i.Kind == ItemKinds.Quiz), TaskPerformances: its.Count(i => i.Kind == ItemKinds.TaskPerformance),
                    StrandCode: e.StrandCode, StrandName: e.StrandName);
            })
            .ToList();
    }
}

/// <summary>Who takes a subject in a section: the students enrolled in that section who have that subject on their enrollment.</summary>
internal static class Roster
{
    public static IQueryable<Enrollment> Enrollments(AppDbContext db, int sectionId, int subjectId) =>
        db.Enrollments.Where(e => e.SectionId == sectionId && e.Status == EnrollmentStatus.Enrolled
                                  && e.EnrolledSubjects.Any(x => x.SubjectId == subjectId));

    /// <summary>The teaching assignments of the subjects a student takes now: the student is enrolled in the section with the subject.</summary>
    public static IQueryable<TeachingAssignment> AssignmentsOf(AppDbContext db, int studentId) =>
        db.TeachingAssignments.Where(a => db.EnrollmentSubjects.Any(x => x.Enrollment.StudentId == studentId && x.Enrollment.Status == EnrollmentStatus.Enrolled
                                                                         && x.Enrollment.SectionId == a.SectionId && x.SubjectId == a.SubjectId));

    public static IQueryable<int> StudentIds(AppDbContext db, int sectionId, int subjectId) =>
        Enrollments(db, sectionId, subjectId).Select(e => e.StudentId).Distinct();
}
