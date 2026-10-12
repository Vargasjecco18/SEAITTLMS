using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>A course in the list of the course materials, with what the provider has prepared for it.</summary>
public sealed record MaterialCourseRow(
    int SubjectId, string Code, string Name, bool HasSyllabus, int Modules, int Activities, int Quizzes, int TaskPerformances, int Teachers, int Sent);

public sealed record MaterialItemView(
    int Id, string Kind, string Title, string? Instructions, decimal MaxScore, DateTime? DueAtUtc, int? TimeLimitMinutes, bool ShuffleQuestions,
    bool ShuffleOptions, bool AntiCheat, int MaxViolations, bool ShowResult, int Questions);

/// <param name="SentTo">To how many teachers' courses it was sent.</param>
public sealed record MaterialModuleView(
    int Id, bool IsSyllabus, string Title, string? Description, string? Body, string? LinkUrl, int Position, IReadOnlyList<MaterialItemView> Items, int SentTo);

/// <summary>One teacher's course (a teacher, a section) that the materials of the course can be sent to, and which modules it got.</summary>
public sealed record MaterialTeacherRow(
    int AssignmentId, int TeacherUserId, string TeacherName, string SectionLabel, string? Schedule, int Students, IReadOnlyList<int> Received);

public sealed record CourseWithoutSyllabus(int SubjectId, string Name, int Teachers);

public sealed record RecentDelivery(string SubjectName, string ModuleTitle, string TeacherName, string SectionLabel, bool IsSyllabus, bool Posted, DateTime SentAtUtc);

/// <summary>What the provider's dashboard shows about the course materials.</summary>
/// <param name="CoursesTaught">Courses that have a teacher this school year.</param>
/// <param name="StillDrafts">Modules that were sent and that the teachers have not posted yet.</param>
public sealed record MaterialOverview(
    int CoursesTaught, int LibraryModules, int LibraryItems, int Sent, int StillDrafts, IReadOnlyList<CourseWithoutSyllabus> WithoutSyllabus,
    IReadOnlyList<RecentDelivery> Recent);

/// <summary>An activity, quiz or task performance of the library and where it belongs, for the question editor.</summary>
public sealed record MaterialItemInfo(int ItemId, string Kind, string Title, int ModuleId, string ModuleTitle, int SubjectId, string SubjectName);

public sealed record CourseMaterials(
    int SubjectId, string Code, string Name, IReadOnlyList<MaterialModuleView> Modules, IReadOnlyList<MaterialTeacherRow> Teachers);

/// <param name="Copied">How many modules were put in teachers' courses (a module sent to three teachers counts three times).</param>
/// <param name="Teachers">How many teachers got something new.</param>
/// <param name="Skipped">How many modules had been sent to that teacher's course before.</param>
public sealed record SendOutcome(int Copied, int Teachers, int Skipped);

/// <summary>
/// The course materials of the academic provider: a library per course (a syllabus, modules and the activities, quizzes and task
/// performances in them) that is sent to the teachers who teach the course. A teacher gets their own copy as a draft: students
/// can't see it until the teacher posts it, and the teacher can change everything (when it is posted, the due dates, the text).
/// Every change checks again, in the database, that the person is an active academic provider.
/// </summary>
public sealed class MaterialService(IDbContextFactory<AppDbContext> dbFactory, NotificationService notifications, TimeProvider clock)
{
    private const string ProviderOnly = "Only the academic provider prepares course materials.";

    // ======================================================================= reading

