using System.Globalization;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>A row of the announcements workbook: the announcement that is ready, or what is wrong with it.</summary>
public sealed record AnnouncementImportRow(int Row, string Title, AnnouncementInput? Input, IReadOnlyList<string> Errors);

public sealed record AnnouncementImportParse(IReadOnlyList<AnnouncementImportRow> Rows, string? FileError);

/// <summary>
/// The Excel workbook for the announcements of the school (the academic coordinator): the template to fill in, the export of the announcements that exist, and the reading of a filled
/// workbook (every row becomes one announcement, written like the box of the page writes one).
/// </summary>
public static class AnnouncementSheets
{
    public const int MaxRows = 100;
    private const int TemplateRows = 20;
    private static readonly string[] Headers = ["Title", "Message", "About", "For", "Last day to show"];

    public static string FileName(string what) => FileDownloads.CleanName($"{what} - Announcements.xlsx");

    public static byte[] Template() => Build([], example: true);

    public static byte[] Export(IEnumerable<AnnouncementView> announcements) => Build(announcements.ToList(), example: false);

    private static byte[] Build(IReadOnlyList<AnnouncementView> items, bool example)
    {
        var book = new XlsxBook();
        var sheet = book.AddSheet("Announcements");
        sheet.Row(1, Headers, XlsxStyle.Header);
        sheet.Width(1, 34).Width(2, 60).Width(3, 18).Width(4, 14).Width(5, 18);
        var row = 2;
        foreach (var a in items)
        {
            sheet.Set(row, 1, a.Title, XlsxStyle.InputText).Set(row, 2, a.Body, XlsxStyle.Wrap).Set(row, 3, AnnouncementCategories.Display(a.Category), XlsxStyle.InputText)
                .Set(row, 4, a.Audience, XlsxStyle.InputText).Set(row, 5, a.ExpiresOn is { } on ? on.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "", XlsxStyle.InputText);
            row++;
        }

        if (example)
        {
            sheet.Set(row, 1, "Midterm exams", XlsxStyle.InputText).Set(row, 2, "The midterm exams are on November 18 and 19. Bring a pen and your school ID.", XlsxStyle.Wrap)
                .Set(row, 3, "Exam", XlsxStyle.InputText).Set(row, 4, "Students", XlsxStyle.InputText).Set(row, 5, "2026-11-19", XlsxStyle.InputText);
            row++;
        }

        var last = Math.Max(TemplateRows + 1, items.Count + 2);
        for (; row <= last; row++)
        {
            for (var column = 1; column <= Headers.Length; column++)
            {
                sheet.Set(row, column, null, column == 2 ? XlsxStyle.Wrap : XlsxStyle.InputText);
            }
        }

        sheet.FrozenRows = 1;
        sheet.ListChoice($"C2:C{last}", AnnouncementCategories.All.Select(AnnouncementCategories.Display).ToArray());
        sheet.ListChoice($"D2:D{last}", AnnouncementAudiences.All);

        var how = book.AddSheet("How to fill");
        how.Set(1, 1, "How to fill the Announcements sheet", XlsxStyle.Title);
        var steps = new[]
        {
            "1. Type one announcement in each row. The example row can be changed or deleted.",
            $"2. Title is needed (up to {AnnouncementService.MaxTitleLength} characters). Message is what people read (up to {AnnouncementService.MaxBodyLength} characters).",
            "3. About: Exam, Holiday, Semester break, Teachers' meeting or Other (choose it from the list; Other if you leave it empty). A teachers' meeting is for the teachers only.",
            "4. For: Everyone, Students or Teachers (Everyone if you leave it empty).",
            "5. Last day to show: the day, like 2026-11-19, after which it is not shown any more. Leave it empty to show it until you delete it.",
            $"6. At most {MaxRows} rows. Save the file as an Excel Workbook (.xlsx), then import it. The people it is for are told, as when you write one by hand.",
        };
        for (var i = 0; i < steps.Length; i++)
        {
            how.Set(3 + i, 1, steps[i], XlsxStyle.Wrap);
        }

        how.Width(1, 110);
        return book.ToBytes();
    }

    private static string Key(string text) => new string(text.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    public static AnnouncementImportParse Parse(byte[] data)
    {
        IReadOnlyList<XlsxSheetData> sheets;
        try
        {
            sheets = XlsxReader.Read(data);
        }
        catch (InvalidDataException problem)
        {
            return new AnnouncementImportParse([], problem.Message);
        }

        var sheet = sheets.FirstOrDefault();
        if (sheet is null || sheet.Rows.Count == 0)
        {
            return new AnnouncementImportParse([], "The file has no rows.");
        }

        var headers = Enumerable.Range(1, 8).Select(c => sheet.Cell(1, c).Trim().ToLowerInvariant()).ToList();
        if (!headers.Contains("title") || !headers.Contains("message"))
        {
            return new AnnouncementImportParse([], "This is not the template: the first row must have the column titles (Title, Message, About ...). Download the template again.");
        }

        int Column(string name, int fallback) => headers.IndexOf(name) is >= 0 and var i ? i + 1 : fallback;
        var title = Column("title", 1);
        var message = Column("message", 2);
        var about = Column("about", 3);
        var audience = Column("for", 4);
        var until = Column("last day to show", 5);
        var categories = AnnouncementCategories.All.ToDictionary(c => Key(AnnouncementCategories.Display(c)), c => c);
        var audiences = AnnouncementAudiences.All.ToDictionary(a => Key(a), a => a);

        var rows = new List<AnnouncementImportRow>();
        for (var r = 2; r <= sheet.Rows.Count; r++)
        {
            var cells = new[] { title, message, about, audience, until }.Select(c => sheet.Cell(r, c).Trim()).ToList();
            if (cells.All(c => c.Length == 0))
            {
                continue;
            }

            if (rows.Count >= MaxRows)
            {
                return new AnnouncementImportParse(rows, $"The file has more than {MaxRows} rows. Import it in parts.");
            }

            var errors = new List<string>();
            var input = new AnnouncementInput { Title = cells[0], Body = cells[1], Category = AnnouncementCategories.Other, Audience = AnnouncementAudiences.Everyone };
            if (cells[0].Length is 0 or > AnnouncementService.MaxTitleLength)
            {
                errors.Add($"Write a title (up to {AnnouncementService.MaxTitleLength} characters).");
            }

            if (cells[1].Length is 0 or > AnnouncementService.MaxBodyLength)
            {
                errors.Add($"Write the message (up to {AnnouncementService.MaxBodyLength} characters).");
            }

            if (cells[2].Length > 0)
            {
                if (categories.TryGetValue(Key(cells[2]), out var category))
                {
                    input.Category = category;
                }
                else
                {
                    errors.Add("About must be Exam, Holiday, Semester break, Teachers' meeting or Other.");
                }
            }

            if (cells[3].Length > 0)
            {
                if (audiences.TryGetValue(Key(cells[3]), out var who))
                {
                    input.Audience = who;
                }
                else
                {
                    errors.Add("For must be Everyone, Students or Teachers.");
                }
            }

            if (cells[4].Length > 0)
            {
                if (XlsxReader.ToDate(cells[4]) is { } day)
                {
                    input.ExpiresOn = DateOnly.FromDateTime(day);
                }
                else
                {
                    errors.Add("Last day to show: write the day like 2026-11-19.");
                }
            }

            rows.Add(new AnnouncementImportRow(r, cells[0], errors.Count == 0 ? input : null, errors));
        }

        return rows.Count == 0 ? new AnnouncementImportParse([], "The file has no announcements. Fill in the rows under the column titles.") : new AnnouncementImportParse(rows, null);
    }
}
