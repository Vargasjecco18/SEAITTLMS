using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>When a module or an item is visible to students: empty is a draft, a past time is posted, a future time is scheduled.</summary>
public static class Visibility
{
    public const string Draft = "Draft";
    public const string Scheduled = "Scheduled";
    public const string Posted = "Posted";

    public static string State(DateTime? visibleFromUtc, DateTime nowUtc) =>
        visibleFromUtc is null ? Draft : visibleFromUtc <= nowUtc ? Posted : Scheduled;

    public static bool IsVisible(DateTime? visibleFromUtc, DateTime nowUtc) => visibleFromUtc is not null && visibleFromUtc <= nowUtc;
}

// ---------------------------------------------------------------------- what the teacher types

public sealed class ModuleInput
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string? Body { get; set; }
    public string? LinkUrl { get; set; }
}

public sealed class ItemInput
{
    public string Kind { get; set; } = ItemKinds.Activity;
    public string Title { get; set; } = "";
    public string? Instructions { get; set; }
    public decimal MaxScore { get; set; } = 100;
    public DateTime? DueAtUtc { get; set; }
    public DateTime? VisibleFromUtc { get; set; }

    public int? TimeLimitMinutes { get; set; }
    public bool ShuffleQuestions { get; set; } = true;
    public bool ShuffleOptions { get; set; } = true;
    public bool AntiCheat { get; set; } = true;
    public int MaxViolations { get; set; } = 3;
    public bool ShowResult { get; set; } = true;
}

public sealed class QuestionInput
{
    public string Text { get; set; } = "";
    public string Kind { get; set; } = QuestionKinds.MultipleChoice;

    /// <summary>The choices, one per line (multiple choice).</summary>
    public string? OptionsText { get; set; }

    public string CorrectAnswer { get; set; } = "";
    public decimal Points { get; set; } = 1;
}

// ---------------------------------------------------------------------- what the teacher sees

public sealed record TeacherItem(
    int Id, string Kind, string Title, string? Instructions, decimal MaxScore, DateTime? DueAtUtc, DateTime? VisibleFromUtc, string State,
    int? TimeLimitMinutes, bool ShuffleQuestions, bool ShuffleOptions, bool AntiCheat, int MaxViolations, bool ShowResult,
    int Questions, int Students, int Submitted, int Graded, int Flagged);

/// <summary>An activity, quiz or task performance with the settings the calendar's edit box shows, and where it belongs.</summary>
public sealed record ItemForEdit(
    int ModuleId, int AssignmentId, int ItemId, string Kind, string Title, string? Instructions, decimal MaxScore, DateTime? DueAtUtc, DateTime? VisibleFromUtc,
    int? TimeLimitMinutes, bool ShuffleQuestions, bool ShuffleOptions, bool AntiCheat, int MaxViolations, bool ShowResult, int Questions,
    string SubjectName, byte GradeLevel, string StrandCode, string SectionName, string ModuleTitle)
{
    public string SectionLabel => TeachingService.SectionLabel(GradeLevel, StrandCode, SectionName);
}

public sealed record TeacherModule(
    int Id, string Title, string? Description, string? Body, string? LinkUrl, int Position, DateTime? VisibleFromUtc, string State,
    IReadOnlyList<TeacherItem> Items, bool IsSyllabus = false, bool FromProvider = false);

public sealed record TeacherSubject(
    int AssignmentId, int SubjectId, string SubjectName, string SectionLabel, string? Schedule, int Students, IReadOnlyList<TeacherModule> Modules);

// ---------------------------------------------------------------------- what the student sees

public sealed record StudentItem(
    int Id, string Kind, string Title, decimal MaxScore, DateTime? DueAtUtc, string Status, decimal? Score, bool IsLate, bool IsOverdue, int ModuleId, string ModuleTitle);

public sealed record StudentModule(
    int Id, string Title, string? Description, string? Body, string? LinkUrl, int Position, DateTime VisibleFromUtc, bool Opened, IReadOnlyList<StudentItem> Items,
    int AssignmentId = 0, string SubjectName = "", bool IsSyllabus = false);

public sealed record StudentSubject(
    int AssignmentId, int SubjectId, string SubjectName, string SectionLabel, string TeacherName, IReadOnlyList<StudentModule> Modules);

