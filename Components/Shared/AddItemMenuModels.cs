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
