using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Components.Shared;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>A course in the list of the course materials, with what the provider has prepared for it.</summary>
/// <param name="Placement">In a strand's list: where the course is studied, like "Grade 11 · 1st Term, Grade 12 · 1st Term".</param>
public sealed record MaterialCourseRow(
    int SubjectId, string Code, string Name, bool HasSyllabus, int Modules, int Exams, int Teachers, int Sent, string? Placement = null);

/// <summary>A strand of a track in the course library, with how many courses it studies and how many of them have a syllabus.</summary>
public sealed record LibraryStrand(int StrandId, string Code, string Name, int Courses, int WithSyllabus);

/// <summary>A term of a strand in the course library (1st Term, 2nd Term): how many subjects the strand studies in it and how many of them have a syllabus.</summary>
public sealed record LibraryTerm(int TermNumber, string Name, int Courses, int WithSyllabus);

/// <summary>A track of the course library: the first thing the provider sees. Its strands come next, then the courses of a strand.</summary>
public sealed record LibraryTrack(int TrackId, string Code, string Name, int Courses, int WithSyllabus, IReadOnlyList<LibraryStrand> Strands);

/// <summary>A summative exam of the library: the schedule it runs in (from <paramref name="OpensAtUtc"/> to <paramref name="DueAtUtc"/>) and to how many teachers' courses it was sent.</summary>
public sealed record MaterialExamView(
    int Id, string Title, string? Instructions, decimal MaxScore, DateTime? OpensAtUtc, DateTime? DueAtUtc, int SentTo,
    int Questions = 0, int? TimeLimitMinutes = null, bool AntiCheat = true, int MaxViolations = 3, bool ShuffleQuestions = true, bool ShuffleOptions = true, bool ShowResult = true);

/// <param name="SentTo">To how many teachers' courses it was sent.</param>
public sealed record MaterialModuleView(
    int Id, bool IsSyllabus, string Title, string? Description, string? Body, string? LinkUrl, int Position, int SentTo);

/// <summary>
/// What the provider writes for a summative exam: the paper (instructions and files), the questions (from the Excel file; the students answer them on the screen and the system
/// gives the score), the settings of a quiz (time limit, the anti-cheating watch ...) and the schedule.
/// </summary>
public sealed class ExamInput
{
    public string Title { get; set; } = "";
    public string? Instructions { get; set; }
    public decimal MaxScore { get; set; } = 100;

    /// <summary>The questions. Empty (null) leaves the questions the exam has as they are; a list replaces them (the highest score then is the total of their points).</summary>
    public IReadOnlyList<QuestionInput>? Questions { get; set; }

    public int? TimeLimitMinutes { get; set; }
    public bool AntiCheat { get; set; } = true;
    public int MaxViolations { get; set; } = 3;
    public bool ShuffleQuestions { get; set; } = true;
    public bool ShuffleOptions { get; set; } = true;
    public bool ShowResult { get; set; } = true;

    /// <summary>When the exam opens (UTC). Both ends of the schedule are needed.</summary>
    public DateTime? OpensAtUtc { get; set; }

    /// <summary>When the exam closes (UTC).</summary>
    public DateTime? DueAtUtc { get; set; }

    public string OpensText { get; set; } = "";
    public string DueText { get; set; } = "";
}

/// <summary>One teacher's course (a teacher, a section) that the materials of the course can be sent to, and which modules it got.</summary>
public sealed record MaterialTeacherRow(
    int AssignmentId, int TeacherUserId, string TeacherName, string SectionLabel, int Students, IReadOnlyList<int> Received, int ExamsGot = 0);

public sealed record CourseWithoutSyllabus(int SubjectId, string Name, int Teachers);

public sealed record RecentDelivery(string SubjectName, string ModuleTitle, string TeacherName, string SectionLabel, bool IsSyllabus, bool Posted, DateTime SentAtUtc);

/// <summary>What the provider's dashboard shows about the course materials.</summary>
/// <param name="CoursesTaught">Courses that have a teacher this school year.</param>
/// <param name="StillDrafts">Modules that were sent and that the teachers have not posted yet.</param>
public sealed record MaterialOverview(
    int CoursesTaught, int LibraryModules, int LibraryItems, int Sent, int StillDrafts, IReadOnlyList<CourseWithoutSyllabus> WithoutSyllabus,
    IReadOnlyList<RecentDelivery> Recent);

public sealed record CourseMaterials(
    int SubjectId, string Code, string Name, IReadOnlyList<MaterialModuleView> Modules, IReadOnlyList<MaterialExamView> Exams, IReadOnlyList<MaterialTeacherRow> Teachers);

/// <param name="Copied">How many modules and exams were put in teachers' courses (a module sent to three teachers counts three times).</param>
/// <param name="Teachers">How many teachers got something new.</param>
/// <param name="Skipped">How many modules had been sent to that teacher's course before.</param>
public sealed record SendOutcome(int Copied, int Teachers, int Skipped);

