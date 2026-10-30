using SEAITT_LMS.Components.Shared;

namespace SEAITT_LMS.Services;

/// <summary>One row of an Excel file of summative exams: the exam that is ready (or what is wrong with it).</summary>
public sealed record ExamImportRow(int Row, string Title, ExamInput? Input, IReadOnlyList<string> Errors);

public sealed record ExamImportParse(IReadOnlyList<ExamImportRow> Rows, string? FileError);

/// <summary>A summative exam as it goes into the export of a course.</summary>
public sealed record ExamExportRow(string Title, string? Instructions, decimal MaxScore, DateTime? OpensAtUtc, DateTime? DueAtUtc);

/// <summary>
/// The Excel workbook for the summative exams of a course of the library (the academic provider's "Summative exams"): one row for each exam with its title, instructions,
/// highest score and its schedule (when it opens and when it closes, Philippine time). Like the modules workbook: a template with an example and a sheet that says how to fill it,
/// an export of the exams the course has (the same workbook, to change and import again) and the import.
/// </summary>
public static class ExamSheets
{
    public const int MaxRows = 50;
    private const int TemplateRows = 15;
    private const string SheetName = "Summative exams";

    private static readonly string[] Headers = ["Title", "Instructions", "Highest score", "Opens on", "Closes on"];

    public static string FileName(string what) => FileDownloads.CleanName($"{what} - Summative exams.xlsx");

    public static byte[] Template() => Build([], example: true);

    public static byte[] Export(IEnumerable<ExamExportRow> exams) => Build(exams.ToList(), example: false);

    private static byte[] Build(IReadOnlyList<ExamExportRow> exams, bool example)
    {
        var book = new XlsxBook();
        var sheet = book.AddSheet(SheetName);
        sheet.Row(1, Headers, XlsxStyle.Header);
        sheet.Width(1, 32).Width(2, 56).Width(3, 14).Width(4, 18).Width(5, 18);
        var row = 2;
        foreach (var e in exams)
        {
            sheet.Set(row, 1, e.Title, XlsxStyle.InputText).Set(row, 2, e.Instructions ?? "", XlsxStyle.Wrap).Set(row, 3, e.MaxScore, XlsxStyle.Input)
                .Set(row, 4, ItemSheets.DateText(e.OpensAtUtc), XlsxStyle.InputText).Set(row, 5, ItemSheets.DateText(e.DueAtUtc), XlsxStyle.InputText);
            row++;
        }

        if (example)
        {
            sheet.Set(row, 1, "1st Term summative exam", XlsxStyle.InputText).Set(row, 2, "Answer all the items on the paper. Use a black or blue pen.", XlsxStyle.Wrap)
                .Set(row, 3, 100, XlsxStyle.Input).Set(row, 4, "2026-12-14 08:00", XlsxStyle.InputText).Set(row, 5, "2026-12-14 10:00", XlsxStyle.InputText);
            row++;
        }

        var last = Math.Max(TemplateRows + 1, exams.Count + 2);
        for (; row <= last; row++)
        {
            for (var column = 1; column <= Headers.Length; column++)
            {
                sheet.Set(row, column, null, column == 2 ? XlsxStyle.Wrap : column == 3 ? XlsxStyle.Input : XlsxStyle.InputText);
            }
        }

        sheet.FrozenRows = 1;
        sheet.Tip($"A2:A{last}", "Type the title of the exam (needed).").Tip($"B2:B{last}", "Type what the students must know or do.");
        sheet.Number($"C2:C{last}", 1, 1000, "Type the highest score, a number from 1 to 1000. Leave it empty for 100.");
        sheet.Tip($"D2:E{last}", "Type the day and the hour (Philippine time) like 2026-12-14 08:00. Both are needed.");

        TemplateGuide.Add(book, "How to fill the Summative exams sheet",
        [
            new(SheetName, "Title", "Needed", "The name of the exam, like 1st Term summative exam.", TemplateGuide.TypeIt),
            new(SheetName, "Instructions", "Optional", "What the students must know or do. The paper itself (a file) is added to the exam on the page.", TemplateGuide.TypeIt),
            new(SheetName, "Highest score", "Optional", "A number from 1 to 1000. Empty: 100.", TemplateGuide.TypeNumber),
            new(SheetName, "Opens on", "Needed", "The day and hour the exam opens (Philippine time).", TemplateGuide.DateHow),
            new(SheetName, "Closes on", "Needed", "The day and hour the exam closes (Philippine time). It is after it opens.", TemplateGuide.DateHow),
        ],
            $"At most {MaxRows} exams.",
            "Files (the exam paper) are added after importing, with Edit on the exam.");
        return book.ToBytes();
    }

    public static ExamImportParse Parse(byte[] data)
    {
        IReadOnlyList<XlsxSheetData> sheets;
        try
        {
            sheets = XlsxReader.Read(data);
        }
        catch (InvalidDataException problem)
        {
            return new ExamImportParse([], problem.Message);
        }

        var sheet = sheets.FirstOrDefault(s => string.Equals(s.Name.Trim(), SheetName, StringComparison.OrdinalIgnoreCase)) ?? sheets.FirstOrDefault();
        if (sheet is null || sheet.Rows.Count == 0)
        {
            return new ExamImportParse([], "The file has no rows.");
        }

        var headers = Enumerable.Range(1, 8).Select(c => sheet.Cell(1, c).Trim().ToLowerInvariant()).ToList();
        if (!headers.Contains("title"))
        {
            return new ExamImportParse([], "This is not the template: the first row must have the column titles (Title, Instructions ...). Download the template again.");
        }

        int Column(string name, int fallback) => headers.IndexOf(name) is >= 0 and var i ? i + 1 : fallback;
        var columns = new[] { Column("title", 1), Column("instructions", 2), Column("highest score", 3), Column("opens on", 4), Column("closes on", 5) };
        var rows = new List<ExamImportRow>();
        for (var r = 2; r <= sheet.Rows.Count; r++)
        {
            var cells = columns.Select(c => sheet.Cell(r, c).Trim()).ToList();
            if (cells.All(c => c.Length == 0))
            {
                continue;
            }

            if (rows.Count >= MaxRows)
            {
                return new ExamImportParse(rows, $"The file has more than {MaxRows} exams. Import it in parts.");
            }

            var errors = new List<string>();
            var input = new ExamInput { Title = cells[0], Instructions = cells[1].Length == 0 ? null : cells[1], MaxScore = 100 };
            if (cells[2].Length > 0)
            {
                if (decimal.TryParse(cells[2], System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var score))
                {
                    input.MaxScore = score;
                }
                else
                {
                    errors.Add("Highest score: write a number from 1 to 1000.");
                }
            }

            input.OpensAtUtc = ItemSheets.ParseMoment(cells[3], "Opens on", errors);
            input.DueAtUtc = ItemSheets.ParseMoment(cells[4], "Closes on", errors);
            if (errors.Count == 0)
            {
                errors.AddRange(MaterialService.ValidateExam(input));
            }

            rows.Add(new ExamImportRow(r, cells[0], errors.Count == 0 ? input : null, errors));
        }

        return rows.Count == 0 ? new ExamImportParse([], "The file has no exams. Fill in the rows under the column titles.") : new ExamImportParse(rows, null);
    }
}
