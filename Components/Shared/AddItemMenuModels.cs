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

    /// <summary>"Handout 2: Ledgers" for a handout that is called Ledgers; a handout whose title already starts with "Handout" (like "Handout 2: Ledgers") is shown as it is.</summary>
    public static string ModuleLabel(int number, string title)
    {
        var text = title.TrimStart();
        if (text.StartsWith("Handout", StringComparison.OrdinalIgnoreCase))
        {
            return title;
        }

        // A title written when the word was Module ("Module 2: Ledgers") shows with the new word.
        return text.StartsWith("Module", StringComparison.OrdinalIgnoreCase) ? "Handout" + text["Module".Length..] : $"Handout {number}: {title}";
    }
}

/// <summary>One line inside a dropdown of the course menu: a module, or a seatwork, assignment, quiz or task performance.</summary>
public sealed record CourseMenuEntry(string Title, string Href, bool Current = false);

/// <summary>A dropdown of the course menu (Courses, Modules, Activities, and inside Activities: Seatwork, Assignments, Quizzes, Task performance): its name, how many it holds, the page that lists them all (empty: none) and the lines inside. Activities holds the dropdowns of the kinds as its Children.</summary>
public sealed record CourseMenuGroup(string Key, string Label, string Icon, int Count, string SeeAllHref, IReadOnlyList<CourseMenuEntry> Entries, IReadOnlyList<CourseMenuGroup>? Children = null);

/// <summary>Builds the dropdowns of the course menu from a course the teacher or the student has loaded, so every page of the course shows the same menu.</summary>
public static class CourseMenuBuilder
{
    /// <summary>The order of the menu: Modules, then the kinds of work (seatwork, assignments, quizzes, task performance) and, once the course has them, recitations and exams.</summary>
    private static readonly string[] KindOrder = [ItemKinds.Seatwork, ItemKinds.Activity, ItemKinds.Quiz, ItemKinds.TaskPerformance, ItemKinds.Recitation, ItemKinds.Exam, ItemKinds.Other];

    public static string Label(string kind) => kind switch
    {
        ItemKinds.Seatwork => "Seatwork",
        ItemKinds.Activity => "Assignments",
        ItemKinds.Quiz => "Quizzes",
        ItemKinds.TaskPerformance => "Task performance",
        ItemKinds.Recitation => "Recitations",
        ItemKinds.Exam => "Exams",
        ItemKinds.Other => "Others",
        _ => ItemKinds.Plural(kind),
    };

    /// <summary>The dropdowns one level down: Activities is replaced by the dropdowns of the kinds inside it (for the pop-ups that list the kinds without the Activities parent).</summary>
    public static IReadOnlyList<CourseMenuGroup> Flatten(IReadOnlyList<CourseMenuGroup> groups) => [.. groups.SelectMany(g => g.Children ?? [g])];

    public static IReadOnlyList<CourseMenuGroup> ForTeacher(TeacherSubject subject, int? currentModuleId = null, int? currentItemId = null)
    {
        var page = $"teacher/subjects/{subject.AssignmentId}";
        var groups = new List<CourseMenuGroup>();
        var number = 0;
        var modules = subject.Modules.Where(m => !m.IsExamSet).Select(m => new CourseMenuEntry(m.IsSyllabus ? "Syllabus" : Titled(++number, m.Title), $"{page}/modules/{m.Id}", m.Id == currentModuleId)).ToList();
        groups.Add(new CourseMenuGroup("modules", "Handouts", "layers", subject.Modules.Count(m => !m.IsSyllabus && !m.IsExamSet), page, modules));

        // Activities: one dropdown with a dropdown of each kind inside it.
        var kinds = new List<CourseMenuGroup>();
        foreach (var kind in KindOrder.Where(k => k != ItemKinds.Exam))   // the exams are the provider's: the teacher's menu has no Exams
        {
            var items = subject.Modules.SelectMany(m => m.Items.Where(i => i.Kind == kind)).ToList();
            if (ItemKinds.IsOptionalTab(kind) && items.Count == 0)
            {
                continue;
            }

            kinds.Add(new CourseMenuGroup(CourseMenu.KeyOf(kind), Label(kind), SubjectStyle.KindIcon(kind), items.Count, $"{page}?tab={CourseMenu.KeyOf(kind)}",
                [.. items.Select(i => new CourseMenuEntry(i.Title, $"teacher/items/{i.Id}", i.Id == currentItemId))]));
        }

        groups.Add(new CourseMenuGroup("activities", "Activities", "notebook-pen", kinds.Sum(k => k.Count), "", [], kinds));
        return groups;
    }

    public static IReadOnlyList<CourseMenuGroup> ForStudent(StudentSubject subject, int? currentModuleId = null, int? currentItemId = null)
    {
        var page = $"student/subjects/{subject.AssignmentId}";
        var groups = new List<CourseMenuGroup>();
        var number = 0;
        var modules = subject.Modules.Where(m => !m.IsExamSet).Select(m => new CourseMenuEntry(m.IsSyllabus ? "Syllabus" : Titled(++number, m.Title), $"student/modules/{m.Id}", m.Id == currentModuleId)).ToList();
        groups.Add(new CourseMenuGroup("modules", "Handouts", "layers", subject.Modules.Count(m => !m.IsSyllabus && !m.IsExamSet), page, modules));

        var kinds = new List<CourseMenuGroup>();
        foreach (var kind in KindOrder)
        {
            var items = subject.Modules.SelectMany(m => m.Items.Where(i => i.Kind == kind)).ToList();
            if (ItemKinds.IsOptionalTab(kind) && items.Count == 0)
            {
                continue;
            }

            kinds.Add(new CourseMenuGroup(CourseMenu.KeyOf(kind), Label(kind), SubjectStyle.KindIcon(kind), items.Count, $"{page}?tab={CourseMenu.KeyOf(kind)}",
                [.. items.Select(i => new CourseMenuEntry(i.Title, $"student/items/{i.Id}", i.Id == currentItemId))]));
        }

        groups.Add(new CourseMenuGroup("activities", "Activities", "notebook-pen", kinds.Sum(k => k.Count), "", [], kinds));
        return groups;
    }

    /// <summary>The pages of the course under the line of the menu: the students, the gradebook and the attendance (teacher), the classmates, the teachers and the grades (student).</summary>
    public static IReadOnlyList<CourseMenuLink> ExtraLinks(bool teacher, int assignmentId, string current) => teacher
        ?
        [
            new("Students", "users-round", $"teacher/subjects/{assignmentId}/students", current == "students"),
            new("Gradebook", "clipboard-list", $"teacher/gradebook?subject={assignmentId}", current == "gradebook"),
            new("Attendance", "calendar-check", $"teacher/attendance?subject={assignmentId}", current == "attendance"),
        ]
        :
        [
            new("Classmates", "users-round", $"student/subjects/{assignmentId}/classmates", current == "classmates"),
            new("Teachers", "presentation", $"student/subjects/{assignmentId}/teachers", current == "teachers"),
            new("Grades", "trending-up", "student/grades", current == "grades"),
        ];

    /// <summary>"Handout 2: Ledgers" for a handout that is called Ledgers; a title that already starts with "Handout" stays as it is.</summary>
    private static string Titled(int number, string title) => AddItemMenuLabels.ModuleLabel(number, title);
}