/// <summary>
/// The course materials of the academic provider: a library per course (a syllabus, modules and the summative exams) that is sent to the teachers who
/// teach the course. The provider does not write assignments, quizzes or task performances: those are the teacher's. A teacher gets their own copy as a draft: students
/// can't see it until the teacher posts it, and the teacher can change everything (when it is posted, the due dates, the text).
/// Every change checks again, in the database, that the person is an active academic provider.
/// </summary>
public sealed class MaterialService(IDbContextFactory<AppDbContext> dbFactory, NotificationService notifications, TimeProvider clock)
{
    private const string ProviderOnly = "Only the academic provider prepares course materials.";

    // ======================================================================= reading

    /// <summary>Every active course with what is prepared for it. <paramref name="search"/> looks in the name and the code.</summary>
    public async Task<IReadOnlyList<MaterialCourseRow>> ListCoursesAsync(string? search = null, CancellationToken ct = default, int? strandId = null, int? termNumber = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var yearId = await CurrentYearIdAsync(db, ct);

        var query = db.Subjects.AsNoTracking().Where(s => s.IsActive);
        Dictionary<int, string>? placements = null;
        if (strandId is { } onlyStrand)
        {
            // Only the courses this strand studies, and where (grade level and term).
            var rows = await db.StrandSubjects.AsNoTracking().Where(x => x.StrandId == onlyStrand && (termNumber == null || x.TermNumber == termNumber))
                .Select(x => new { x.SubjectId, x.GradeLevel, x.TermNumber }).ToListAsync(ct);
            var termNames = await db.Terms.AsNoTracking().OrderByDescending(t => t.SchoolYear.StartDate).Select(t => new { t.TermNumber, t.Name }).ToListAsync(ct);
            string TermName(byte number) => termNames.FirstOrDefault(t => t.TermNumber == number)?.Name ?? $"Term {number}";
            placements = rows.GroupBy(x => x.SubjectId).ToDictionary(
                g => g.Key,
                g => string.Join(", ", g.OrderBy(x => x.GradeLevel).ThenBy(x => x.TermNumber).Select(x => $"Grade {x.GradeLevel} · {TermName(x.TermNumber)}")));
            var subjectIds = placements.Keys.ToList();
            query = query.Where(s => subjectIds.Contains(s.Id));
        }
        foreach (var token in (search ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var t = token;
            query = query.Where(s => s.Name.Contains(t) || s.Code.Contains(t));
        }

        var subjects = await query.OrderBy(s => s.Name).Select(s => new { s.Id, s.Code, s.Name }).ToListAsync(ct);

        var modules = await db.MaterialModules.AsNoTracking().Where(m => !m.IsExamSet)
            .GroupBy(m => m.SubjectId)
            .Select(g => new { SubjectId = g.Key, Syllabus = g.Count(m => m.IsSyllabus), Modules = g.Count(m => !m.IsSyllabus) })
            .ToListAsync(ct);
        var exams = await db.MaterialItems.AsNoTracking().Where(i => i.Kind == ItemKinds.Exam)
            .GroupBy(i => i.Module.SubjectId)
            .Select(g => new { SubjectId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var teachers = await db.TeachingAssignments.AsNoTracking().Where(a => a.Section.SchoolYearId == yearId)
            .GroupBy(a => a.SubjectId).Select(g => new { SubjectId = g.Key, Count = g.Count() }).ToListAsync(ct);
        var sent = await db.MaterialDeliveries.AsNoTracking()
            .GroupBy(d => d.MaterialModule.SubjectId).Select(g => new { SubjectId = g.Key, Count = g.Count() }).ToListAsync(ct);

        return subjects.Select(s =>
        {
            var m = modules.FirstOrDefault(x => x.SubjectId == s.Id);
            return new MaterialCourseRow(
                s.Id, s.Code, s.Name, (m?.Syllabus ?? 0) > 0, m?.Modules ?? 0, exams.FirstOrDefault(x => x.SubjectId == s.Id)?.Count ?? 0,
                teachers.FirstOrDefault(x => x.SubjectId == s.Id)?.Count ?? 0, sent.FirstOrDefault(x => x.SubjectId == s.Id)?.Count ?? 0,
                placements?.GetValueOrDefault(s.Id));
        }).ToList();
    }

    /// <summary>The terms a strand studies in (1st Term, 2nd Term ...), for the provider to choose from after choosing a strand.</summary>
    public async Task<IReadOnlyList<LibraryTerm>> ListStrandTermsAsync(int strandId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await db.StrandSubjects.AsNoTracking().Where(x => x.StrandId == strandId && x.Subject.IsActive)
            .Select(x => new { x.TermNumber, x.SubjectId }).Distinct().ToListAsync(ct);
        var withSyllabus = (await db.MaterialModules.AsNoTracking().Where(m => m.IsSyllabus).Select(m => m.SubjectId).Distinct().ToListAsync(ct)).ToHashSet();
        var termNames = await db.Terms.AsNoTracking().OrderByDescending(t => t.SchoolYear.StartDate).Select(t => new { t.TermNumber, t.Name }).ToListAsync(ct);
        string NameOf(int number) => termNames.FirstOrDefault(t => t.TermNumber == number)?.Name ?? $"Term {number}";

        // The terms the school has, even one the strand has no subject in yet, so the choice is the same for every strand.
        var numbers = rows.Select(r => (int)r.TermNumber).Concat(termNames.Select(t => (int)t.TermNumber)).Distinct().Order().ToList();
        return numbers.Select(n =>
        {
            var subjects = rows.Where(r => r.TermNumber == n).Select(r => r.SubjectId).Distinct().ToList();
            return new LibraryTerm(n, NameOf(n), subjects.Count, subjects.Count(withSyllabus.Contains));
        }).ToList();
    }

    /// <summary>The tracks of the school with their strands, and for each how many courses it studies and how many of those have a syllabus in the library.</summary>
    public async Task<IReadOnlyList<LibraryTrack>> ListTracksAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var tracks = await db.Tracks.AsNoTracking().Where(t => t.IsActive).OrderBy(t => t.Id).Select(t => new { t.Id, t.Code, t.Name }).ToListAsync(ct);
        var strands = await db.Strands.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Id).Select(s => new { s.Id, s.TrackId, s.Code, s.Name }).ToListAsync(ct);
        var curriculum = await db.StrandSubjects.AsNoTracking().Where(x => x.Subject.IsActive).Select(x => new { x.StrandId, x.SubjectId }).Distinct().ToListAsync(ct);
        var withSyllabus = (await db.MaterialModules.AsNoTracking().Where(m => m.IsSyllabus).Select(m => m.SubjectId).Distinct().ToListAsync(ct)).ToHashSet();

        return tracks.Select(t =>
        {
            var own = strands.Where(s => s.TrackId == t.Id).ToList();
            var rows = own.Select(s =>
            {
                var subjects = curriculum.Where(c => c.StrandId == s.Id).Select(c => c.SubjectId).Distinct().ToList();
                return new LibraryStrand(s.Id, s.Code, s.Name, subjects.Count, subjects.Count(withSyllabus.Contains));
            }).ToList();
            var all = own.SelectMany(s => curriculum.Where(c => c.StrandId == s.Id).Select(c => c.SubjectId)).Distinct().ToList();
            return new LibraryTrack(t.Id, t.Code, t.Name, all.Count, all.Count(withSyllabus.Contains), rows);
        }).ToList();
    }

    public async Task<CourseMaterials?> GetCourseAsync(int subjectId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var subject = await db.Subjects.AsNoTracking().Where(s => s.Id == subjectId).Select(s => new { s.Id, s.Code, s.Name }).FirstOrDefaultAsync(ct);
        if (subject is null)
        {
            return null;
        }

        var modules = await db.MaterialModules.AsNoTracking().Where(m => m.SubjectId == subjectId && !m.IsExamSet)
            .OrderBy(m => m.Position).ThenBy(m => m.Id).ToListAsync(ct);
        var moduleIds = modules.Select(m => m.Id).ToList();
        var deliveries = await db.MaterialDeliveries.AsNoTracking().Where(d => moduleIds.Contains(d.MaterialModuleId))
            .Select(d => new { d.MaterialModuleId, d.AssignmentId })
            .ToListAsync(ct);

        var views = modules.Select(m => new MaterialModuleView(
            m.Id, m.IsSyllabus, m.Title, m.Description, m.Body, m.LinkUrl, m.Position,
            deliveries.Count(d => d.MaterialModuleId == m.Id))).ToList();

        // The summative exams, and in which teachers' courses each one is already.
        var examRows = await db.MaterialItems.AsNoTracking().Where(i => i.Kind == ItemKinds.Exam && i.Module.SubjectId == subjectId && i.Module.IsExamSet)
            .OrderBy(i => i.VisibleFromUtc).ThenBy(i => i.Position).ThenBy(i => i.Id)
            .Select(i => new { i.Id, i.Title, i.Instructions, i.MaxScore, i.VisibleFromUtc, i.DueAtUtc, Questions = i.Questions.Count, i.TimeLimitMinutes, i.AntiCheat, i.MaxViolations, i.ShuffleQuestions, i.ShuffleOptions, i.ShowResult })
            .ToListAsync(ct);
        var examIds = examRows.Select(e => e.Id).ToList();
        var examCopies = await db.ModuleItems.AsNoTracking().Where(i => i.MaterialItemId != null && examIds.Contains(i.MaterialItemId.Value))
            .Select(i => new { ExamId = i.MaterialItemId!.Value, i.Module.AssignmentId })
            .ToListAsync(ct);
        var exams = examRows.Select(e => new MaterialExamView(e.Id, e.Title, e.Instructions, e.MaxScore, e.VisibleFromUtc, e.DueAtUtc, examCopies.Count(c => c.ExamId == e.Id),
            e.Questions, e.TimeLimitMinutes, e.AntiCheat, e.MaxViolations, e.ShuffleQuestions, e.ShuffleOptions, e.ShowResult)).ToList();

        var yearId = await CurrentYearIdAsync(db, ct);
        var assignments = await db.TeachingAssignments.AsNoTracking()
            .Where(a => a.SubjectId == subjectId && a.Section.SchoolYearId == yearId)
            .OrderBy(a => a.Teacher.LastName).ThenBy(a => a.Teacher.FirstName).ThenBy(a => a.Section.GradeLevel).ThenBy(a => a.Section.Name)
            .Select(a => new
            {
                a.Id, a.SectionId, TeacherUserId = a.Teacher.UserId, a.Teacher.FirstName, a.Teacher.MiddleName, a.Teacher.LastName, a.Teacher.NameExtension,
                a.Section.GradeLevel, StrandCode = a.Section.Strand.Code, SectionName = a.Section.Name,
            })
            .ToListAsync(ct);

        var teachers = new List<MaterialTeacherRow>();
        foreach (var a in assignments)
        {
            var students = await Roster.StudentIds(db, a.SectionId, subjectId).CountAsync(ct);
            teachers.Add(new MaterialTeacherRow(
                a.Id, a.TeacherUserId, UserDirectory.FullName(a.FirstName, a.MiddleName, a.LastName, a.NameExtension),
                TeachingService.SectionLabel(a.GradeLevel, a.StrandCode, a.SectionName), students,
                deliveries.Where(d => d.AssignmentId == a.Id).Select(d => d.MaterialModuleId).ToList(), examCopies.Count(c => c.AssignmentId == a.Id)));
        }

        return new CourseMaterials(subject.Id, subject.Code, subject.Name, views, exams, teachers);
    }

    public async Task<MaterialOverview> GetOverviewAsync(int recent = 6, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var yearId = await CurrentYearIdAsync(db, ct);

        var taught = await db.TeachingAssignments.AsNoTracking().Where(a => a.Section.SchoolYearId == yearId)
            .GroupBy(a => new { a.SubjectId, a.Subject.Name }).Select(g => new { g.Key.SubjectId, g.Key.Name, Teachers = g.Count() }).ToListAsync(ct);
        var withSyllabus = (await db.MaterialModules.AsNoTracking().Where(m => m.IsSyllabus).Select(m => m.SubjectId).ToListAsync(ct)).ToHashSet();
        var without = taught.Where(c => !withSyllabus.Contains(c.SubjectId)).OrderBy(c => c.Name).Take(8)
            .Select(c => new CourseWithoutSyllabus(c.SubjectId, c.Name, c.Teachers)).ToList();

        var now = clock.GetUtcNow().UtcDateTime;
        var deliveries = await db.MaterialDeliveries.AsNoTracking()
            .Where(d => !d.MaterialModule.IsExamSet)
            .OrderByDescending(d => d.CreatedAtUtc).ThenByDescending(d => d.Id).Take(recent)
            .Select(d => new
            {
                Subject = d.MaterialModule.Subject.Name, Title = d.MaterialModule.Title, d.MaterialModule.IsSyllabus, d.CreatedAtUtc,
                d.Module.VisibleFromUtc, d.Assignment.Teacher.FirstName, d.Assignment.Teacher.MiddleName, d.Assignment.Teacher.LastName, d.Assignment.Teacher.NameExtension,
                d.Assignment.Section.GradeLevel, Strand = d.Assignment.Section.Strand.Code, SectionName = d.Assignment.Section.Name,
            })
            .ToListAsync(ct);

        return new MaterialOverview(
            taught.Count, await db.MaterialModules.CountAsync(m => !m.IsExamSet, ct), await db.MaterialItems.CountAsync(i => i.Kind == ItemKinds.Exam, ct), await db.MaterialDeliveries.CountAsync(d => !d.MaterialModule.IsExamSet, ct),
            await db.MaterialDeliveries.CountAsync(d => !d.MaterialModule.IsExamSet && d.Module.VisibleFromUtc == null, ct), without,
            deliveries.Select(d => new RecentDelivery(
                d.Subject, d.Title, UserDirectory.FullName(d.FirstName, d.MiddleName, d.LastName, d.NameExtension),
                TeachingService.SectionLabel(d.GradeLevel, d.Strand, d.SectionName), d.IsSyllabus, d.VisibleFromUtc is { } v && v <= now, d.CreatedAtUtc)).ToList());
    }

    // ======================================================================= modules and the syllabus

    public async Task<ServiceResult> SaveModuleAsync(int userId, int subjectId, int? moduleId, ModuleInput input, bool isSyllabus, CancellationToken ct = default)
    {
        var errors = ModuleService.ValidateModule(input);
        if (errors.Count > 0)
        {
            return ServiceResult.Fail(errors);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsProviderAsync(db, userId, ct))
        {
            return ServiceResult.Fail(ProviderOnly);
        }

        if (!await db.Subjects.AnyAsync(s => s.Id == subjectId, ct))
        {
            return ServiceResult.Fail("This course no longer exists.");
        }

        MaterialModule module;
        if (moduleId is { } id)
        {
            var found = await db.MaterialModules.FirstOrDefaultAsync(m => m.Id == id && m.SubjectId == subjectId && !m.IsExamSet, ct);
            if (found is null)
            {
                return ServiceResult.Fail("This module was not found.");
            }

            module = found;
        }
        else
        {
            if (isSyllabus && await db.MaterialModules.AnyAsync(m => m.SubjectId == subjectId && m.IsSyllabus, ct))
            {
                return ServiceResult.Fail("This course has a syllabus already. Edit it instead.");
            }

            var position = isSyllabus ? 0 : (await db.MaterialModules.Where(m => m.SubjectId == subjectId && !m.IsSyllabus && !m.IsExamSet).MaxAsync(m => (int?)m.Position, ct) ?? 0) + 1;
            module = new MaterialModule { SubjectId = subjectId, IsSyllabus = isSyllabus, Position = position, CreatedByUserId = userId };
            db.MaterialModules.Add(module);
        }

        module.Title = input.Title.Trim();
        module.Description = ModuleService.Clean(input.Description);
        module.Body = ModuleService.Clean(input.Body);
        module.LinkUrl = ModuleService.Clean(input.LinkUrl);
        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok(moduleId is null ? (isSyllabus ? "The syllabus was added." : "The module was added.") : "Saved.", module.Id);
    }

    public async Task<ServiceResult> DeleteModuleAsync(int userId, int moduleId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsProviderAsync(db, userId, ct))
        {
            return ServiceResult.Fail(ProviderOnly);
        }

        var deleted = await db.MaterialModules.Where(m => m.Id == moduleId && !m.IsExamSet).ExecuteDeleteAsync(ct);
        return deleted == 0
            ? ServiceResult.Fail("This module was already deleted.")
            : ServiceResult.Ok("Deleted from the library. The copies the teachers already got stay with them.");
    }