    /// <summary>Every active course with what is prepared for it. <paramref name="search"/> looks in the name and the code.</summary>
    public async Task<IReadOnlyList<MaterialCourseRow>> ListCoursesAsync(string? search = null, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var yearId = await CurrentYearIdAsync(db, ct);

        var query = db.Subjects.AsNoTracking().Where(s => s.IsActive);
        foreach (var token in (search ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var t = token;
            query = query.Where(s => s.Name.Contains(t) || s.Code.Contains(t));
        }

        var subjects = await query.OrderBy(s => s.Name).Select(s => new { s.Id, s.Code, s.Name }).ToListAsync(ct);

        var modules = await db.MaterialModules.AsNoTracking()
            .GroupBy(m => m.SubjectId)
            .Select(g => new { SubjectId = g.Key, Syllabus = g.Count(m => m.IsSyllabus), Modules = g.Count(m => !m.IsSyllabus) })
            .ToListAsync(ct);
        var items = await db.MaterialItems.AsNoTracking()
            .GroupBy(i => new { i.Module.SubjectId, i.Kind })
            .Select(g => new { g.Key.SubjectId, g.Key.Kind, Count = g.Count() })
            .ToListAsync(ct);
        var teachers = await db.TeachingAssignments.AsNoTracking().Where(a => a.Section.SchoolYearId == yearId)
            .GroupBy(a => a.SubjectId).Select(g => new { SubjectId = g.Key, Count = g.Count() }).ToListAsync(ct);
        var sent = await db.MaterialDeliveries.AsNoTracking()
            .GroupBy(d => d.MaterialModule.SubjectId).Select(g => new { SubjectId = g.Key, Count = g.Count() }).ToListAsync(ct);

        return subjects.Select(s =>
        {
            var m = modules.FirstOrDefault(x => x.SubjectId == s.Id);
            int Items(string kind) => items.FirstOrDefault(x => x.SubjectId == s.Id && x.Kind == kind)?.Count ?? 0;
            return new MaterialCourseRow(
                s.Id, s.Code, s.Name, (m?.Syllabus ?? 0) > 0, m?.Modules ?? 0, Items(ItemKinds.Activity), Items(ItemKinds.Quiz), Items(ItemKinds.TaskPerformance),
                teachers.FirstOrDefault(x => x.SubjectId == s.Id)?.Count ?? 0, sent.FirstOrDefault(x => x.SubjectId == s.Id)?.Count ?? 0);
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

        var modules = await db.MaterialModules.AsNoTracking().Where(m => m.SubjectId == subjectId)
            .OrderBy(m => m.Position).ThenBy(m => m.Id).ToListAsync(ct);
        var moduleIds = modules.Select(m => m.Id).ToList();
        var items = await db.MaterialItems.AsNoTracking().Where(i => moduleIds.Contains(i.ModuleId))
            .OrderBy(i => i.Position).ThenBy(i => i.Id)
            .Select(i => new { Item = i, Questions = i.Questions.Count })
            .ToListAsync(ct);
        var deliveries = await db.MaterialDeliveries.AsNoTracking().Where(d => moduleIds.Contains(d.MaterialModuleId))
            .Select(d => new { d.MaterialModuleId, d.AssignmentId })
            .ToListAsync(ct);

        var views = modules.Select(m => new MaterialModuleView(
            m.Id, m.IsSyllabus, m.Title, m.Description, m.Body, m.LinkUrl, m.Position,
            items.Where(x => x.Item.ModuleId == m.Id).Select(x => ToView(x.Item, x.Questions)).ToList(),
            deliveries.Count(d => d.MaterialModuleId == m.Id))).ToList();

        var yearId = await CurrentYearIdAsync(db, ct);
        var assignments = await db.TeachingAssignments.AsNoTracking()
            .Where(a => a.SubjectId == subjectId && a.Section.SchoolYearId == yearId)
            .OrderBy(a => a.Teacher.LastName).ThenBy(a => a.Teacher.FirstName).ThenBy(a => a.Section.GradeLevel).ThenBy(a => a.Section.Name)
            .Select(a => new
            {
                a.Id, a.SectionId, TeacherUserId = a.Teacher.UserId, a.Teacher.FirstName, a.Teacher.MiddleName, a.Teacher.LastName, a.Teacher.NameExtension,
                a.Section.GradeLevel, StrandCode = a.Section.Strand.Code, SectionName = a.Section.Name,
                Schedule = a.Section.Schedule != null ? a.Section.Schedule.Code : null,
            })
            .ToListAsync(ct);

        var teachers = new List<MaterialTeacherRow>();
        foreach (var a in assignments)
        {
            var students = await Roster.StudentIds(db, a.SectionId, subjectId).CountAsync(ct);
            teachers.Add(new MaterialTeacherRow(
                a.Id, a.TeacherUserId, UserDirectory.FullName(a.FirstName, a.MiddleName, a.LastName, a.NameExtension),
                TeachingService.SectionLabel(a.GradeLevel, a.StrandCode, a.SectionName), a.Schedule, students,
                deliveries.Where(d => d.AssignmentId == a.Id).Select(d => d.MaterialModuleId).ToList()));
        }

        return new CourseMaterials(subject.Id, subject.Code, subject.Name, views, teachers);
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
            .OrderByDescending(d => d.CreatedAtUtc).ThenByDescending(d => d.Id).Take(recent)
            .Select(d => new
            {
                Subject = d.MaterialModule.Subject.Name, Title = d.MaterialModule.Title, d.MaterialModule.IsSyllabus, d.CreatedAtUtc,
                d.Module.VisibleFromUtc, d.Assignment.Teacher.FirstName, d.Assignment.Teacher.MiddleName, d.Assignment.Teacher.LastName, d.Assignment.Teacher.NameExtension,
                d.Assignment.Section.GradeLevel, Strand = d.Assignment.Section.Strand.Code, SectionName = d.Assignment.Section.Name,
            })
            .ToListAsync(ct);

        return new MaterialOverview(
            taught.Count, await db.MaterialModules.CountAsync(ct), await db.MaterialItems.CountAsync(ct), await db.MaterialDeliveries.CountAsync(ct),
            await db.MaterialDeliveries.CountAsync(d => d.Module.VisibleFromUtc == null, ct), without,
            deliveries.Select(d => new RecentDelivery(
                d.Subject, d.Title, UserDirectory.FullName(d.FirstName, d.MiddleName, d.LastName, d.NameExtension),
                TeachingService.SectionLabel(d.GradeLevel, d.Strand, d.SectionName), d.IsSyllabus, d.VisibleFromUtc is { } v && v <= now, d.CreatedAtUtc)).ToList());
    }

    public async Task<MaterialItemInfo?> GetItemAsync(int itemId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.MaterialItems.AsNoTracking().Where(i => i.Id == itemId)
            .Select(i => new MaterialItemInfo(i.Id, i.Kind, i.Title, i.ModuleId, i.Module.Title, i.Module.SubjectId, i.Module.Subject.Name))
            .FirstOrDefaultAsync(ct);
    }

    private static MaterialItemView ToView(MaterialItem i, int questions) => new(
        i.Id, i.Kind, i.Title, i.Instructions, i.MaxScore, i.DueAtUtc, i.TimeLimitMinutes, i.ShuffleQuestions, i.ShuffleOptions, i.AntiCheat,
        i.MaxViolations, i.ShowResult, questions);

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
            var found = await db.MaterialModules.FirstOrDefaultAsync(m => m.Id == id && m.SubjectId == subjectId, ct);
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

            var position = isSyllabus ? 0 : (await db.MaterialModules.Where(m => m.SubjectId == subjectId && !m.IsSyllabus).MaxAsync(m => (int?)m.Position, ct) ?? 0) + 1;
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

        var deleted = await db.MaterialModules.Where(m => m.Id == moduleId).ExecuteDeleteAsync(ct);
        return deleted == 0
            ? ServiceResult.Fail("This module was already deleted.")
            : ServiceResult.Ok("Deleted from the library. The copies the teachers already got stay with them.");
    }

    // ======================================================================= activities, quizzes, task performances

    public async Task<ServiceResult> SaveItemAsync(int userId, int moduleId, int? itemId, ItemInput input, CancellationToken ct = default)
    {
        input.VisibleFromUtc = null;
        var errors = ModuleService.ValidateItem(input);
        if (errors.Count > 0)
        {
            return ServiceResult.Fail(errors);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsProviderAsync(db, userId, ct))
        {
            return ServiceResult.Fail(ProviderOnly);
        }

        var module = await db.MaterialModules.AsNoTracking().Where(m => m.Id == moduleId).Select(m => new { m.Id, m.IsSyllabus }).FirstOrDefaultAsync(ct);
        if (module is null)
        {
            return ServiceResult.Fail("This module was not found.");
        }

        if (module.IsSyllabus)
        {
            return ServiceResult.Fail("A syllabus has no activities. Add them to a module.");
        }

        MaterialItem item;
        var isNew = itemId is null;
        if (isNew)
        {
            var position = (await db.MaterialItems.Where(i => i.ModuleId == moduleId).MaxAsync(i => (int?)i.Position, ct) ?? 0) + 1;
            item = new MaterialItem { ModuleId = moduleId, Position = position };
            db.MaterialItems.Add(item);
        }
        else
        {
            var existing = await db.MaterialItems.FirstOrDefaultAsync(i => i.Id == itemId && i.ModuleId == moduleId, ct);
            if (existing is null)
            {
                return ServiceResult.Fail("This item was not found.");
            }

            item = existing;
        }

        item.Kind = input.Kind;
        item.Title = input.Title.Trim();
        item.Instructions = ModuleService.Clean(input.Instructions);
        item.MaxScore = input.MaxScore <= 0 ? 100 : input.MaxScore;
        item.DueAtUtc = input.DueAtUtc;
        item.TimeLimitMinutes = input.Kind == ItemKinds.Quiz ? input.TimeLimitMinutes : null;
        item.ShuffleQuestions = input.ShuffleQuestions;
        item.ShuffleOptions = input.ShuffleOptions;
        item.AntiCheat = input.AntiCheat;
        item.MaxViolations = input.MaxViolations;
        item.ShowResult = input.ShowResult;
        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok(isNew ? $"{ItemKinds.Display(item.Kind)} added." : "Saved.", item.Id);
    }

    public async Task<ServiceResult> DeleteItemAsync(int userId, int itemId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsProviderAsync(db, userId, ct))
        {
            return ServiceResult.Fail(ProviderOnly);
        }

        var deleted = await db.MaterialItems.Where(i => i.Id == itemId).ExecuteDeleteAsync(ct);
        return deleted == 0 ? ServiceResult.Fail("This item was already deleted.") : ServiceResult.Ok("Deleted from the library.");
    }

    public async Task<IReadOnlyList<QuestionInput>?> GetQuestionsAsync(int itemId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await db.MaterialItems.AnyAsync(i => i.Id == itemId && i.Kind == ItemKinds.Quiz, ct))
        {
            return null;
        }

        return await db.MaterialQuestions.AsNoTracking().Where(q => q.ItemId == itemId).OrderBy(q => q.Position).ThenBy(q => q.Id)
            .Select(q => new QuestionInput { Text = q.Text, Kind = q.Kind, OptionsText = q.OptionsText, CorrectAnswer = q.CorrectAnswer, Points = q.Points })
            .ToListAsync(ct);
    }

