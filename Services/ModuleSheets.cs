using SEAITT_LMS.Components.Shared;

namespace SEAITT_LMS.Services;

/// <summary>A row of the modules workbook: the module that is ready (or what is wrong with it) and the files its Files column names.</summary>
public sealed record ModuleImportRow(int Row, string Title, ModuleInput? Input, IReadOnlyList<string> FileNames, IReadOnlyList<string> Errors);

public sealed record ModuleImportParse(IReadOnlyList<ModuleImportRow> Rows, string? FileError)
{
    public IReadOnlyList<string> FileNamesWanted => Rows.Where(r => r.Input is not null).SelectMany(r => r.FileNames).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}

/// <summary>A module as it goes into the export: its text and the names of its files.</summary>
public sealed record ModuleExportRow(string Title, string? Description, string? Body, string? LinkUrl, IReadOnlyList<string> FileNames);

/// <summary>One module that is ready to be saved, with the files it named (already read).</summary>
public sealed record ModuleToSave(ModuleInput Input, IReadOnlyList<NewAttachment> Files, int Row);

/// <summary>
/// The Excel workbook for adding modules to a course of the library (the academic provider's "Add module"): one row for each module with its title, short description, lesson text, link
/// and the names of its files (chosen when the workbook is imported and matched by name). The export of a course is the same workbook.
/// </summary>
public static class ModuleSheets
{
    public const int MaxRows = 100;
    private const int TemplateRows = 20;
    private const string SheetName = "Modules";

    private static readonly string[] Headers = ["Title", "Short description", "Lesson", "Link", "Files"];

    public static string FileName(string what) => FileDownloads.CleanName($"{what} - Modules.xlsx");

    public static byte[] Template() => Build([], example: true);

    public static byte[] Export(IEnumerable<ModuleExportRow> modules) => Build(modules.ToList(), example: false);

    private static byte[] Build(IReadOnlyList<ModuleExportRow> modules, bool example)
    {
        var book = new XlsxBook();
        var sheet = book.AddSheet(SheetName);
        sheet.Row(1, Headers, XlsxStyle.Header);
        sheet.Width(1, 34).Width(2, 40).Width(3, 60).Width(4, 36).Width(5, 36);
        var row = 2;
        foreach (var m in modules)
        {
            sheet.Set(row, 1, m.Title, XlsxStyle.InputText).Set(row, 2, m.Description ?? "", XlsxStyle.Wrap).Set(row, 3, m.Body ?? "", XlsxStyle.Wrap)
                .Set(row, 4, m.LinkUrl ?? "", XlsxStyle.InputText).Set(row, 5, string.Join("; ", m.FileNames), XlsxStyle.InputText);
            row++;
        }

        if (example)
        {
            sheet.Set(row, 1, "Module 1: Journal entries", XlsxStyle.InputText).Set(row, 2, "How a transaction is written in the journal.", XlsxStyle.Wrap)
                .Set(row, 3, "Read the lesson, then do the activities of this module.", XlsxStyle.Wrap).Set(row, 4, "", XlsxStyle.InputText).Set(row, 5, "lesson.pdf", XlsxStyle.InputText);
            row++;
        }

        for (; row <= Math.Max(TemplateRows + 1, modules.Count + 2); row++)
        {
            for (var column = 1; column <= Headers.Length; column++)
            {
                sheet.Set(row, column, null, column is 2 or 3 ? XlsxStyle.Wrap : XlsxStyle.InputText);
            }
        }

        sheet.FrozenRows = 1;

        var how = book.AddSheet("How to fill");
        how.Set(1, 1, "How to fill the Modules sheet", XlsxStyle.Title);
        var steps = new[]
        {
            "1. Type one module in each row. The example row can be changed or deleted.",
            "2. Title is needed. The short description is what the teachers see in the list, the lesson is the text of the module.",
            "3. Link (optional): a file or video the lesson uses; it starts with https://.",
            "4. Files: the names of the files of the module, separated by a semicolon, like lesson.pdf; worksheet.docx. In the import box you choose these files from your computer, and each one is attached to the module that names it.",
            $"5. At most {MaxRows} modules. Save the file as an Excel Workbook (.xlsx), then import it with the Template and import button. Add the assignments, quizzes and task performances of each module afterwards with Add Activities.",
        };
        for (var i = 0; i < steps.Length; i++)
        {
            how.Set(3 + i, 1, steps[i], XlsxStyle.Wrap);
        }

        how.Width(1, 120);
        return book.ToBytes();
    }

    public static ModuleImportParse Parse(byte[] data)
    {
        IReadOnlyList<XlsxSheetData> sheets;
        try
        {
            sheets = XlsxReader.Read(data);
        }
        catch (InvalidDataException problem)
        {
            return new ModuleImportParse([], problem.Message);
        }

        var sheet = sheets.FirstOrDefault(s => string.Equals(s.Name.Trim(), SheetName, StringComparison.OrdinalIgnoreCase)) ?? sheets.FirstOrDefault();
        if (sheet is null || sheet.Rows.Count == 0)
        {
            return new ModuleImportParse([], "The file has no rows.");
        }

        var headers = Enumerable.Range(1, 8).Select(c => sheet.Cell(1, c).Trim().ToLowerInvariant()).ToList();
        if (!headers.Contains("title"))
        {
            return new ModuleImportParse([], "This is not the template: the first row must have the column titles (Title, Short description ...). Download the template again.");
        }

        int Column(string name, int fallback) => headers.IndexOf(name) is >= 0 and var i ? i + 1 : fallback;
        var title = Column("title", 1);
        var description = Column("short description", 2);
        var lesson = Column("lesson", 3);
        var link = Column("link", 4);
        var files = Column("files", 5);
        var rows = new List<ModuleImportRow>();
        for (var r = 2; r <= sheet.Rows.Count; r++)
        {
            var cells = new[] { title, description, lesson, link, files }.Select(c => sheet.Cell(r, c).Trim()).ToList();
            if (cells.All(c => c.Length == 0))
            {
                continue;
            }

            if (rows.Count >= MaxRows)
            {
                return new ModuleImportParse(rows, $"The file has more than {MaxRows} modules. Import it in parts.");
            }

            var input = new ModuleInput { Title = cells[0], Description = cells[1].Length == 0 ? null : cells[1], Body = cells[2].Length == 0 ? null : cells[2], LinkUrl = cells[3].Length == 0 ? null : cells[3] };
            var errors = ModuleService.ValidateModule(input);
            var names = cells[4].Split([';', '\n', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            rows.Add(new ModuleImportRow(r, cells[0], errors.Count == 0 ? input : null, names, errors));
        }

        return rows.Count == 0 ? new ModuleImportParse([], "The file has no modules. Fill in the rows under the column titles.") : new ModuleImportParse(rows, null);
    }
}