    // ======================================================================= summative exams

    /// <summary>The problems of an exam: a title, a highest score, and a schedule with both ends (it closes after it opens).</summary>
    public static List<string> ValidateExam(ExamInput input)
    {
        var errors = new List<string>();
        var title = (input.Title ?? "").Trim();
        if (title.Length is 0 or > 200)
        {
            errors.Add("Enter the title of the exam (up to 200 characters).");
        }

        if (input.Questions is not { Count: > 0 } && input.MaxScore is <= 0 or > 1000)
        {
            errors.Add("The highest score must be between 1 and 1000.");
        }

        if (input.Questions is { Count: > 0 } questions)
        {
            errors.AddRange(ModuleService.ValidateQuestions(questions));
        }

        if (input.TimeLimitMinutes is { } limit && (limit < 1 || limit > 600))
        {
            errors.Add("The time limit must be between 1 and 600 minutes.");
        }

        if (input.MaxViolations is < 1 or > 10)
        {
            errors.Add("The number of warnings before the exam is handed in must be between 1 and 10.");
        }

        if ((input.Instructions ?? "").Length > 5000)
        {
            errors.Add("The instructions can have at most 5000 characters.");
        }

        if (input.OpensAtUtc is null || input.DueAtUtc is null)
        {
            errors.Add("Set the schedule: when the exam opens and when it closes.");
        }
        else if (input.DueAtUtc <= input.OpensAtUtc)
        {
            errors.Add("The exam must close after it opens.");
        }

        return errors;
    }

