namespace SEAITT_LMS.Services;

/// <summary>One column of a template, as the sheet "How to fill" tells it: where it is, if it is needed, what to write and what to use in Excel (type it, choose it from the list ...).</summary>
public sealed record GuideColumn(string Sheet, string Column, string Needed, string Write, string Use);

/// <summary>
/// The sheet "How to fill" of every Excel template. It is short and direct: four steps, then a table with a line for each column that says if it is needed, what to write in it and what
/// to use in Excel (type it, choose it from the drop-down list, type it with a semicolon between the parts). The same words are shown when the pointer is over a cell, see <see cref="XlsxSheet.Tip"/>.
/// </summary>
public static class TemplateGuide
{
    public const string SheetName = "How to fill";

    /// <summary>Adds the sheet to the workbook. <paramref name="extraSteps"/> go after the four steps every template has (the limits, what happens after the import ...).</summary>
    public static void Add(XlsxBook book, string title, IReadOnlyList<GuideColumn> columns, params string[] extraSteps)
    {
        var how = book.AddSheet(SheetName);
        how.Set(1, 1, title, XlsxStyle.Title);

        var steps = new List<string>
        {
            "1. Fill in the yellow cells. The first row shows an example: change it or delete it.",
            "2. Where a cell has an arrow, press the arrow and choose from the list. Put the pointer on a cell to read what to put in it.",
            "3. Save the file with Ctrl+S as an Excel Workbook (.xlsx).",
            "4. Press the Import button in the system and choose the file.",
        };
        steps.AddRange(extraSteps);

        var row = 3;
        foreach (var step in steps)
        {
            how.Set(row++, 1, step, XlsxStyle.Bold);
        }

        row++;
        how.Row(row++, ["Sheet", "Column", "Needed?", "What to write", "In Excel"], XlsxStyle.Header);
        foreach (var c in columns)
        {
            how.Set(row, 1, c.Sheet, XlsxStyle.Wrap).Set(row, 2, c.Column, XlsxStyle.Bold).Set(row, 3, c.Needed, XlsxStyle.Wrap).Set(row, 4, c.Write, XlsxStyle.Wrap).Set(row, 5, c.Use, XlsxStyle.Wrap);
            row++;
        }

        how.Width(1, 20).Width(2, 24).Width(3, 14).Width(4, 64).Width(5, 40);
    }

    public const string TypeIt = "Type it.";
    public const string TypeNumber = "Type a number.";
    public const string Semicolons = "Type them with a semicolon (;) between them.";
    public const string DateHow = "Type it like 2026-11-20 17:00 (year-month-day hour:minute).";
}
