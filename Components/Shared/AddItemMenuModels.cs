using SEAITT_LMS.Data.Entities;
using SEAITT_LMS.Services;

namespace SEAITT_LMS.Components.Shared;

/// <summary>A module of a course in the Add menu: its number and a label like "Module 2: Journal entries".</summary>
public sealed record AddMenuModule(int Id, string Label);

/// <summary>What was chosen in the Add menu: the module and the kind of work (one of <see cref="Data.Entities.ItemKinds"/>).</summary>
public sealed record AddMenuPick(int ModuleId, string Kind);

/// <summary>What importing an Excel file of items did: how many were added, and what went wrong with the others.</summary>
public sealed record ItemImportSummary(int Created, IReadOnlyList<string> Errors);

/// <summary>A link in the second menu of a course.</summary>
public sealed record CourseMenuLink(string Label, string Icon, string Href, bool Current = false);

public static class AddItemMenuLabels
{
    /// <summary>What the Add menu gives back (instead of a kind of work) when the person chose "Template and import (Excel)" for a module.</summary>
    public const string ExcelKind = "Excel";

    /// <summary>"Module 2: Ledgers" for a module that is called Ledgers; a module whose title already starts with "Module" (like "Module 2: Ledgers") is shown as it is.</summary>
    public static string ModuleLabel(int number, string title) =>
        title.TrimStart().StartsWith("Module", StringComparison.OrdinalIgnoreCase) ? title : $"Module {number}: {title}";
}

/// <summary>One line inside a dropdown of the course menu: a module, or a seatwork, assignment, quiz or task performance.</summary>
public sealed record CourseMenuEntry(string Title, string Href, bool Current = false);

/// <summary>A dropdown of the course menu (Modules, Seatwork, Assignments, Quizzes, Task performance): its name, how many it holds, the page that lists them all and the lines inside.</summary>
public sealed record CourseMenuGroup(string Key, string Label, string Icon, int Count, string SeeAllHref, IReadOnlyList<CourseMenuEntry> Entries);

/// <summary>Builds the dropdowns of the course menu from a course the teacher or the student has loaded, so every page of the course shows the same menu.</summary>
public static class CourseMenuBuilder
{
    /// <summary>The order of the menu: Modules, then the kinds of work (seatwork, assignments, quizzes, task performance) and, once the course has them, recitations and exams.</summary>
    private static readonly string[] KindOrder = [ItemKinds.Seatwork, ItemKinds.Activity, ItemKinds.Quiz, ItemKinds.TaskPerformance, ItemKinds.Recitation, ItemKinds.Exam];

    public static string Label(string kind) => kind switch
    {
        ItemKinds.Seatwork => "Seatwork",
        ItemKinds.Activity => "Assignments",
        ItemKinds.Quiz => "Quizzes",
        ItemKinds.TaskPerformance => "Task performance",
        ItemKinds.Recitation => "Recitations",
        ItemKinds.Exam => "Exams",
        _ => ItemKinds.Plural(kind),
    };

    public static IReadOnlyList<CourseMenuGroup> ForTeacher(TeacherSubject subject, int? currentModuleId = null)
    {
        var page = $"teacher/subjects/{subject.AssignmentId}";
        var groups = new List<CourseMenuGroup>();
        var number = 0;
        var modules = subject.Modules.Where(m => !m.IsExamSet).Select(m => new CourseMenuEntry(m.IsSyllabus ? "Syllabus" : Titled(++number, m.Title), $"{page}/modules/{m.Id}", m.Id == currentModuleId)).ToList();
        groups.Add(new CourseMenuGroup("modules", "Modules", "layers", subject.Modules.Count(m => !m.IsSyllabus && !m.IsExamSet), page, modules));
        foreach (var kind in KindOrder)
        {
            var items = subject.Modules.SelectMany(m => m.Items.Where(i => i.Kind == kind)).ToList();
            if (ItemKinds.IsOptionalTab(kind) && items.Count == 0)
            {
                continue;
            }

            groups.Add(new CourseMenuGroup(CourseMenu.KeyOf(kind), Label(kind), SubjectStyle.KindIcon(kind), items.Count, $"{page}?tab={CourseMenu.KeyOf(kind)}",
                [.. items.Select(i => new CourseMenuEntry(i.Title, $"teacher/items/{i.Id}"))]));
        }

        return groups;
    }

    public static IReadOnlyList<CourseMenuGroup> ForStudent(StudentSubject subject, int? currentModuleId = null)
    {
        var page = $"student/subjects/{subject.AssignmentId}";
        var groups = new List<CourseMenuGroup>();
        var number = 0;
        var modules = subject.Modules.Where(m => !m.IsExamSet).Select(m => new CourseMenuEntry(m.IsSyllabus ? "Syllabus" : Titled(++number, m.Title), $"student/modules/{m.Id}", m.Id == currentModuleId)).ToList();
        groups.Add(new CourseMenuGroup("modules", "Modules", "layers", subject.Modules.Count(m => !m.IsSyllabus && !m.IsExamSet), page, modules));
        foreach (var kind in KindOrder)
        {
            var items = subject.Modules.SelectMany(m => m.Items.Where(i => i.Kind == kind)).ToList();
            if (ItemKinds.IsOptionalTab(kind) && items.Count == 0)
            {
                continue;
            }

            groups.Add(new CourseMenuGroup(CourseMenu.KeyOf(kind), Label(kind), SubjectStyle.KindIcon(kind), items.Count, $"{page}?tab={CourseMenu.KeyOf(kind)}",
                [.. items.Select(i => new CourseMenuEntry(i.Title, $"student/items/{i.Id}"))]));
        }

        return groups;
    }

    /// <summary>"Module 2: Ledgers" for a module that is called Ledgers; a title that already starts with "Module" stays as it is.</summary>
    private static string Titled(int number, string title) => AddItemMenuLabels.ModuleLabel(number, title);
}
