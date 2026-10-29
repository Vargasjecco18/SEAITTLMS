namespace SEAITT_LMS.Components.Shared;

/// <summary>A module of a course in the Add menu: its number and a label like "Module 2: Journal entries".</summary>
public sealed record AddMenuModule(int Id, string Label);

/// <summary>What was chosen in the Add menu: the module and the kind of work (one of <see cref="Data.Entities.ItemKinds"/>).</summary>
public sealed record AddMenuPick(int ModuleId, string Kind);

/// <summary>What importing an Excel file of items did: how many were added, and what went wrong with the others.</summary>
public sealed record ItemImportSummary(int Created, IReadOnlyList<string> Errors);

/// <summary>A link in the second menu of a course.</summary>
public sealed record CourseMenuLink(string Label, string Icon, string Href);
