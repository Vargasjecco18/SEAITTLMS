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

    /// <summary>One cell for each file (the first is needed), so a file is added by typing its name in the next cell; "Files" (names with a semicolon) from older workbooks is read too.</summary>
    private const int FileColumns = 5;

    private static readonly string[] Headers = ["Title", "Link", "File 1", "File 2", "File 3", "File 4", "File 5"];

    public static string FileName(string what) => FileDownloads.CleanName($"{what} - Modules.xlsx");

    public static byte[] Template() => Build([], example: true);

    public static byte[] Export(IEnumerable<ModuleExportRow> modules) => Build(modules.ToList(), example: false);

    private static byte[] Build(IReadOnlyList<ModuleExportRow> modules, bool example)
    {
        var book = new XlsxBook();
        var sheet = book.AddSheet(SheetName);
        sheet.Row(1, Headers, XlsxStyle.Header);
        sheet.Width(1, 38).Width(2, 38).Width(3, 26).Width(4, 22).Width(5, 22).Width(6, 22).Width(7, 22).Width(8, 3);
        var row = 2;
        void Write(int at, string title, string? link, IReadOnlyList<string> files)
        {
            sheet.Set(at, 1, title, XlsxStyle.InputText).Set(at, 2, link ?? "", XlsxStyle.InputText);
            for (var i = 0; i < FileColumns; i++)
            {
                sheet.Set(at, 3 + i, i < files.Count ? files[i] : null, XlsxStyle.InputText);
            }
        }

        foreach (var m in modules)
        {
            Write(row++, m.Title, m.LinkUrl, m.FileNames);
        }

        if (example)
        {
            Write(row++, "Module 1: Journal entries", "https://example.com/video", ["lesson.pdf", "worksheet.docx"]);
        }

        var last = Math.Max(TemplateRows + 1, modules.Count + 2);
        for (; row <= last; row++)
        {
            Write(row, "", null, []);
        }

        sheet.FrozenRows = 1;
        sheet.Tip($"A2:A{last}", "Type the title of the module (needed).").Tip($"B2:B{last}", "Paste an address that starts with https://  (optional: a page or a video the lesson uses).")
            .Tip($"C2:C{last}", "Type the name of the file of the module with its ending, like lesson.pdf (needed). Then choose the same file when you import.")
            .Tip($"D2:G{last}", "Another file of the same module: type its name, one name in each cell (optional).");

        TemplateGuide.StepsAtRight(sheet, 9, "Step by step",
            "1. Type the title of the module in the column Title, like Module 1: Journal entries. The first row is an example: change it or delete it.",
            "2. If the lesson uses a page or a video, paste its address in Link (it starts with https://). It is optional.",
            "3. Type the name of the file in File 1, with its ending, like lesson.pdf. Every module needs a file.",
            "4. More files? Type each name in the next cell: File 2, File 3, File 4, File 5.",
            "5. Save with Ctrl+S as an Excel Workbook (.xlsx).",
            "6. In the system press Import, choose this workbook, then choose the same files you named. There is no size limit.");

        TemplateGuide.Add(book, "How to fill the Modules sheet",
        [
            new("Modules", "Title", "Needed", "The name of the module, like Module 1: Introduction.", TemplateGuide.TypeIt),
            new("Modules", "Link", "Optional", "A page or a video the lesson uses. It starts with https://", "Paste the address."),
            new("Modules", "File 1", "Needed", "The name of the file of the module with its ending, like lesson.pdf.", "Type the name. In the import box, choose the same file."),
            new("Modules", "File 2 to File 5", "Optional", "The names of other files of the same module.", "One name in each cell."),
        ],
            $"At most {MaxRows} modules, and {AttachmentService.MaxPerOwner} files for each module.",
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

        var headers = Enumerable.Range(1, 12).Select(c => sheet.Cell(1, c).Trim().ToLowerInvariant()).ToList();
        if (!headers.Contains("title"))
        {
            return new ModuleImportParse([], "This is not the template: the first row must have the column titles (Title, Link, File 1 ...). Download the template again.");
        }

        int Column(string name, int fallback) => headers.IndexOf(name) is >= 0 and var i ? i + 1 : fallback;
        var title = Column("title", 1);
        var link = Column("link", 2);
        // File 1, File 2 ... (one name in each cell) and the older column "Files" (names with a semicolon between them).
        var fileColumns = Enumerable.Range(0, headers.Count).Where(i => headers[i] == "files" || (headers[i].StartsWith("file ", StringComparison.Ordinal) && int.TryParse(headers[i].AsSpan(5), out _))).Select(i => i + 1).ToList();
        if (fileColumns.Count == 0)
        {
            fileColumns = [3];
        }

        var rows = new List<ModuleImportRow>();
        for (var r = 2; r <= sheet.Rows.Count; r++)
        {
            var cells = new[] { title, link }.Select(c => sheet.Cell(r, c).Trim()).Append(string.Join(';', fileColumns.Select(c => sheet.Cell(r, c).Trim()).Where(c => c.Length > 0))).ToList();
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
            if (names.Count == 0)
            {
                errors = [.. errors, "Write the name of the file of the module in File 1 (a module needs a file)."];
            }
            rows.Add(new ModuleImportRow(r, cells[0], errors.Count == 0 ? input : null, names, errors));
        }

        return rows.Count == 0 ? new ModuleImportParse([], "The file has no modules. Fill in the rows under the column titles.") : new ModuleImportParse(rows, null);
    }
}