    /// <summary>Replaces the questions of a quiz of the library. The score of the quiz becomes the total of the points.</summary>
    public async Task<ServiceResult> SaveQuestionsAsync(int userId, int itemId, IReadOnlyList<QuestionInput> questions, CancellationToken ct = default)
    {
        var errors = ModuleService.ValidateQuestions(questions);
        if (errors.Count > 0)
        {
            return ServiceResult.Fail(errors);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsProviderAsync(db, userId, ct))
        {
            return ServiceResult.Fail(ProviderOnly);
        }

        var item = await db.MaterialItems.FirstOrDefaultAsync(i => i.Id == itemId && i.Kind == ItemKinds.Quiz, ct);
        if (item is null)
        {
            return ServiceResult.Fail("This quiz was not found.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.MaterialQuestions.Where(q => q.ItemId == itemId).ExecuteDeleteAsync(ct);
        var position = 0;
        foreach (var q in questions)
        {
            var options = ModuleService.SplitOptions(q.OptionsText);
            db.MaterialQuestions.Add(new MaterialQuestion
            {
                ItemId = itemId, Position = ++position, Text = q.Text.Trim(), Kind = q.Kind,
                OptionsText = q.Kind == QuestionKinds.MultipleChoice ? string.Join('\n', options) : null,
                CorrectAnswer = q.Kind == QuestionKinds.MultipleChoice
                    ? options.First(o => string.Equals(o, q.CorrectAnswer.Trim(), StringComparison.OrdinalIgnoreCase))
                    : q.CorrectAnswer.Trim(),
                Points = q.Points,
            });
        }

        item.MaxScore = questions.Count == 0 ? 100 : questions.Sum(q => q.Points);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ServiceResult.Ok($"{questions.Count} question{(questions.Count == 1 ? "" : "s")} saved. The quiz is worth {item.MaxScore:0.##} points.");
    }

    // ======================================================================= sending

    /// <summary>
    /// Sends modules of the course to the courses of some teachers. Each teacher's course gets a copy of the module with all its
    /// activities, quizzes (with their questions) and task performances, as a draft that is not posted. A module that was sent to
    /// that course before is skipped. Each teacher is told once.
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
            .Where(m => m.SubjectId == subjectId && moduleIds.Contains(m.Id))
            .Include(m => m.Items.OrderBy(i => i.Position).ThenBy(i => i.Id)).ThenInclude(i => i.Questions.OrderBy(q => q.Position).ThenBy(q => q.Id))
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
                foreach (var item in source.Items)
                {
                    var copy = new ModuleItem
                    {
                        Kind = item.Kind, Title = item.Title, Instructions = item.Instructions, MaxScore = item.MaxScore, DueAtUtc = item.DueAtUtc,
                        Position = item.Position, TimeLimitMinutes = item.TimeLimitMinutes, ShuffleQuestions = item.ShuffleQuestions,
                        ShuffleOptions = item.ShuffleOptions, AntiCheat = item.AntiCheat, MaxViolations = item.MaxViolations, ShowResult = item.ShowResult,
                    };
                    foreach (var q in item.Questions)
                    {
                        copy.Questions.Add(new QuizQuestion
                        {
                            Position = q.Position, Text = q.Text, Kind = q.Kind, OptionsText = q.OptionsText, CorrectAnswer = q.CorrectAnswer, Points = q.Points,
                        });
                    }

                    module.Items.Add(copy);
                }

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
                $"{subject} · {teacher.Section}: {list}. They are drafts: set when to post them and the due dates.",
                $"teacher/subjects/{teacher.AssignmentId}", ct);
        }