/// <summary>
/// Modules and the activities, quizzes and task performances in them. The teacher of a subject makes and posts them; the students
/// of the section read them once they are posted. Every method checks that the person is the teacher of the subject, or a student who
/// takes it, so nobody can reach another class's work by changing an address.
/// </summary>
public sealed class ModuleService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider clock, NotificationService notifications)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // ======================================================================= the teacher: reading

    public async Task<TeacherSubject?> GetTeacherSubjectAsync(int teacherId, int assignmentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = Now;

        var a = await db.TeachingAssignments.AsNoTracking().Where(x => x.Id == assignmentId && x.TeacherId == teacherId)
            .Select(x => new
            {
                x.Id, x.SubjectId, SubjectName = x.Subject.Name, x.SectionId, x.Section.GradeLevel, StrandCode = x.Section.Strand.Code,
                SectionName = x.Section.Name, Schedule = x.Section.Schedule != null ? x.Section.Schedule.Code : null,
            })
            .FirstOrDefaultAsync(ct);
        if (a is null)
        {
            return null;
        }

        var students = await Roster.StudentIds(db, a.SectionId, a.SubjectId).CountAsync(ct);
        var modules = await db.Modules.AsNoTracking().Where(m => m.AssignmentId == assignmentId).OrderBy(m => m.Position).ThenBy(m => m.Id).ToListAsync(ct);
        var moduleIds = modules.Select(m => m.Id).ToList();
        var items = await db.ModuleItems.AsNoTracking().Where(i => moduleIds.Contains(i.ModuleId)).OrderBy(i => i.Position).ThenBy(i => i.Id)
            .Select(i => new
            {
                Item = i,
                Questions = i.Questions.Count,
                Submitted = i.Submissions.Count(s => s.Status != SubmissionStatus.InProgress),
                Graded = i.Submissions.Count(s => s.Status == SubmissionStatus.Graded),
                Flagged = i.Submissions.Count(s => s.IsFlagged),
            })
            .ToListAsync(ct);

        var fromProvider = (await db.MaterialDeliveries.AsNoTracking().Where(d => d.AssignmentId == assignmentId).Select(d => d.ModuleId).ToListAsync(ct)).ToHashSet();
        var result = modules.Select(m => new TeacherModule(
            m.Id, m.Title, m.Description, m.Body, m.LinkUrl, m.Position, m.VisibleFromUtc, Visibility.State(m.VisibleFromUtc, now),
            items.Where(x => x.Item.ModuleId == m.Id).Select(x => ToTeacherItem(x.Item, now, x.Questions, students, x.Submitted, x.Graded, x.Flagged)).ToList(),
            m.IsSyllabus, fromProvider.Contains(m.Id))).ToList();

        return new TeacherSubject(a.Id, a.SubjectId, a.SubjectName, TeachingService.SectionLabel(a.GradeLevel, a.StrandCode, a.SectionName), a.Schedule, students, result);
    }

    /// <summary>
    /// Remembers that the teacher opened a module the academic provider sent them (its text, one of its activities, or they posted it). Only the first time
    /// is kept; a module the teacher wrote themselves has no delivery, so nothing happens.
    /// </summary>
    public async Task MarkDeliveryOpenedAsync(int teacherId, int moduleId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = Now;
        await db.MaterialDeliveries
            .Where(d => d.ModuleId == moduleId && d.OpenedAtUtc == null && d.Assignment.TeacherId == teacherId)
            .ExecuteUpdateAsync(set => set.SetProperty(d => d.OpenedAtUtc, (DateTime?)now).SetProperty(d => d.UpdatedAtUtc, now), ct);
    }

    /// <summary>The same for an activity, a quiz or a task performance of the teacher (it belongs to a module).</summary>
    public async Task MarkItemOpenedAsync(int teacherId, int itemId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var moduleId = await db.ModuleItems.AsNoTracking().Where(i => i.Id == itemId && i.Module.Assignment.TeacherId == teacherId).Select(i => (int?)i.ModuleId).FirstOrDefaultAsync(ct);
        if (moduleId is { } id)
        {
            await MarkDeliveryOpenedAsync(teacherId, id, ct);
        }
    }

    /// <summary>One item of the teacher with what its edit box needs, or null when it is not theirs.</summary>
    public async Task<ItemForEdit?> GetItemForEditAsync(int teacherId, int itemId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.ModuleItems.AsNoTracking()
            .Where(i => i.Id == itemId && i.Module.Assignment.TeacherId == teacherId)
            .Select(i => new ItemForEdit(
                i.ModuleId, i.Module.AssignmentId, i.Id, i.Kind, i.Title, i.Instructions, i.MaxScore, i.DueAtUtc, i.VisibleFromUtc, i.TimeLimitMinutes,
                i.ShuffleQuestions, i.ShuffleOptions, i.AntiCheat, i.MaxViolations, i.ShowResult, i.Questions.Count,
                i.Module.Assignment.Subject.Name, i.Module.Assignment.Section.GradeLevel, i.Module.Assignment.Section.Strand.Code, i.Module.Assignment.Section.Name,
                i.Module.Title))
            .Select(x => x)
            .FirstOrDefaultAsync(ct);
    }

    private static TeacherItem ToTeacherItem(ModuleItem i, DateTime now, int questions, int students, int submitted, int graded, int flagged) => new(
        i.Id, i.Kind, i.Title, i.Instructions, i.MaxScore, i.DueAtUtc, i.VisibleFromUtc, i.VisibleFromUtc is null ? "WithModule" : Visibility.State(i.VisibleFromUtc, now),
        i.TimeLimitMinutes, i.ShuffleQuestions, i.ShuffleOptions, i.AntiCheat, i.MaxViolations, i.ShowResult, questions, students, submitted, graded, flagged);

    // ======================================================================= the teacher: modules

    public async Task<ServiceResult> CreateModuleAsync(int teacherId, int assignmentId, ModuleInput input, CancellationToken ct = default)
    {
        var errors = ValidateModule(input);
        if (errors.Count > 0)
        {
            return ServiceResult.Fail(errors);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await db.TeachingAssignments.AnyAsync(a => a.Id == assignmentId && a.TeacherId == teacherId, ct))
        {
            return ServiceResult.Fail("This is not your course.");
        }

        var position = (await db.Modules.Where(m => m.AssignmentId == assignmentId).MaxAsync(m => (int?)m.Position, ct) ?? 0) + 1;
        var module = new LearningModule
        {
            AssignmentId = assignmentId, Title = input.Title.Trim(), Description = Clean(input.Description), Body = Clean(input.Body),
            LinkUrl = Clean(input.LinkUrl), Position = position,
        };
        db.Modules.Add(module);
        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok("The module was made. Students can't see it until you post it.", module.Id);
    }

    public async Task<ServiceResult> UpdateModuleAsync(int teacherId, int moduleId, ModuleInput input, CancellationToken ct = default)
    {
        var errors = ValidateModule(input);
        if (errors.Count > 0)
        {
            return ServiceResult.Fail(errors);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var module = await db.Modules.FirstOrDefaultAsync(m => m.Id == moduleId && m.Assignment.TeacherId == teacherId, ct);
        if (module is null)
        {
            return ServiceResult.Fail("This module was not found.");
        }

        module.Title = input.Title.Trim();
        module.Description = Clean(input.Description);
        module.Body = Clean(input.Body);
        module.LinkUrl = Clean(input.LinkUrl);
        await db.SaveChangesAsync(ct);
        await MarkDeliveryOpenedAsync(teacherId, moduleId, ct);
        return ServiceResult.Ok("The module was saved.");
    }

    public async Task<ServiceResult> DeleteModuleAsync(int teacherId, int moduleId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var deleted = await db.Modules.Where(m => m.Id == moduleId && m.Assignment.TeacherId == teacherId).ExecuteDeleteAsync(ct);
        return deleted == 0 ? ServiceResult.Fail("This module was not found.") : ServiceResult.Ok("The module was deleted with its activities and grades.");
    }

    /// <summary>Posts the module now (<paramref name="atUtc"/> empty) or at a time in the future. Students are told when it appears.</summary>
    public async Task<ServiceResult> PostModuleAsync(int teacherId, int moduleId, DateTime? atUtc, CancellationToken ct = default)
    {
        var now = Now;
        if (atUtc is { } at && at <= now.AddMinutes(1))
        {
            atUtc = null;
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var module = await db.Modules.FirstOrDefaultAsync(m => m.Id == moduleId && m.Assignment.TeacherId == teacherId, ct);
        if (module is null)
        {
            return ServiceResult.Fail("This module was not found.");
        }

        module.VisibleFromUtc = atUtc ?? now;
        module.NotifiedAtUtc = null;
        await db.SaveChangesAsync(ct);
        await MarkDeliveryOpenedAsync(teacherId, moduleId, ct);

        if (atUtc is null)
        {
            await AutomationService.NotifyNewContentAsync(dbFactory, notifications, now, ct);
            return ServiceResult.Ok("The module is posted. Students were told.");
        }

        return ServiceResult.Ok($"The module will appear for the students on {Components.Shared.PhilippineTime.FormatDateTime(atUtc.Value)}.");
    }

    /// <summary>Hides a posted or scheduled module again (it becomes a draft).</summary>
    public async Task<ServiceResult> HideModuleAsync(int teacherId, int moduleId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var changed = await db.Modules.Where(m => m.Id == moduleId && m.Assignment.TeacherId == teacherId)
            .ExecuteUpdateAsync(set => set.SetProperty(m => m.VisibleFromUtc, (DateTime?)null).SetProperty(m => m.NotifiedAtUtc, (DateTime?)null), ct);
        return changed == 0 ? ServiceResult.Fail("This module was not found.") : ServiceResult.Ok("The module is hidden from the students again.");
    }

    /// <summary>Moves a module one place up (-1) or down (+1) in the subject.</summary>
    public async Task<ServiceResult> MoveModuleAsync(int teacherId, int moduleId, int direction, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var module = await db.Modules.AsNoTracking().Where(m => m.Id == moduleId && m.Assignment.TeacherId == teacherId).Select(m => new { m.Id, m.AssignmentId }).FirstOrDefaultAsync(ct);
        if (module is null)
        {
            return ServiceResult.Fail("This module was not found.");
        }

        var all = await db.Modules.Where(m => m.AssignmentId == module.AssignmentId).OrderBy(m => m.Position).ThenBy(m => m.Id).ToListAsync(ct);
        var index = all.FindIndex(m => m.Id == moduleId);
        var target = index + Math.Sign(direction);
        if (target < 0 || target >= all.Count)
        {
            return ServiceResult.Ok();
        }

        (all[index], all[target]) = (all[target], all[index]);
        for (var i = 0; i < all.Count; i++)
        {
            all[i].Position = i + 1;
        }

        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    // ======================================================================= the teacher: activities, quizzes, task performances

    public async Task<ServiceResult> SaveItemAsync(int teacherId, int moduleId, int? itemId, ItemInput input, CancellationToken ct = default)
    {
        var errors = ValidateItem(input);
        if (errors.Count > 0)
        {
            return ServiceResult.Fail(errors);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var module = await db.Modules.AsNoTracking().Where(m => m.Id == moduleId && m.Assignment.TeacherId == teacherId).Select(m => new { m.Id, m.VisibleFromUtc }).FirstOrDefaultAsync(ct);
        if (module is null)
        {
            return ServiceResult.Fail("This module was not found.");
        }

        await MarkDeliveryOpenedAsync(teacherId, moduleId, ct);

        ModuleItem item;
        var isNew = itemId is null;
        if (isNew)
        {
            var position = (await db.ModuleItems.Where(i => i.ModuleId == moduleId).MaxAsync(i => (int?)i.Position, ct) ?? 0) + 1;
            item = new ModuleItem { ModuleId = moduleId, Position = position };
            db.ModuleItems.Add(item);
        }
        else
        {
            var existing = await db.ModuleItems.FirstOrDefaultAsync(i => i.Id == itemId && i.ModuleId == moduleId, ct);
            if (existing is null)
            {
                return ServiceResult.Fail("This item was not found.");
            }

            item = existing;
        }

        var scheduleChanged = isNew || item.VisibleFromUtc != input.VisibleFromUtc;
        var dueChanged = isNew || item.DueAtUtc != input.DueAtUtc;
        item.Kind = input.Kind;
        item.Title = input.Title.Trim();
        item.Instructions = Clean(input.Instructions);
        item.MaxScore = input.MaxScore;
        item.DueAtUtc = input.DueAtUtc;
        item.VisibleFromUtc = input.VisibleFromUtc;
        item.TimeLimitMinutes = input.Kind == ItemKinds.Quiz ? input.TimeLimitMinutes : null;
        item.ShuffleQuestions = input.ShuffleQuestions;
        item.ShuffleOptions = input.ShuffleOptions;
        item.AntiCheat = input.AntiCheat;
        item.MaxViolations = input.MaxViolations;
        item.ShowResult = input.ShowResult;
        if (scheduleChanged)
        {
            item.NotifiedAtUtc = null;
        }

        if (dueChanged)
        {
            item.DueReminderSentAtUtc = null;
        }

        await db.SaveChangesAsync(ct);

        // An item added to a module that is already posted is told to the students at once.
        if (Visibility.IsVisible(module.VisibleFromUtc, Now))
        {
            await AutomationService.NotifyNewContentAsync(dbFactory, notifications, Now, ct);
        }

        return ServiceResult.Ok(isNew ? $"{ItemKinds.Display(item.Kind)} added." : "Saved.", item.Id);
    }

    public async Task<ServiceResult> DeleteItemAsync(int teacherId, int itemId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var deleted = await db.ModuleItems.Where(i => i.Id == itemId && i.Module.Assignment.TeacherId == teacherId).ExecuteDeleteAsync(ct);
        return deleted == 0 ? ServiceResult.Fail("This item was not found.") : ServiceResult.Ok("Deleted with the work the students handed in.");
    }

    public async Task<IReadOnlyList<QuestionInput>?> GetQuestionsAsync(int teacherId, int itemId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await db.ModuleItems.AnyAsync(i => i.Id == itemId && i.Kind == ItemKinds.Quiz && i.Module.Assignment.TeacherId == teacherId, ct))
        {
            return null;
        }

        return await db.QuizQuestions.AsNoTracking().Where(q => q.ItemId == itemId).OrderBy(q => q.Position).ThenBy(q => q.Id)
            .Select(q => new QuestionInput { Text = q.Text, Kind = q.Kind, OptionsText = q.OptionsText, CorrectAnswer = q.CorrectAnswer, Points = q.Points })
            .ToListAsync(ct);
    }

    /// <summary>Replaces the questions of a quiz. The score of the quiz becomes the total of the points. Not possible once somebody has started it.</summary>
    public async Task<ServiceResult> SaveQuestionsAsync(int teacherId, int itemId, IReadOnlyList<QuestionInput> questions, CancellationToken ct = default)
    {
        var errors = ValidateQuestions(questions);
        if (errors.Count > 0)
        {
            return ServiceResult.Fail(errors);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var item = await db.ModuleItems.FirstOrDefaultAsync(i => i.Id == itemId && i.Kind == ItemKinds.Quiz && i.Module.Assignment.TeacherId == teacherId, ct);
        if (item is null)
        {
            return ServiceResult.Fail("This quiz was not found.");
        }

        if (await db.Submissions.AnyAsync(s => s.ItemId == itemId, ct))
        {
            return ServiceResult.Fail("Students have already started this quiz, so its questions can't be changed.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.QuizQuestions.Where(q => q.ItemId == itemId).ExecuteDeleteAsync(ct);
        var position = 0;
        foreach (var q in questions)
        {
            db.QuizQuestions.Add(new QuizQuestion
            {
                ItemId = itemId, Position = ++position, Text = q.Text.Trim(), Kind = q.Kind,
                OptionsText = q.Kind == QuestionKinds.MultipleChoice ? string.Join('\n', SplitOptions(q.OptionsText)) : null,
                CorrectAnswer = q.Kind == QuestionKinds.MultipleChoice
                    ? SplitOptions(q.OptionsText).First(o => string.Equals(o, q.CorrectAnswer.Trim(), StringComparison.OrdinalIgnoreCase))
                    : q.CorrectAnswer.Trim(),
                Points = q.Points,
            });
        }

        item.MaxScore = questions.Count == 0 ? 100 : questions.Sum(q => q.Points);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ServiceResult.Ok($"{questions.Count} question{(questions.Count == 1 ? "" : "s")} saved. The quiz is worth {item.MaxScore:0.##} points.");
    }

    /// <summary>What is wrong with the questions of a quiz (none when they are all right). Also used for the quizzes of the course materials.</summary>
    internal static List<string> ValidateQuestions(IReadOnlyList<QuestionInput> questions)
    {
        var errors = new List<string>();
        for (var n = 0; n < questions.Count; n++)
        {
            var q = questions[n];
            var label = $"Question {n + 1}";
            if (string.IsNullOrWhiteSpace(q.Text) || q.Text.Trim().Length > 1000)
            {
                errors.Add($"{label}: write the question (up to 1000 characters).");
            }

            if (q.Points is <= 0 or > 100)
            {
                errors.Add($"{label}: the points must be between 0.01 and 100.");
            }

            switch (q.Kind)
            {
                case QuestionKinds.MultipleChoice:
                    var options = SplitOptions(q.OptionsText);
                    if (options.Count < 2)
                    {
                        errors.Add($"{label}: write at least two choices, one per line.");
                    }
                    else if (options.Distinct(StringComparer.OrdinalIgnoreCase).Count() != options.Count)
                    {
                        errors.Add($"{label}: two choices are the same.");
                    }
                    else if (!options.Contains(q.CorrectAnswer.Trim(), StringComparer.OrdinalIgnoreCase))
                    {
                        errors.Add($"{label}: pick the right choice.");
                    }

                    break;
                case QuestionKinds.TrueFalse:
                    if (q.CorrectAnswer is not ("True" or "False"))
                    {
                        errors.Add($"{label}: say whether the statement is true or false.");
                    }

                    break;
                case QuestionKinds.ShortAnswer:
                    if (string.IsNullOrWhiteSpace(q.CorrectAnswer) || q.CorrectAnswer.Length > 500)
                    {
                        errors.Add($"{label}: write the right answer (several can be separated by |).");
                    }

                    break;
                default:
                    errors.Add($"{label}: unknown kind of question.");
                    break;
            }
        }

        if (questions.Count > 100)
        {
            errors.Add("A quiz can have at most 100 questions.");
        }

        return errors;
    }

    public static List<string> SplitOptions(string? text) =>
        (text ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    // ======================================================================= the student: reading

    /// <summary>True when the student is enrolled in the section with the subject (so they are on the roster of the assignment).</summary>
    private static async Task<bool> OnRosterAsync(AppDbContext db, int studentId, int sectionId, int subjectId, CancellationToken ct) =>
        await Roster.Enrollments(db, sectionId, subjectId).AnyAsync(e => e.StudentId == studentId, ct);

    public async Task<StudentSubject?> GetStudentSubjectAsync(int studentId, int assignmentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = Now;

        var a = await db.TeachingAssignments.AsNoTracking().Where(x => x.Id == assignmentId)
            .Select(x => new
            {
                x.Id, x.SectionId, x.SubjectId, SubjectName = x.Subject.Name, x.Section.GradeLevel, StrandCode = x.Section.Strand.Code,
                SectionName = x.Section.Name, Teacher = x.Teacher.FirstName + " " + x.Teacher.LastName,
            })
            .FirstOrDefaultAsync(ct);
        if (a is null || !await OnRosterAsync(db, studentId, a.SectionId, a.SubjectId, ct))
        {
            return null;
        }

        var modules = await db.Modules.AsNoTracking()
            .Where(m => m.AssignmentId == assignmentId && m.VisibleFromUtc != null && m.VisibleFromUtc <= now)
            .OrderBy(m => m.Position).ThenBy(m => m.Id)
            .ToListAsync(ct);
        var ids = modules.Select(m => m.Id).ToList();
        var opened = (await db.ModuleViews.AsNoTracking().Where(v => v.StudentId == studentId && ids.Contains(v.ModuleId)).Select(v => v.ModuleId).ToListAsync(ct)).ToHashSet();
        var items = await LoadStudentItemsAsync(db, studentId, ids, now, ct);

        return new StudentSubject(
            a.Id, a.SubjectId, a.SubjectName, TeachingService.SectionLabel(a.GradeLevel, a.StrandCode, a.SectionName), a.Teacher,
            modules.Select(m => new StudentModule(m.Id, m.Title, m.Description, null, null, m.Position, m.VisibleFromUtc!.Value, opened.Contains(m.Id),
                items.Where(i => i.ModuleId == m.Id).ToList(), IsSyllabus: m.IsSyllabus)).ToList());
    }

    /// <summary>A module with its text. Opening it is remembered, so it is no longer listed as not opened yet.</summary>
    public async Task<StudentModule?> OpenModuleAsync(int studentId, int moduleId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = Now;

        var m = await db.Modules.AsNoTracking().Where(x => x.Id == moduleId && x.VisibleFromUtc != null && x.VisibleFromUtc <= now)
            .Select(x => new { Module = x, x.Assignment.SectionId, x.Assignment.SubjectId, SubjectName = x.Assignment.Subject.Name })
            .FirstOrDefaultAsync(ct);
        if (m is null || !await OnRosterAsync(db, studentId, m.SectionId, m.SubjectId, ct))
        {
            return null;
        }

        if (!await db.ModuleViews.AnyAsync(v => v.ModuleId == moduleId && v.StudentId == studentId, ct))
        {
            db.ModuleViews.Add(new ModuleView { ModuleId = moduleId, StudentId = studentId, FirstOpenedAtUtc = now });
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Opened twice at the same moment: it is remembered either way.
            }
        }

        var items = await LoadStudentItemsAsync(db, studentId, [moduleId], now, ct);
        return new StudentModule(m.Module.Id, m.Module.Title, m.Module.Description, m.Module.Body, m.Module.LinkUrl, m.Module.Position, m.Module.VisibleFromUtc!.Value, true, items,
            m.Module.AssignmentId, m.SubjectName, m.Module.IsSyllabus);
    }

    private static async Task<List<StudentItem>> LoadStudentItemsAsync(AppDbContext db, int studentId, IReadOnlyCollection<int> moduleIds, DateTime now, CancellationToken ct)
    {
        var rows = await db.ModuleItems.AsNoTracking()
            .Where(i => moduleIds.Contains(i.ModuleId) && (i.VisibleFromUtc == null || i.VisibleFromUtc <= now))
            .OrderBy(i => i.Position).ThenBy(i => i.Id)
            .Select(i => new
            {
                i.Id, i.Kind, i.Title, i.MaxScore, i.DueAtUtc, i.ModuleId, ModuleTitle = i.Module.Title, i.ShowResult,
                Mine = i.Submissions.Where(s => s.StudentId == studentId).Select(s => new { s.Status, s.Score, s.IsLate }).FirstOrDefault(),
            })
            .ToListAsync(ct);

        return rows.Select(r =>
        {
            var status = r.Mine is null ? "NotStarted" : r.Mine.Status;
            var overdue = r.DueAtUtc is { } due && due < now && (r.Mine is null || r.Mine.Status == SubmissionStatus.InProgress);
            var score = r.Kind == ItemKinds.Quiz && !r.ShowResult ? null : r.Mine?.Score;
            return new StudentItem(r.Id, r.Kind, r.Title, r.MaxScore, r.DueAtUtc, status, score, r.Mine?.IsLate ?? false, overdue, r.ModuleId, r.ModuleTitle);
        }).ToList();
    }

    // ======================================================================= helpers

    internal static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    internal static List<string> ValidateModule(ModuleInput input)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(input.Title) || input.Title.Trim().Length > 200)
        {
            errors.Add("Write the title of the module (up to 200 characters).");
        }

        if (input.Description is { Length: > 1000 })
        {
            errors.Add("The short description can have at most 1000 characters.");
        }

        if (input.Body is { Length: > 20000 })
        {
            errors.Add("The lesson can have at most 20,000 characters.");
        }

        if (!string.IsNullOrWhiteSpace(input.LinkUrl) && !IsWebAddress(input.LinkUrl.Trim()))
        {
            errors.Add("The link must start with http:// or https://.");
        }

        return errors;
    }

    public static bool IsWebAddress(string text) =>
        text.Length <= 500 && Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";

    internal static List<string> ValidateItem(ItemInput input)
    {
        var errors = new List<string>();
        if (!ItemKinds.All.Contains(input.Kind))
        {
            errors.Add("Choose what kind it is.");
        }

        if (string.IsNullOrWhiteSpace(input.Title) || input.Title.Trim().Length > 200)
        {
            errors.Add("Write a title (up to 200 characters).");
        }

        if (input.Instructions is { Length: > 10000 })
        {
            errors.Add("The instructions can have at most 10,000 characters.");
        }

        if (input.Kind != ItemKinds.Quiz && (input.MaxScore <= 0 || input.MaxScore > 1000))
        {
            errors.Add("The highest score must be between 1 and 1000.");
        }

        if (input.Kind == ItemKinds.Quiz)
        {
            if (input.TimeLimitMinutes is { } limit && (limit < 1 || limit > 600))
            {
                errors.Add("The time limit must be between 1 and 600 minutes.");
            }

            if (input.MaxViolations is < 1 or > 10)
            {
                errors.Add("The number of warnings before the quiz is handed in must be between 1 and 10.");
            }
        }

        if (input.DueAtUtc is { } due && input.VisibleFromUtc is { } from && due <= from)
        {
            errors.Add("The due date must be after the time it appears.");
        }

        return errors;
    }
}
