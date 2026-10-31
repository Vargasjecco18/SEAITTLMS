using SEAITT_LMS.Components.Shared;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>One row of an Excel file of summative exams: the exam that is ready (or what is wrong with it).</summary>
public sealed record ExamImportRow(int Row, string Title, ExamInput? Input, IReadOnlyList<string> Errors);

public sealed record ExamImportParse(IReadOnlyList<ExamImportRow> Rows, string? FileError);

/// <summary>A summative exam as it goes into the export of a course, with its questions.</summary>
public sealed record ExamExportRow(
    string Title, string? Instructions, decimal MaxScore, DateTime? OpensAtUtc, DateTime? DueAtUtc, int? TimeLimitMinutes = null, bool AntiCheat = true, IReadOnlyList<QuestionInput>? Questions = null);

/// <summary>
/// The Excel workbook for the summative exams of a course of the library (the academic provider's "Summative exams"): the sheet <b>Summative exams</b> has one row for each exam with
/// its title, instructions, schedule (when it opens and when it closes, Philippine time), time limit and the anti-cheating watch; the sheet <b>Questions</b> has the questions of each
/// exam (the column Exam says which one), written like the questions of a quiz. The students answer the questions on the screen and the system gives the score, so the highest score of an
/// exam is the total of the points of its questions. Like the modules workbook: a template with an example and a sheet that says how to fill it, an export of the exams the course has
/// (the same workbook, to change and import again) and the import.
/// </summary>
public static class ExamSheets
{
    public const int MaxRows = 50;
    private const int TemplateRows = 15;
    private const int TemplateQuestionRows = 60;
    private const string SheetName = "Summative exams";
    private const string QuestionsSheetName = "Questions";

    private static readonly string[] Headers = ["Title", "Instructions", "Highest score", "Opens on", "Closes on", "Time limit (minutes)", "Anti-cheat"];
    private static readonly string[] QuestionHeaders = ["Exam", "Question", "Type", "Choices", "Right answer", "Points"];

    public static string FileName(string what) => FileDownloads.CleanName($"{what} - Summative exams.xlsx");

    public static byte[] Template() => Build([], example: true);

    public static byte[] Export(IEnumerable<ExamExportRow> exams) => Build(exams.ToList(), example: false);

    private static string TypeText(string kind) => kind switch
    {
        QuestionKinds.TrueFalse => "True or false",
        QuestionKinds.ShortAnswer => "Short answer",
        _ => "Multiple choice",
    };

