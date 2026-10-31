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
/// The Excel workbook for adding modules to a course of the library (the academic provider's "Add module"): one row for each module with its title, link
/// and the names of its files (chosen when the workbook is imported and matched by name). The export of a course is the same workbook.
/// </summary>
public static class ModuleSheets
{
    public const int MaxRows = 100;
    private const int TemplateRows = 20;
    private const string SheetName = "Modules";

    private static readonly string[] Headers = ["Title", "Link", "Files"];

    public static string FileName(string what) => FileDownloads.CleanName($"{what} - Modules.xlsx");

    public static byte[] Template() => Build([], example: true);

    public static byte[] Export(IEnumerable<ModuleExportRow> modules) => Build(modules.ToList(), example: false);

    private static byte[] Build(IReadOnlyList<ModuleExportRow> modules, bool example)
    {
        var book = new XlsxBook();
        var sheet = book.AddSheet(SheetName);
        sheet.Row(1, Headers, XlsxStyle.Header);
        sheet.Width(1, 40).Width(2, 40).Width(3, 40);
        var row = 2;
        foreach (var m in modules)
        {
            sheet.Set(row, 1, m.Title, XlsxStyle.InputText).Set(row, 2, m.LinkUrl ?? "", XlsxStyle.InputText).Set(row, 3, string.Join("; ", m.FileNames), XlsxStyle.InputText);
            row++;
        }

        if (example)
        {
            sheet.Set(row, 1, "Module 1: Journal entries", XlsxStyle.InputText).Set(row, 2, "", XlsxStyle.InputText).Set(row, 3, "lesson.pdf", XlsxStyle.InputText);
            row++;
        }

        for (; row <= Math.Max(TemplateRows + 1, modules.Count + 2); row++)
        {
            for (var column = 1; column <= Headers.Length; column++)
            {
                sheet.Set(row, column, null, XlsxStyle.InputText);
            }
        }

        sheet.FrozenRows = 1;
        var lastModule = Math.Max(TemplateRows + 1, modules.Count + 2);
        sheet.Tip($"A2:A{lastModule}", "Type the title of the module (needed). The title is the title of its file.").Tip($"B2:B{lastModule}", "Type or paste an address that starts with https://")
            .Tip($"C2:C{lastModule}", "Type the names of the files with a semicolon between them, like lesson.pdf; worksheet.docx. Choose the same files in the import box.");

        TemplateGuide.Add(book, "How to fill the Modules sheet",
        [
            new("Modules", "Title", "Needed", "The name of the module, like Module 1: Introduction.", TemplateGuide.TypeIt),
            new("Modules", "Link", "Optional", "A page or a video the lesson uses. It starts with https://", "Type or paste the address."),
            new("Modules", "Files", "Optional", "The names of the files of the module, like lesson.pdf; worksheet.docx.", "Type the names with a semicolon (;) between them. In the import box, choose the same files."),
        ],
            $"At most {MaxRows} modules.",
            "The teachers add the seatwork, assignments, quizzes and task performances of each module with Add activities.");
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
            return new ModuleImportParse([], "This is not the template: the first row must have the column titles (Title, Link, Files). Download the template again.");
        }

        int Column(string name, int fallback) => headers.IndexOf(name) is >= 0 and var i ? i + 1 : fallback;
        var title = Column("title", 1);
        var link = Column("link", 2);
        var files = Column("files", 3);
        var rows = new List<ModuleImportRow>();
        for (var r = 2; r <= sheet.Rows.Count; r++)
        {
            var cells = new[] { title, link, files }.Select(c => sheet.Cell(r, c).Trim()).ToList();
            if (cells.All(c => c.Length == 0))
            {
                continue;
            }

            if (rows.Count >= MaxRows)
            {
                return new ModuleImportParse(rows, $"The file has more than {MaxRows} modules. Import it in parts.");
            }

            var input = new ModuleInput { Title = cells[0], LinkUrl = cells[1].Length == 0 ? null : cells[1] };
            var errors = ModuleService.ValidateModule(input);
            var names = cells[2].Split([';', '\n', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            rows.Add(new ModuleImportRow(r, cells[0], errors.Count == 0 ? input : null, names, errors));
        }

        return rows.Count == 0 ? new ModuleImportParse([], "The file has no modules. Fill in the rows under the column titles.") : new ModuleImportParse(rows, null);
    }
}
