using System.Globalization;
using SEAITT_LMS.Components.Shared;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>What a filled workbook of an evaluation says: its details and its questions, and what is wrong with them.</summary>
public sealed record EvaluationImportParse(EvaluationInput? Details, IReadOnlyList<EvaluationQuestionInput> Questions, IReadOnlyList<string> Errors, string? FileError);

/// <summary>
/// The Excel workbook of a teacher evaluation (the academic coordinator): the template to fill in, the export of an evaluation, and the reading of a filled workbook, which makes a new
/// draft evaluation. The sheet Evaluation has the title, the instructions and the due date; the sheet Questions has one row for each question, its type, its area and its options.
/// </summary>
public static class EvaluationSheets
{
    public const int MaxQuestions = 100;
    private const int TemplateRows = 20;
    private static readonly string[] QuestionHeaders = ["Question", "Type", "Area", "Options"];

    public static string FileName(string what) => FileDownloads.CleanName($"{what} - Evaluation.xlsx");

    public static byte[] Template() => Build(null, example: true);

    public static byte[] Export(EvaluationDetail evaluation) => Build(evaluation, example: false);

    private static string DateText(DateTime? utc) => utc is { } at ? PhilippineTime.ToLocal(at).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : "";

    private static byte[] Build(EvaluationDetail? evaluation, bool example)
    {
        var book = new XlsxBook();
        var details = book.AddSheet("Evaluation");
        details.Set(1, 1, "Title", XlsxStyle.Header).Set(1, 2, "What to write", XlsxStyle.Header);
        details.Set(2, 1, "Title", XlsxStyle.Bold).Set(2, 2, evaluation?.Title ?? (example ? "Teacher evaluation, 1st term" : ""), XlsxStyle.InputText);
        details.Set(3, 1, "Instructions", XlsxStyle.Bold).Set(3, 2, evaluation?.Instructions ?? (example ? "Answer honestly. Nobody sees who wrote what." : ""), XlsxStyle.Wrap);
        details.Set(4, 1, "Due date", XlsxStyle.Bold).Set(4, 2, evaluation is null ? (example ? "2026-12-05 17:00" : "") : DateText(evaluation.DueAtUtc), XlsxStyle.InputText);
        details.Set(6, 1, "Write the title, the instructions the students read and the due date (Philippine time, like 2026-12-05 17:00; it can also be chosen when the evaluation is sent). Leave the due date empty if you do not know it yet.", XlsxStyle.Note);
        details.Width(1, 16).Width(2, 80);
        details.FrozenRows = 1;

        var questions = book.AddSheet("Questions");
        questions.Row(1, QuestionHeaders, XlsxStyle.Header);
        questions.Width(1, 64).Width(2, 28).Width(3, 26).Width(4, 44);
        var row = 2;
        if (evaluation is not null)
        {
            foreach (var q in evaluation.Questions)
            {
                questions.Set(row, 1, q.Text, XlsxStyle.Wrap).Set(row, 2, EvaluationQuestionKinds.Display(q.Kind), XlsxStyle.InputText).Set(row, 3, q.Category ?? "", XlsxStyle.InputText)
                    .Set(row, 4, q.Options is { Count: > 0 } o ? string.Join("; ", o) : "", XlsxStyle.Wrap);
                row++;
            }
        }
        else if (example)
        {
            (string Text, string Kind, string Area, string Options)[] samples =
            [
                ("The teacher explains the lessons clearly.", EvaluationQuestionKinds.Likert, "Teaching", ""),
                ("The teacher is prepared for the class.", EvaluationQuestionKinds.Performance, "Teaching", ""),
                ("The teacher starts and ends the class on time.", EvaluationQuestionKinds.Frequency, "Professionalism", ""),
                ("Does the teacher return your work with comments?", EvaluationQuestionKinds.Binary, "Feedback", ""),
                ("Put these in order, the one the teacher does best first.", EvaluationQuestionKinds.Ranking, "Teaching", "Explains well; Gives examples; Answers questions; Gives feedback"),
                ("What helps you learn? Tick all that apply.", EvaluationQuestionKinds.SelectAll, "Learning", "Slides; Videos; Group work; Practice problems"),
                ("What would you like to tell your teacher?", EvaluationQuestionKinds.Comment, "Comments", ""),
            ];
            foreach (var s in samples)
            {
                questions.Set(row, 1, s.Text, XlsxStyle.Wrap).Set(row, 2, EvaluationQuestionKinds.Display(s.Kind), XlsxStyle.InputText).Set(row, 3, s.Area, XlsxStyle.InputText).Set(row, 4, s.Options, XlsxStyle.Wrap);
                row++;
            }
        }

        var last = Math.Max(TemplateRows + 1, row + 10);
        for (; row <= last; row++)
        {
            for (var column = 1; column <= QuestionHeaders.Length; column++)
            {
                questions.Set(row, column, null, column is 1 or 4 ? XlsxStyle.Wrap : XlsxStyle.InputText);
            }
        }

        questions.FrozenRows = 1;
        questions.ListChoice($"B2:B{last}", EvaluationQuestionKinds.All.Select(EvaluationQuestionKinds.Display).ToArray(), "Choose how the students answer from the list.");
        questions.Tip($"A2:A{last}", "Type the question (needed).").Tip($"C2:C{last}", "Type the group of the question, like Teaching. It can be empty.")
            .Tip($"D2:D{last}", "Only for Ranking and Select-All: type the options with a semicolon between them, like Slides; Videos; Group work.");
        details.Tip("B2", "Type the title of the questionnaire (needed).").Tip("B3", "Type what the students read before they answer.").Tip("B4", "Type the last day and hour to answer like 2026-12-05 17:00 (Philippine time), or leave it empty.");

        TemplateGuide.Add(book, "How to fill the evaluation",
        [
            new("Evaluation", "Title", "Needed", "The name of the questionnaire, like Teacher evaluation, 1st term.", TemplateGuide.TypeIt),
            new("Evaluation", "Instructions", "Optional", "What the students read before they answer.", TemplateGuide.TypeIt),
            new("Evaluation", "Due date", "Optional", "The last day and hour to answer (Philippine time). It can also be chosen when the evaluation is sent.", TemplateGuide.DateHow),
            new("Questions", "Question", "Needed", "The question the students answer.", TemplateGuide.TypeIt),
            new("Questions", "Type", "Needed", "How the students answer: 5-Point Likert Scale, 5-Point Performance Scale, Frequency Scale, Binary Scale, Ranking Scale, Select-All-That-Apply or Comment.", "Choose it from the list."),
            new("Questions", "Area", "Optional", "The group the question belongs to, like Teaching or Feedback. The results are shown by area.", TemplateGuide.TypeIt),
            new("Questions", "Options", "Ranking and Select-All only", $"The options, from {EvaluationQuestionKinds.MinOptions} to {EvaluationQuestionKinds.MaxOptions}, like Slides; Videos; Group work. Leave it empty for the other types.", TemplateGuide.Semicolons),
        ],
            $"At most {MaxQuestions} questions.",
            "A new draft evaluation is made. Open it to choose the teachers' courses and send it.");
        return book.ToBytes();
    }