    /// <summary>Adds a summative exam to the library of a course, or changes one. The exams of a course live in a box (a library module that is never listed as a module).</summary>
    public async Task<ServiceResult> SaveExamAsync(int userId, int subjectId, int? examId, ExamInput input, CancellationToken ct = default)
    {
        var errors = ValidateExam(input);
        if (errors.Count > 0)
        {
            return ServiceResult.Fail(errors);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsProviderAsync(db, userId, ct))
        {
            return ServiceResult.Fail(ProviderOnly);
        }

        if (!await db.Subjects.AnyAsync(s => s.Id == subjectId, ct))
        {
            return ServiceResult.Fail("This course no longer exists.");
        }

        MaterialItem item;
        var isNew = examId is null;
        if (examId is { } id)
        {
            var found = await db.MaterialItems.FirstOrDefaultAsync(i => i.Id == id && i.Kind == ItemKinds.Exam && i.Module.SubjectId == subjectId && i.Module.IsExamSet, ct);
            if (found is null)
            {
                return ServiceResult.Fail("This exam was not found.");
            }

            item = found;
        }
        else
        {
            var box = await db.MaterialModules.FirstOrDefaultAsync(m => m.SubjectId == subjectId && m.IsExamSet, ct);
            if (box is null)
            {
                box = new MaterialModule { SubjectId = subjectId, IsExamSet = true, Title = "Summative exams", Position = int.MaxValue, CreatedByUserId = userId };
                db.MaterialModules.Add(box);
                await db.SaveChangesAsync(ct);
            }

            var position = (await db.MaterialItems.Where(i => i.ModuleId == box.Id).MaxAsync(i => (int?)i.Position, ct) ?? 0) + 1;
            item = new MaterialItem { ModuleId = box.Id, Kind = ItemKinds.Exam, Position = position };
            db.MaterialItems.Add(item);
        }

        item.Title = input.Title.Trim();
        item.Instructions = ModuleService.Clean(input.Instructions);
        item.MaxScore = input.MaxScore;
        item.VisibleFromUtc = input.OpensAtUtc;
        item.DueAtUtc = input.DueAtUtc;
        item.TimeLimitMinutes = input.TimeLimitMinutes;
        item.AntiCheat = input.AntiCheat;
        item.MaxViolations = input.MaxViolations;
        item.ShuffleQuestions = input.ShuffleQuestions;
        item.ShuffleOptions = input.ShuffleOptions;
        item.ShowResult = input.ShowResult;
        await db.SaveChangesAsync(ct);

        // New questions replace the old ones; the exam is then worth the total of their points.
        if (input.Questions is { } questions)
        {
            await db.MaterialQuestions.Where(q => q.ItemId == item.Id).ExecuteDeleteAsync(ct);
            var position = 0;
            foreach (var q in questions)
            {
                var options = q.Kind == QuestionKinds.MultipleChoice ? ModuleService.SplitOptions(q.OptionsText) : [];
                db.MaterialQuestions.Add(new MaterialQuestion
                {
                    ItemId = item.Id, Position = ++position, Text = q.Text.Trim(), Kind = q.Kind,
                    OptionsText = q.Kind == QuestionKinds.MultipleChoice ? string.Join('\n', options) : null,
                    CorrectAnswer = q.Kind == QuestionKinds.MultipleChoice
                        ? options.First(o => string.Equals(o, q.CorrectAnswer.Trim(), StringComparison.OrdinalIgnoreCase))
                        : q.CorrectAnswer.Trim(),
                    Points = q.Points,
                });
            }

            if (questions.Count > 0)
            {
                item.MaxScore = questions.Sum(q => q.Points);
            }

            await db.SaveChangesAsync(ct);
        }
        else if (!isNew && await db.MaterialQuestions.Where(q => q.ItemId == item.Id).SumAsync(q => (decimal?)q.Points, ct) is { } total && total > 0)
        {
            // The questions stay as they are, and the exam stays worth the total of their points.
            item.MaxScore = total;
            await db.SaveChangesAsync(ct);
        }

        return ServiceResult.Ok(isNew ? "The exam was added." : "Saved.", item.Id);
    }