        var teachersTold = told.Select(t => t.UserId).Distinct().Count();
        if (copied == 0)
        {
            return ServiceResult.Ok("Nothing new to send: the teachers you chose already have those modules.", 0);
        }

        return ServiceResult.Ok(
            $"Sent {Plural(library.Count, "module")} to {Plural(teachersTold, "teacher")}"
            + (skipped > 0 ? $" ({skipped} already sent before were skipped)." : ". They are drafts in the teachers' courses."), copied);
    }

    // ======================================================================= helpers

    private static async Task<bool> IsProviderAsync(AppDbContext db, int userId, CancellationToken ct) =>
        await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.IsActive && u.RoleId == AppRoles.AcademicProviderId, ct);

    /// <summary>The school year of the current term, or the latest school year when no term is current.</summary>
    internal static async Task<int?> CurrentYearIdAsync(AppDbContext db, CancellationToken ct) =>
        await db.Terms.AsNoTracking().Where(t => t.IsCurrent).Select(t => (int?)t.SchoolYearId).FirstOrDefaultAsync(ct)
        ?? await db.SchoolYears.AsNoTracking().OrderByDescending(y => y.StartDate).ThenByDescending(y => y.Id).Select(y => (int?)y.Id).FirstOrDefaultAsync(ct);

    private static string Plural(int count, string one) => count == 1 ? $"1 {one}" : $"{count} {one}s";
}
