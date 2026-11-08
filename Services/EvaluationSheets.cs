using System.Globalization;
using SEAITT_LMS.Components.Shared;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>
/// The Excel workbook of a teacher evaluation (the academic coordinator): the export of an evaluation. The sheet Evaluation has the title, the instructions and the due date; the sheet
/// Questions has one row for each question, its type, its area and its options. (The questions are written in the sheet of the system, not in an Excel template.)
/// </summary>
public static class EvaluationSheets
{
    private static readonly string[] QuestionHeaders = ["Question", "Type", "Area", "Options"];

    public static string FileName(string what) => FileDownloads.CleanName($"{what} - Evaluation.xlsx");

    public static byte[] Export(EvaluationDetail evaluation) => Build(evaluation);

    private static string DateText(DateTime? utc) => utc is { } at ? PhilippineTime.ToLocal(at).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : "";

    private static byte[] Build(EvaluationDetail evaluation)
    {
        var book = new XlsxBook();
        var details = book.AddSheet("Evaluation");
        details.Set(1, 1, "Title", XlsxStyle.Header).Set(1, 2, "Evaluation", XlsxStyle.Header);
        details.Set(2, 1, "Title", XlsxStyle.Bold).Set(2, 2, evaluation.Title, XlsxStyle.InputText);
        details.Set(3, 1, "Instructions", XlsxStyle.Bold).Set(3, 2, evaluation.Instructions ?? "", XlsxStyle.Wrap);
        details.Set(4, 1, "Due date", XlsxStyle.Bold).Set(4, 2, DateText(evaluation.DueAtUtc), XlsxStyle.InputText);
        details.Width(1, 16).Width(2, 80);
        details.FrozenRows = 1;

        var questions = book.AddSheet("Questions");
        questions.Row(1, QuestionHeaders, XlsxStyle.Header);
        questions.Width(1, 64).Width(2, 28).Width(3, 26).Width(4, 44);
        var row = 2;
        foreach (var q in evaluation.Questions)
        {
            questions.Set(row, 1, q.Text, XlsxStyle.Wrap).Set(row, 2, EvaluationQuestionKinds.Display(q.Kind), XlsxStyle.InputText).Set(row, 3, q.Category ?? "", XlsxStyle.InputText)
                .Set(row, 4, q.Options is { Count: > 0 } o ? string.Join("; ", o) : "", XlsxStyle.Wrap);
            row++;
        }

        questions.FrozenRows = 1;
        return book.ToBytes();
    }
}