    private static string Key(string text) => new string(text.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    public static EvaluationImportParse Parse(byte[] data)
    {
        IReadOnlyList<XlsxSheetData> sheets;
        try
        {
            sheets = XlsxReader.Read(data);
        }
        catch (InvalidDataException problem)
        {
            return new EvaluationImportParse(null, [], [], problem.Message);
        }

        var details = sheets.FirstOrDefault(s => string.Equals(s.Name.Trim(), "Evaluation", StringComparison.OrdinalIgnoreCase));
        var sheet = sheets.FirstOrDefault(s => string.Equals(s.Name.Trim(), "Questions", StringComparison.OrdinalIgnoreCase));
        if (details is null || sheet is null)
        {
            return new EvaluationImportParse(null, [], [], "This is not the template: it needs the sheets Evaluation and Questions. Download the template again.");
        }

        var errors = new List<string>();
        string Value(string label)
        {
            for (var r = 2; r <= Math.Min(details.Rows.Count, 12); r++)   // (row 1 has the column titles)
            {
                if (string.Equals(details.Cell(r, 1).Trim(), label, StringComparison.OrdinalIgnoreCase))
                {
                    return details.Cell(r, 2).Trim();
                }
            }

            return "";
        }

        var input = new EvaluationInput { Title = Value("Title"), Instructions = Value("Instructions") is { Length: > 0 } instructionsText ? instructionsText : null };
        if (input.Title.Length is 0 or > 200)
        {
            errors.Add("Sheet Evaluation: write the title of the evaluation (up to 200 characters).");
        }

        if (input.Instructions is { Length: > 2000 })
        {
            errors.Add("Sheet Evaluation: the instructions can have at most 2000 characters.");
        }

        var due = Value("Due date");
        if (due.Length > 0)
        {
            if (XlsxReader.ToDate(due) is { } parsed)
            {
                input.DueAtUtc = PhilippineTime.ToUtc(DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified));
            }
            else
            {
                errors.Add("Sheet Evaluation: write the due date like 2026-12-05 17:00.");
            }
        }

        var kinds = EvaluationQuestionKinds.All.ToDictionary(k => Key(EvaluationQuestionKinds.Display(k)), k => k);
        foreach (var k in EvaluationQuestionKinds.All)
        {
            kinds.TryAdd(Key(k), k);   // the short names (Likert, Binary ...) are accepted too
        }

        var questions = new List<EvaluationQuestionInput>();
        for (var r = 2; r <= sheet.Rows.Count; r++)
        {
            var text = sheet.Cell(r, 1).Trim();
            var type = sheet.Cell(r, 2).Trim();
            var area = sheet.Cell(r, 3).Trim();
            var options = sheet.Cell(r, 4).Trim();
            if (text.Length == 0 && type.Length == 0 && area.Length == 0 && options.Length == 0)
            {
                continue;
            }

            if (questions.Count >= MaxQuestions)
            {
                errors.Add($"The file has more than {MaxQuestions} questions. Import it in parts.");
                break;
            }

            var label = $"Questions, row {r}";
            if (text.Length is 0 or > 500)
            {
                errors.Add($"{label}: write the question (up to 500 characters).");
                continue;
            }

            if (!kinds.TryGetValue(Key(type), out var kind))
            {
                errors.Add($"{label}: choose the type from the list (5-Point Likert Scale, 5-Point Performance Scale, Frequency Scale, Binary Scale, Ranking Scale, Select-All-That-Apply or Comment).");
                continue;
            }

            var list = options.Split([';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            if (EvaluationQuestionKinds.HasOptions(kind))
            {
                if (list.Count < EvaluationQuestionKinds.MinOptions || list.Count > EvaluationQuestionKinds.MaxOptions)
                {
                    errors.Add($"{label}: give from {EvaluationQuestionKinds.MinOptions} to {EvaluationQuestionKinds.MaxOptions} options, separated by a semicolon.");
                    continue;
                }
            }
            else
            {
                list = [];
            }

            questions.Add(new EvaluationQuestionInput { Text = text, Kind = kind, Category = area.Length == 0 ? null : area, Options = list });
        }

        if (questions.Count == 0 && errors.Count == 0)
        {
            errors.Add("The file has no questions. Fill in the rows of the sheet Questions.");
        }

        return new EvaluationImportParse(input, questions, errors, null);
    }
}