    private static byte[] Build(IReadOnlyList<ExamExportRow> exams, bool example)
    {
        var book = new XlsxBook();
        var sheet = book.AddSheet(SheetName);
        sheet.Row(1, Headers, XlsxStyle.Header);
        sheet.Width(1, 32).Width(2, 56).Width(3, 14).Width(4, 18).Width(5, 18).Width(6, 20).Width(7, 12);
        var row = 2;
        foreach (var e in exams)
        {
            sheet.Set(row, 1, e.Title, XlsxStyle.InputText).Set(row, 2, e.Instructions ?? "", XlsxStyle.Wrap).Set(row, 3, e.MaxScore, XlsxStyle.Input)
                .Set(row, 4, ItemSheets.DateText(e.OpensAtUtc), XlsxStyle.InputText).Set(row, 5, ItemSheets.DateText(e.DueAtUtc), XlsxStyle.InputText)
                .Set(row, 6, e.TimeLimitMinutes is { } limit ? limit : null, XlsxStyle.Input).Set(row, 7, e.AntiCheat ? "Yes" : "No", XlsxStyle.InputText);
            row++;
        }

        if (example)
        {
            sheet.Set(row, 1, "1st Term summative exam", XlsxStyle.InputText).Set(row, 2, "Answer all the questions. Do not leave this page while you take the exam.", XlsxStyle.Wrap)
                .Set(row, 3, null, XlsxStyle.Input).Set(row, 4, "2026-12-14 08:00", XlsxStyle.InputText).Set(row, 5, "2026-12-14 10:00", XlsxStyle.InputText)
                .Set(row, 6, 60, XlsxStyle.Input).Set(row, 7, "Yes", XlsxStyle.InputText);
            row++;
        }

        var last = Math.Max(TemplateRows + 1, exams.Count + 2);
        for (; row <= last; row++)
        {
            for (var column = 1; column <= Headers.Length; column++)
            {
                sheet.Set(row, column, null, column == 2 ? XlsxStyle.Wrap : column is 3 or 6 ? XlsxStyle.Input : XlsxStyle.InputText);
            }
        }

        sheet.FrozenRows = 1;
        sheet.Tip($"A2:A{last}", "Type the title of the exam (needed). Write the same title in the column Exam of the sheet Questions.").Tip($"B2:B{last}", "Type what the students must know or do.");
        sheet.Number($"C2:C{last}", 1, 1000, "Leave it empty when the exam has questions: the highest score is the total of their points. Without questions, type a number from 1 to 1000.");
        sheet.Tip($"D2:E{last}", "Type the day and the hour (Philippine time) like 2026-12-14 08:00. Both are needed.");
        sheet.Number($"F2:F{last}", 1, 600, "Type the minutes the student has once started, from 1 to 600. Leave it empty for no time limit.");
        sheet.ListChoice($"G2:G{last}", ["Yes", "No"], "Choose Yes to watch the student: a warning when the student leaves the exam page, copies, pastes, prints or takes a screenshot. Empty: Yes.");

        // ---- the questions of every exam
        var questions = book.AddSheet(QuestionsSheetName);
        questions.Row(1, QuestionHeaders, XlsxStyle.Header);
        questions.Width(1, 32).Width(2, 50).Width(3, 16).Width(4, 44).Width(5, 24).Width(6, 8);
        var qRow = 2;
        foreach (var exam in exams)
        {
            foreach (var q in exam.Questions ?? [])
            {
                questions.Set(qRow, 1, exam.Title, XlsxStyle.InputText).Set(qRow, 2, q.Text, XlsxStyle.Wrap).Set(qRow, 3, TypeText(q.Kind), XlsxStyle.InputText)
                    .Set(qRow, 4, q.Kind == QuestionKinds.MultipleChoice ? string.Join("; ", ModuleService.SplitOptions(q.OptionsText)) : "", XlsxStyle.Wrap)
                    .Set(qRow, 5, q.CorrectAnswer, XlsxStyle.InputText).Set(qRow, 6, q.Points, XlsxStyle.Input);
                qRow++;
            }
        }

        if (example)
        {
            const string title = "1st Term summative exam";
            questions.Set(qRow, 1, title, XlsxStyle.InputText).Set(qRow, 2, "Which account is increased by a debit?", XlsxStyle.Wrap).Set(qRow, 3, "Multiple choice", XlsxStyle.InputText)
                .Set(qRow, 4, "Asset; Liability; Income", XlsxStyle.Wrap).Set(qRow, 5, "Asset", XlsxStyle.InputText).Set(qRow, 6, 2, XlsxStyle.Input);
            qRow++;
            questions.Set(qRow, 1, title, XlsxStyle.InputText).Set(qRow, 2, "Every debit has an equal credit.", XlsxStyle.Wrap).Set(qRow, 3, "True or false", XlsxStyle.InputText)
                .Set(qRow, 4, "", XlsxStyle.Wrap).Set(qRow, 5, "True", XlsxStyle.InputText).Set(qRow, 6, 1, XlsxStyle.Input);
            qRow++;
            questions.Set(qRow, 1, title, XlsxStyle.InputText).Set(qRow, 2, "What is the left side of the accounting equation?", XlsxStyle.Wrap).Set(qRow, 3, "Short answer", XlsxStyle.InputText)
                .Set(qRow, 4, "", XlsxStyle.Wrap).Set(qRow, 5, "Assets", XlsxStyle.InputText).Set(qRow, 6, 1, XlsxStyle.Input);
            qRow++;
        }

        var lastQuestion = Math.Max(TemplateQuestionRows + 1, qRow + 20);
        for (; qRow <= lastQuestion; qRow++)
        {
            for (var column = 1; column <= QuestionHeaders.Length; column++)
            {
                questions.Set(qRow, column, null, column is 2 or 4 ? XlsxStyle.Wrap : column == 6 ? XlsxStyle.Input : XlsxStyle.InputText);
            }
        }

        questions.FrozenRows = 1;
        questions.ListChoice($"C2:C{lastQuestion}", ["Multiple choice", "True or false", "Short answer"], "Choose the type from the list.");
        questions.Tip($"A2:A{lastQuestion}", "Type the title of the exam this question belongs to, the same as in the sheet Summative exams.").Tip($"B2:B{lastQuestion}", "Type the question (needed).")
            .Tip($"D2:D{lastQuestion}", "Multiple choice only: type the choices with a semicolon between them, like Asset; Liability; Income.")
            .Tip($"E2:E{lastQuestion}", "Type the right answer: the right choice (or its letter), True or False, or the answer. Other accepted answers go after a | sign.");
        questions.Number($"F2:F{lastQuestion}", 0.01m, 100, "Type the points, from 0.01 to 100. Leave it empty for 1.");

        TemplateGuide.Add(book, "How to fill the Summative exams and Questions sheets",
        [
            new(SheetName, "Title", "Needed", "The name of the exam, like 1st Term summative exam.", TemplateGuide.TypeIt),
            new(SheetName, "Instructions", "Optional", "What the students must know or do. A file (the paper) can be added to the exam on the page.", TemplateGuide.TypeIt),
            new(SheetName, "Highest score", "Optional", "Leave it empty when the exam has questions: it is the total of their points. Without questions: a number from 1 to 1000 (empty: 100).", TemplateGuide.TypeNumber),
            new(SheetName, "Opens on", "Needed", "The day and hour the exam opens (Philippine time).", TemplateGuide.DateHow),
            new(SheetName, "Closes on", "Needed", "The day and hour the exam closes (Philippine time). It is after it opens.", TemplateGuide.DateHow),
            new(SheetName, "Time limit (minutes)", "Optional", "How long a student has once started, from 1 to 600. Empty: no limit.", TemplateGuide.TypeNumber),
            new(SheetName, "Anti-cheat", "Optional", "Yes: the student is warned when leaving the exam page, copying, pasting, printing or taking a screenshot; after 3 warnings the exam is handed in. Empty: Yes.", "Choose it from the list."),
            new(QuestionsSheetName, "Exam", "Needed", "The title of the exam the question belongs to, the same as in the sheet Summative exams.", TemplateGuide.TypeIt),
            new(QuestionsSheetName, "Question", "Needed", "The question the students read.", TemplateGuide.TypeIt),
            new(QuestionsSheetName, "Type", "Needed", "Multiple choice, True or false, or Short answer.", "Choose it from the list."),
            new(QuestionsSheetName, "Choices", "Multiple choice only", "The choices, at least two, like Asset; Liability; Income.", TemplateGuide.Semicolons),
            new(QuestionsSheetName, "Right answer", "Needed", "Multiple choice: the right choice (or its letter, A for the first). True or false: True or False. Short answer: the answer; other accepted answers go after a | sign.", TemplateGuide.TypeIt),
            new(QuestionsSheetName, "Points", "Optional", "From 0.01 to 100. Empty: 1.", TemplateGuide.TypeNumber),
        ],
            $"At most {MaxRows} exams and {ItemSheets.MaxQuestions} questions for each.",
            "The students answer the questions on the screen and the system gives the score. Files (the paper) are added after importing, with Edit on the exam.");
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

        var headers = Enumerable.Range(1, 10).Select(c => sheet.Cell(1, c).Trim().ToLowerInvariant()).ToList();
        if (!headers.Contains("title"))
        {
            return new ExamImportParse([], "This is not the template: the first row must have the column titles (Title, Instructions ...). Download the template again.");
        }

        int Column(string name, int fallback) => headers.IndexOf(name) is >= 0 and var i ? i + 1 : fallback;
        var columns = new[] { Column("title", 1), Column("instructions", 2), Column("highest score", 3), Column("opens on", 4), Column("closes on", 5), Column("time limit (minutes)", 6), Column("anti-cheat", 7) };
        var questionsByExam = ReadQuestions(sheets, out var questionErrors);

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
            if (questionsByExam.TryGetValue(cells[0].ToLowerInvariant(), out var questions) && questions.Count > 0)
            {
                input.Questions = questions;
                input.MaxScore = questions.Sum(q => q.Points);
            }
            else if (cells[2].Length > 0)
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

            if (cells[5].Length > 0)
            {
                if (int.TryParse(cells[5], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var minutes))
                {
                    input.TimeLimitMinutes = minutes;
                }
                else
                {
                    errors.Add("Time limit: write the minutes as a number, or leave it empty.");
                }
            }

            input.AntiCheat = !cells[6].Equals("no", StringComparison.OrdinalIgnoreCase);
            if (questionErrors.TryGetValue(cells[0].ToLowerInvariant(), out var problems))
            {
                errors.AddRange(problems);
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

    /// <summary>The questions of the sheet Questions, by the title of the exam (lower case); the problems of a question are told under the exam it belongs to.</summary>
    private static Dictionary<string, List<QuestionInput>> ReadQuestions(IReadOnlyList<XlsxSheetData> sheets, out Dictionary<string, List<string>> errors)
    {
        var result = new Dictionary<string, List<QuestionInput>>();
        errors = [];
        var sheet = sheets.FirstOrDefault(s => string.Equals(s.Name.Trim(), QuestionsSheetName, StringComparison.OrdinalIgnoreCase));
        if (sheet is null || sheet.Rows.Count < 2)
        {
            return result;
        }

        for (var r = 2; r <= sheet.Rows.Count; r++)
        {
            var exam = sheet.Cell(r, 1).Trim();
            var text = sheet.Cell(r, 2).Trim();
            var type = sheet.Cell(r, 3).Trim();
            var choices = sheet.Cell(r, 4).Trim();
            var answer = sheet.Cell(r, 5).Trim();
            var points = sheet.Cell(r, 6).Trim();
            if (exam.Length == 0 && text.Length == 0 && type.Length == 0 && choices.Length == 0 && answer.Length == 0 && points.Length == 0)
            {
                continue;
            }

            var key = exam.ToLowerInvariant();
            if (exam.Length == 0)
            {
                continue;
            }

            var (question, problems) = ItemSheets.ParseQuestionRow(text, type, choices, answer, points);
            if (question is null)
            {
                if (!errors.TryGetValue(key, out var list))
                {
                    errors[key] = list = [];
                }

                list.AddRange(problems.Select(p => $"Questions, row {r}: {p}"));
                continue;
            }

            if (!result.TryGetValue(key, out var questions))
            {
                result[key] = questions = [];
            }

            if (questions.Count < ItemSheets.MaxQuestions)
            {
                questions.Add(question);
            }
        }

        return result;
    }
}
