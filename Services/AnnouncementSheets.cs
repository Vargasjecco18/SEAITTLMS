using System.Globalization;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>
/// The Excel workbook for the announcements of the school (the academic coordinator): the export of the announcements that exist, with the columns Title, Message, About, For, Start and End.
/// (Announcements are written in the sheet of the system, not in an Excel template.)
/// </summary>
public static class AnnouncementSheets
{
    private static readonly string[] Headers = ["Title", "Message", "About", "For", "Start", "End"];

    public static string FileName(string what) => FileDownloads.CleanName($"{what} - Announcements.xlsx");

    public static byte[] Export(IEnumerable<AnnouncementView> announcements) => Build(announcements.ToList(), []);

    /// <summary>Round 47: the file of View announcements: the sheet Announcements and the sheet Holidays (the holidays of the year that are shown).</summary>
    public static byte[] Export(IEnumerable<AnnouncementView> announcements, IEnumerable<Holiday> holidays) => Build(announcements.ToList(), holidays.ToList());

    private static byte[] Build(IReadOnlyList<AnnouncementView> items, IReadOnlyList<Holiday> holidays)
    {
        var book = new XlsxBook();
        var sheet = book.AddSheet("Announcements");
        sheet.Row(1, Headers, XlsxStyle.Header);
        sheet.Width(1, 34).Width(2, 60).Width(3, 18).Width(4, 14).Width(5, 14).Width(6, 14);
        var row = 2;
        foreach (var a in items)
        {
            sheet.Set(row, 1, a.Title, XlsxStyle.InputText).Set(row, 2, a.Body, XlsxStyle.Wrap).Set(row, 3, AnnouncementCategories.Display(a.Category), XlsxStyle.InputText)
                .Set(row, 4, a.Audience, XlsxStyle.InputText)
                .Set(row, 5, a.StartsOn is { } from ? from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "", XlsxStyle.InputText)
                .Set(row, 6, a.ExpiresOn is { } on ? on.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "", XlsxStyle.InputText);
            row++;
        }

        sheet.FrozenRows = 1;

        if (holidays.Count > 0)
        {
            var days = book.AddSheet("Holidays");
            days.Row(1, ["Date", "Holiday", "Kind"], XlsxStyle.Header);
            days.Width(1, 16).Width(2, 40).Width(3, 30);
            var line = 2;
            foreach (var h in holidays.OrderBy(h => h.Day))
            {
                days.Set(line, 1, h.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), XlsxStyle.InputText).Set(line, 2, h.Name, XlsxStyle.InputText).Set(line, 3, h.Kind + (h.Estimated ? " (the day can change)" : ""), XlsxStyle.InputText);
                line++;
            }

            days.FrozenRows = 1;
        }

        return book.ToBytes();
    }
}