    /// <summary>The questions of a summative exam of the library (for the export and for the edit box).</summary>
    public async Task<IReadOnlyList<QuestionInput>> GetExamQuestionsAsync(int examId, CancellationToken ct = default) =>
        (await GetExamQuestionsAsync([examId], ct)).GetValueOrDefault(examId) ?? [];

    /// <summary>The questions of several exams at once, by exam.</summary>
    public async Task<IReadOnlyDictionary<int, IReadOnlyList<QuestionInput>>> GetExamQuestionsAsync(IReadOnlyCollection<int> examIds, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await db.MaterialQuestions.AsNoTracking().Where(q => examIds.Contains(q.ItemId)).OrderBy(q => q.Position).ThenBy(q => q.Id)
            .Select(q => new { q.ItemId, Question = new QuestionInput { Text = q.Text, Kind = q.Kind, OptionsText = q.OptionsText, CorrectAnswer = q.CorrectAnswer, Points = q.Points } })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.ItemId).ToDictionary(g => g.Key, g => (IReadOnlyList<QuestionInput>)[.. g.Select(r => r.Question)]);
    }

    public async Task<ServiceResult> DeleteExamAsync(int userId, int examId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsProviderAsync(db, userId, ct))
        {
            return ServiceResult.Fail(ProviderOnly);
        }

        var deleted = await db.MaterialItems.Where(i => i.Id == examId && i.Kind == ItemKinds.Exam).ExecuteDeleteAsync(ct);
        return deleted == 0 ? ServiceResult.Fail("This exam was already deleted.") : ServiceResult.Ok("Deleted from the library. The copies the teachers already got stay with them.");
    }

    // ======================================================================= sending

    /// <summary>
    /// Sends the syllabus and the modules of the course to the courses of some teachers. Each teacher's course gets a copy of each module (its title, its link and its files) as a
    /// draft that is not posted: the teacher decides when to post it. A module that was sent to that course before is skipped. Each teacher is told once.
    /// </summary>
    public async Task<ServiceResult> SendAsync(int userId, int subjectId, IReadOnlyCollection<int> moduleIds, IReadOnlyCollection<int> assignmentIds, CancellationToken ct = default)
    {
        if (moduleIds.Count == 0)
        {
            return ServiceResult.Fail("Choose what to send.");
        }

        if (assignmentIds.Count == 0)
        {
            return ServiceResult.Fail("Choose the teachers to send it to.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsProviderAsync(db, userId, ct))
        {
            return ServiceResult.Fail(ProviderOnly);
        }

        var subject = await db.Subjects.AsNoTracking().Where(s => s.Id == subjectId).Select(s => s.Name).FirstOrDefaultAsync(ct);
        if (subject is null)
        {
            return ServiceResult.Fail("This course no longer exists.");
        }

        var library = await db.MaterialModules.AsNoTracking()
            .Where(m => m.SubjectId == subjectId && !m.IsExamSet && moduleIds.Contains(m.Id))
            .Include(m => m.Attachments).ThenInclude(a => a.Content)
            .AsSplitQuery()
            .OrderBy(m => m.Position).ThenBy(m => m.Id)
            .ToListAsync(ct);
        if (library.Count == 0)
        {
            return ServiceResult.Fail("What you chose is not in the library of this course any more.");
        }

        var yearId = await CurrentYearIdAsync(db, ct);
        var targets = await db.TeachingAssignments.AsNoTracking()
            .Where(a => a.SubjectId == subjectId && assignmentIds.Contains(a.Id) && a.Section.SchoolYearId == yearId)
            .Select(a => new { a.Id, TeacherUserId = a.Teacher.UserId, SectionName = a.Section.Name, a.Section.GradeLevel, StrandCode = a.Section.Strand.Code })
            .ToListAsync(ct);
        if (targets.Count == 0)
        {
            return ServiceResult.Fail("None of those courses of teachers exists any more. Reload and look again.");
        }

        var copied = 0;
        var skipped = 0;
        var told = new List<(int UserId, int AssignmentId, string Section, List<string> Titles)>();

        foreach (var target in targets)
        {
            var already = (await db.MaterialDeliveries.AsNoTracking().Where(d => d.AssignmentId == target.Id).Select(d => d.MaterialModuleId).ToListAsync(ct)).ToHashSet();
            var nextPosition = (await db.Modules.Where(m => m.AssignmentId == target.Id).MaxAsync(m => (int?)m.Position, ct) ?? 0) + 1;
            var titles = new List<string>();

            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            foreach (var source in library)
            {
                if (already.Contains(source.Id))
                {
                    skipped++;
                    continue;
                }

                var module = new LearningModule
                {
                    AssignmentId = target.Id, Title = source.Title, Description = source.Description, Body = source.Body, LinkUrl = source.LinkUrl,
                    IsSyllabus = source.IsSyllabus, Position = source.IsSyllabus ? 0 : nextPosition++, VisibleFromUtc = null,
                };
                module.Attachments.AddRange(source.Attachments.Select(a => CopyOf(a, userId)));

                db.Modules.Add(module);
                db.MaterialDeliveries.Add(new MaterialDelivery { MaterialModuleId = source.Id, AssignmentId = target.Id, Module = module, SentByUserId = userId });
                titles.Add(source.Title);
                copied++;
            }

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            db.ChangeTracker.Clear();

            if (titles.Count > 0)
            {
                told.Add((target.TeacherUserId, target.Id, TeachingService.SectionLabel(target.GradeLevel, target.StrandCode, target.SectionName), titles));
            }
        }

        foreach (var teacher in told)
        {
            var list = string.Join(", ", teacher.Titles);
            await notifications.AddAsync(
                teacher.UserId, "materials", "Course materials from the academic provider",
                $"{subject} · {teacher.Section}: {list}. They are drafts: set when to post them.",
                $"teacher/subjects/{teacher.AssignmentId}", ct);
        }

        var teachersTold = told.Select(t => t.UserId).Distinct().Count();
        if (copied == 0)
        {
            return ServiceResult.Ok("Nothing new to send: the teachers you chose already have those.", 0);
        }

        return ServiceResult.Ok(
            $"Sent {Plural(library.Count, "module")} to {Plural(teachersTold, "teacher")}"
            + (skipped > 0 ? $" ({skipped} already sent before were skipped)." : ". They are drafts in the teachers' courses."), copied);
    }

    /// <summary>
    /// Posts summative exams straight to the students of some courses (a section and its teacher). Nothing waits for the teacher: each course gets a copy of the exam in its
    /// "Summative exams" box (posted at once, not listed as a module) with the questions and the settings of the exam, and it opens and closes at the times the provider chose.
    /// The students are told, the teacher too. The score of each student goes to the gradebook of the teacher by itself. An exam that a course got before is skipped.
    /// </summary>
    public async Task<ServiceResult> PostExamsAsync(int userId, int subjectId, IReadOnlyCollection<int> examIds, IReadOnlyCollection<int> assignmentIds, CancellationToken ct = default)
    {
        if (examIds.Count == 0)
        {
            return ServiceResult.Fail("Choose the exams to post.");
        }

        if (assignmentIds.Count == 0)
        {
            return ServiceResult.Fail("Choose the sections that get it.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsProviderAsync(db, userId, ct))
        {
            return ServiceResult.Fail(ProviderOnly);
        }

        var subject = await db.Subjects.AsNoTracking().Where(s => s.Id == subjectId).Select(s => s.Name).FirstOrDefaultAsync(ct);
        if (subject is null)
        {
            return ServiceResult.Fail("This course no longer exists.");
        }

        var exams = await db.MaterialItems.AsNoTracking()
            .Where(i => i.Kind == ItemKinds.Exam && i.Module.SubjectId == subjectId && i.Module.IsExamSet && examIds.Contains(i.Id))
            .Include(i => i.Attachments).ThenInclude(a => a.Content)
            .Include(i => i.Questions)
            .AsSplitQuery()
            .OrderBy(i => i.VisibleFromUtc).ThenBy(i => i.Position).ThenBy(i => i.Id)
            .ToListAsync(ct);
        if (exams.Count == 0)
        {
            return ServiceResult.Fail("What you chose is not in the library of this course any more.");
        }

        var yearId = await CurrentYearIdAsync(db, ct);
        var targets = await db.TeachingAssignments.AsNoTracking()
            .Where(a => a.SubjectId == subjectId && assignmentIds.Contains(a.Id) && a.Section.SchoolYearId == yearId)
            .Select(a => new { a.Id, a.SectionId, TeacherUserId = a.Teacher.UserId, SectionName = a.Section.Name, a.Section.GradeLevel, StrandCode = a.Section.Strand.Code })
            .ToListAsync(ct);
        if (targets.Count == 0)
        {
            return ServiceResult.Fail("None of those sections has this course any more. Reload and look again.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var posted = 0;
        var skipped = 0;
        var students = new HashSet<int>();
        foreach (var target in targets)
        {
            var sentBefore = (await db.ModuleItems.AsNoTracking()
                .Where(i => i.Module.AssignmentId == target.Id && i.MaterialItemId != null)
                .Select(i => i.MaterialItemId!.Value).ToListAsync(ct)).ToHashSet();
            var fresh = exams.Where(e => !sentBefore.Contains(e.Id)).ToList();
            skipped += exams.Count - fresh.Count;
            if (fresh.Count == 0)
            {
                continue;
            }

            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            // The box that holds the exams of this course: made the first time, posted at once, and used again after.
            var host = await db.Modules.FirstOrDefaultAsync(m => m.AssignmentId == target.Id && m.IsExamSet, ct);
            if (host is null)
            {
                var position = (await db.Modules.Where(m => m.AssignmentId == target.Id).MaxAsync(m => (int?)m.Position, ct) ?? 0) + 1;
                host = new LearningModule
                {
                    AssignmentId = target.Id, Title = "Summative exams", IsExamSet = true, Position = position, VisibleFromUtc = now, NotifiedAtUtc = now,
                };
                db.Modules.Add(host);
                await db.SaveChangesAsync(ct);
            }

            var itemPosition = await db.ModuleItems.Where(i => i.ModuleId == host.Id).MaxAsync(i => (int?)i.Position, ct) ?? 0;
            foreach (var exam in fresh)
            {
                var copy = new ModuleItem
                {
                    ModuleId = host.Id, Kind = ItemKinds.Exam, Title = exam.Title, Instructions = exam.Instructions, MaxScore = exam.MaxScore,
                    VisibleFromUtc = exam.VisibleFromUtc, DueAtUtc = exam.DueAtUtc, Position = ++itemPosition, MaterialItemId = exam.Id,
                    TimeLimitMinutes = exam.TimeLimitMinutes, ShuffleQuestions = exam.ShuffleQuestions, ShuffleOptions = exam.ShuffleOptions,
                    AntiCheat = exam.AntiCheat, MaxViolations = exam.MaxViolations, ShowResult = exam.ShowResult,
                };
                copy.Attachments.AddRange(exam.Attachments.Select(a => CopyOf(a, userId)));
                copy.Questions.AddRange(exam.Questions.OrderBy(q => q.Position).ThenBy(q => q.Id).Select((q, n) => new QuizQuestion
                {
                    Position = n + 1, Text = q.Text, Kind = q.Kind, OptionsText = q.OptionsText, CorrectAnswer = q.CorrectAnswer, Points = q.Points,
                }));
                db.ModuleItems.Add(copy);
                posted++;
            }

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            var studentUserIds = await Roster.Enrollments(db, target.SectionId, subjectId).AsNoTracking().Where(e => e.Student.User.IsActive).Select(e => e.Student.UserId).ToListAsync(ct);
            var section = TeachingService.SectionLabel(target.GradeLevel, target.StrandCode, target.SectionName);
            var list = string.Join(", ", fresh.Select(e => $"{e.Title} (opens {PhilippineTime.FormatDateTime(e.VisibleFromUtc ?? now)})"));
            foreach (var studentUserId in studentUserIds)
            {
                students.Add(studentUserId);
                await notifications.AddAsync(studentUserId, "materials", "Summative exam posted", $"{subject}: {list}.", $"student/subjects/{target.Id}?tab=exams", ct);
            }

            await notifications.AddAsync(target.TeacherUserId, "materials", "Summative exam posted to your students",
                $"{subject} · {section}: {list}. The scores go to your gradebook by themselves.", $"teacher/subjects/{target.Id}?tab=exams", ct);
            db.ChangeTracker.Clear();
        }

        if (posted == 0)
        {
            return ServiceResult.Ok("Nothing new to post: those sections already have the exams.", 0);
        }

        return ServiceResult.Ok(
            $"Posted {Plural(exams.Count, "exam")} to {Plural(students.Count, "student")}"
            + (skipped > 0 ? $" ({skipped} already posted before were skipped)." : ". They open at the times you set."), posted);
    }

    // ======================================================================= helpers

    /// <summary>A copy of a file of the library for the teacher's draft (the teacher can take it off without touching the library).</summary>
    private static Attachment CopyOf(Attachment source, int userId) => new()
    {
        Name = source.Name, ContentType = source.ContentType, SizeBytes = source.SizeBytes, UploadedByUserId = userId, IsSlides = source.IsSlides,
        Content = new AttachmentContent { Data = source.Content?.Data ?? [] },
    };

    private static async Task<bool> IsProviderAsync(AppDbContext db, int userId, CancellationToken ct) =>
        await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.IsActive && u.RoleId == AppRoles.AcademicProviderId, ct);

    /// <summary>The school year of the current term, or the latest school year when no term is current.</summary>
    internal static async Task<int?> CurrentYearIdAsync(AppDbContext db, CancellationToken ct) =>
        await db.Terms.AsNoTracking().Where(t => t.IsCurrent).Select(t => (int?)t.SchoolYearId).FirstOrDefaultAsync(ct)
        ?? await db.SchoolYears.AsNoTracking().OrderByDescending(y => y.StartDate).ThenByDescending(y => y.Id).Select(y => (int?)y.Id).FirstOrDefaultAsync(ct);

    private static string Plural(int count, string one) => count == 1 ? $"1 {one}" : $"{count} {one}s";
}
