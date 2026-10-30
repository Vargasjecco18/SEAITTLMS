using System.Globalization;
using SEAITT_LMS.Components.Shared;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>One row of an Excel file of assignments, quizzes or task performances: what it holds and what is wrong with it.</summary>
public sealed record ItemImportRow(int Row, ItemInput? Input, IReadOnlyList<string> Errors, string Title = "");

public sealed record ItemImportParse(IReadOnlyList<ItemImportRow> Rows, string? FileError);

/// <summary>An assignment, quiz or task performance as it goes into an Excel file (the export of a module).</summary>
public sealed record ItemExportRow(string Title, string? Instructions, decimal MaxScore, DateTime? VisibleFromUtc, DateTime? DueAtUtc, int? TimeLimitMinutes);

/// <summary>The questions of a quiz as they go into an Excel file.</summary>
public sealed record QuestionImportParse(IReadOnlyList<QuestionInput> Questions, IReadOnlyList<string> Errors, string? FileError);

/// <summary>
/// The Excel files for assignments, quizzes and task performances. The template has the column titles, an example and a sheet that says how to fill it in; the teacher
/// (or the academic provider) fills it in and imports it, and every row becomes one item. The export of a module is the same file, so what was exported can be
/// changed and imported again. A quiz has a second kind of file: its questions (the template, the export and the import of the question editor).
/// </summary>
public static class ItemSheets
{
    public const int MaxRows = 100;
    public const int MaxQuestions = 200;
    private const int TemplateRows = 30;

    private static readonly string[] WorkHeaders = ["Title", "Instructions", "Highest score", "Appears on", "Due date"];
    private static readonly string[] QuizHeaders = ["Title", "Instructions", "Time limit (minutes)", "Appears on", "Due date"];
    private static readonly string[] QuestionHeaders = ["Question", "Type", "Choices", "Right answer", "Points"];

    private static string[] HeadersOf(string kind) => kind == ItemKinds.Quiz ? QuizHeaders : WorkHeaders;

    private static string DateText(DateTime? utc) => utc is { } at ? PhilippineTime.ToLocal(at).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : "";

    // ======================================================================= assignments, quizzes, task performances

    public static string FileName(string kind, string what) => FileDownloads.CleanName($"{what} - {ItemKinds.Plural(kind)}.xlsx");

    public static byte[] Template(string kind) => Build(kind, [], example: true);

    public static byte[] Export(string kind, IEnumerable<ItemExportRow> items) => Build(kind, items, example: false);

    private static byte[] Build(string kind, IEnumerable<ItemExportRow> items, bool example)
    {
        var headers = HeadersOf(kind);
        var isQuiz = kind == ItemKinds.Quiz;
        var book = new XlsxBook();
        var sheet = book.AddSheet(ItemKinds.Plural(kind));
        sheet.Row(1, headers, XlsxStyle.Header);
        sheet.Width(1, 30).Width(2, 50).Width(3, isQuiz ? 20 : 14).Width(4, 18).Width(5, 18);

        var rows = items.ToList();
        var row = 2;
        foreach (var item in rows)
        {
            sheet.Set(row, 1, item.Title, XlsxStyle.InputText).Set(row, 2, item.Instructions ?? "", XlsxStyle.Wrap);
            if (isQuiz)
            {
                sheet.Set(row, 3, item.TimeLimitMinutes, XlsxStyle.Input);
            }
            else
            {
                sheet.Set(row, 3, item.MaxScore, XlsxStyle.Input);
            }

            sheet.Set(row, 4, DateText(item.VisibleFromUtc), XlsxStyle.InputText).Set(row, 5, DateText(item.DueAtUtc), XlsxStyle.InputText);
            row++;
        }

        if (example)
        {
            sheet.Set(row, 1, isQuiz ? "Quiz 1: Journal entries" : $"{ItemKinds.Display(kind)} 1: Journal entries", XlsxStyle.InputText)
                .Set(row, 2, "Read the lesson, then answer the questions.", XlsxStyle.Wrap)
                .Set(row, 3, isQuiz ? 30 : 50, XlsxStyle.Input)
                .Set(row, 4, "", XlsxStyle.InputText)
                .Set(row, 5, "2026-11-20 17:00", XlsxStyle.InputText);
            row++;
        }

        for (; row <= Math.Max(TemplateRows + 1, rows.Count + 2); row++)
        {
            for (var column = 1; column <= headers.Length; column++)
            {
                sheet.Set(row, column, null, column is 1 or 4 or 5 ? XlsxStyle.InputText : XlsxStyle.Input);
            }
        }

        sheet.FrozenRows = 1;

        var lastRow = Math.Max(TemplateRows + 1, rows.Count + 2);
        sheet.Tip($"A2:A{lastRow}", "Type the title (needed).").Tip($"B2:B{lastRow}", "Type what the students must do or read.");
        sheet.Number($"C2:C{lastRow}", 1, isQuiz ? 600 : 1000, isQuiz ? "Type the minutes a student has to answer, from 1 to 600. Leave it empty for no limit." : "Type the highest score, a number from 1 to 1000. Leave it empty for 50.");
        sheet.Tip($"D2:E{lastRow}", "Type the day and the hour (Philippine time) like 2026-11-20 17:00. Leave it empty if it has none.");
        TemplateGuide.Add(book, $"How to fill the {ItemKinds.Plural(kind)} sheet",
        [
            new(ItemKinds.Plural(kind), "Title", "Needed", $"The name of the {ItemKinds.Display(kind).ToLowerInvariant()}.", TemplateGuide.TypeIt),
            new(ItemKinds.Plural(kind), "Instructions", "Optional", "What the students must do or read.", TemplateGuide.TypeIt),
            isQuiz
                ? new(ItemKinds.Plural(kind), "Time limit (minutes)", "Optional", "The minutes a student has once they start. Empty: no limit. The quiz is worth the points of its questions, which you write after importing.", TemplateGuide.TypeNumber)
                : new(ItemKinds.Plural(kind), "Highest score", "Optional", "A number from 1 to 1000. Empty: 50.", TemplateGuide.TypeNumber),
            new(ItemKinds.Plural(kind), "Appears on", "Optional", "The day and hour the students can see it (Philippine time). Empty: it appears with its module.", TemplateGuide.DateHow),
            new(ItemKinds.Plural(kind), "Due date", "Optional", "The last day and hour to hand it in (Philippine time). Empty: no due date.", TemplateGuide.DateHow),
        ],
            $"At most {MaxRows} rows.");
        return book.ToBytes();
    }

    /// <summary>Reads an Excel file of items (the template, or an export). The rows that are empty are skipped; each other row is checked like the box of the page checks an item.</summary>
    public static ItemImportParse Parse(byte[] data, string kind)
    {
        IReadOnlyList<XlsxSheetData> sheets;
        try
        {
            sheets = XlsxReader.Read(data);
        }
        catch (InvalidDataException problem)
        {
            return new ItemImportParse([], problem.Message);
        }

        var sheet = sheets.FirstOrDefault();
        if (sheet is null || sheet.Rows.Count == 0)
        {
            return new ItemImportParse([], "The file has no rows.");
        }

        var isQuiz = kind == ItemKinds.Quiz;
        var headers = Enumerable.Range(1, 8).Select(c => sheet.Cell(1, c).Trim().ToLowerInvariant()).ToList();
        int Column(string name, int fallback) => headers.IndexOf(name) is >= 0 and var i ? i + 1 : fallback;
        var title = Column("title", 1);
        var instructions = Column("instructions", 2);
        var third = isQuiz ? Column("time limit (minutes)", 3) : Column("highest score", 3);
        var appears = Column("appears on", 4);
        var due = Column("due date", 5);
        if (!headers.Contains("title"))
        {
            return new ItemImportParse([], "This is not the template: the first row must have the column titles (Title, Instructions ...). Download the template again.");
        }

        var rows = new List<ItemImportRow>();
        for (var r = 2; r <= sheet.Rows.Count; r++)
        {
            var cells = new[] { title, instructions, third, appears, due }.Select(c => sheet.Cell(r, c).Trim()).ToList();
            if (cells.All(c => c.Length == 0))
            {
                continue;
            }

            if (rows.Count >= MaxRows)
            {
                return new ItemImportParse(rows, $"The file has more than {MaxRows} rows. Import it in parts.");
            }

            var errors = new List<string>();
            var input = new ItemInput { Kind = kind, Title = cells[0], Instructions = cells[1].Length == 0 ? null : cells[1], MaxScore = kind == ItemKinds.TaskPerformance ? 100 : 50 };

            if (cells[2].Length > 0)
            {
                if (!decimal.TryParse(cells[2], NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                {
                    errors.Add(isQuiz ? "Time limit must be a number of minutes." : "Highest score must be a number.");
                }
                else if (isQuiz)
                {
                    input.TimeLimitMinutes = (int)Math.Round(number);
                }
                else
                {
                    input.MaxScore = number;
                }
            }

            input.VisibleFromUtc = ParseMoment(cells[3], "Appears on", errors);
            input.DueAtUtc = ParseMoment(cells[4], "Due date", errors);

            if (errors.Count == 0)
            {
                errors.AddRange(ModuleService.ValidateItem(input));
            }

            rows.Add(new ItemImportRow(r, errors.Count == 0 ? input : null, errors, cells[0]));
        }

        return rows.Count == 0 ? new ItemImportParse([], "The file has no items. Fill in the rows under the column titles.") : new ItemImportParse(rows, null);
    }

    private static DateTime? ParseMoment(string text, string label, List<string> errors)
    {
        if (text.Length == 0)
        {
            return null;
        }

        var parsed = XlsxReader.ToDate(text);
        if (parsed is null)
        {
            errors.Add($"{label}: write the day and time like 2026-11-20 17:00.");
            return null;
        }

        return PhilippineTime.ToUtc(DateTime.SpecifyKind(parsed.Value, DateTimeKind.Unspecified));
    }

    // ======================================================================= the questions of a quiz

    public static string QuestionsFileName(string quizTitle) => FileDownloads.CleanName($"{quizTitle} - questions.xlsx");

    public static byte[] QuestionTemplate() => BuildQuestions([], example: true);

    public static byte[] ExportQuestions(IEnumerable<QuestionInput> questions) => BuildQuestions(questions.ToList(), example: false);

    private static string TypeText(string kind) => kind switch
    {
        QuestionKinds.TrueFalse => "True or false",
        QuestionKinds.ShortAnswer => "Short answer",
        _ => "Multiple choice",
    };

    private static byte[] BuildQuestions(IReadOnlyList<QuestionInput> questions, bool example)
    {
        var book = new XlsxBook();
        var sheet = book.AddSheet("Questions");
        sheet.Row(1, QuestionHeaders, XlsxStyle.Header);
        sheet.Width(1, 50).Width(2, 16).Width(3, 44).Width(4, 24).Width(5, 8);
        var row = 2;
        foreach (var q in questions)
        {
            sheet.Set(row, 1, q.Text, XlsxStyle.Wrap).Set(row, 2, TypeText(q.Kind), XlsxStyle.InputText)
                .Set(row, 3, q.Kind == QuestionKinds.MultipleChoice ? string.Join("; ", ModuleService.SplitOptions(q.OptionsText)) : "", XlsxStyle.Wrap)
                .Set(row, 4, q.CorrectAnswer, XlsxStyle.InputText).Set(row, 5, q.Points, XlsxStyle.Input);
            row++;
        }

        if (example)
        {
            sheet.Set(row, 1, "Which account is increased by a debit?", XlsxStyle.Wrap).Set(row, 2, "Multiple choice", XlsxStyle.InputText)
                .Set(row, 3, "Asset; Liability; Income", XlsxStyle.Wrap).Set(row, 4, "Asset", XlsxStyle.InputText).Set(row, 5, 2, XlsxStyle.Input);
            row++;
            sheet.Set(row, 1, "Every debit has an equal credit.", XlsxStyle.Wrap).Set(row, 2, "True or false", XlsxStyle.InputText)
                .Set(row, 3, "", XlsxStyle.Wrap).Set(row, 4, "True", XlsxStyle.InputText).Set(row, 5, 1, XlsxStyle.Input);
            row++;
            sheet.Set(row, 1, "What is the left side of the accounting equation?", XlsxStyle.Wrap).Set(row, 2, "Short answer", XlsxStyle.InputText)
                .Set(row, 3, "", XlsxStyle.Wrap).Set(row, 4, "Assets", XlsxStyle.InputText).Set(row, 5, 1, XlsxStyle.Input);
            row++;
        }

        for (; row <= Math.Max(TemplateRows + 1, questions.Count + 2); row++)
        {
            for (var column = 1; column <= QuestionHeaders.Length; column++)
            {
                sheet.Set(row, column, null, column is 1 or 3 ? XlsxStyle.Wrap : column is 2 or 4 ? XlsxStyle.InputText : XlsxStyle.Input);
            }
        }

        sheet.FrozenRows = 1;
        sheet.ListChoice($"B2:B{Math.Max(TemplateRows + 1, questions.Count + 2)}", ["Multiple choice", "True or false", "Short answer"], "Choose the type from the list.");

        var lastQuestion = Math.Max(TemplateRows + 1, questions.Count + 2);
        sheet.Tip($"A2:A{lastQuestion}", "Type the question (needed).").Tip($"C2:C{lastQuestion}", "Multiple choice only: type the choices with a semicolon between them, like Asset; Liability; Income.")
            .Tip($"D2:D{lastQuestion}", "Type the right answer: the right choice (or its letter), True or False, or the answer. Other accepted answers go after a | sign.");
        sheet.Number($"E2:E{lastQuestion}", 0.01m, 100, "Type the points, from 0.01 to 100. Leave it empty for 1.");
        TemplateGuide.Add(book, "How to fill the Questions sheet",
        [
            new("Questions", "Question", "Needed", "The question the students read.", TemplateGuide.TypeIt),
            new("Questions", "Type", "Needed", "Multiple choice, True or false, or Short answer.", "Choose it from the list."),
            new("Questions", "Choices", "Multiple choice only", "The choices, at least two, like Asset; Liability; Income.", TemplateGuide.Semicolons),
            new("Questions", "Right answer", "Needed", "Multiple choice: the right choice (or its letter, A for the first). True or false: True or False. Short answer: the answer; other accepted answers go after a | sign.", TemplateGuide.TypeIt),
            new("Questions", "Points", "Optional", "From 0.01 to 100. Empty: 1.", TemplateGuide.TypeNumber),
        ],
            $"At most {MaxQuestions} questions.",
            "The questions are added to the list; then press Save questions.");
        return book.ToBytes();
    }

    /// <summary>
    /// One question of a sheet: the questions sheet of a quiz and the sheet Quiz Questions of the workbook of activities follow the same rules. Gives the question, or what is wrong with it.
    /// </summary>
    public static (QuestionInput? Question, IReadOnlyList<string> Errors) ParseQuestionRow(string text, string type, string choices, string answer, string points)
    {
        var kind = type.ToLowerInvariant().Replace(" ", "").Replace("-", "") switch
        {
            "multiplechoice" or "multiple" or "mc" or "" => QuestionKinds.MultipleChoice,
            "trueorfalse" or "truefalse" or "tf" or "t/f" => QuestionKinds.TrueFalse,
            "shortanswer" or "short" or "sa" => QuestionKinds.ShortAnswer,
            _ => null,
        };
        if (kind is null)
        {
            return (null, ["the type must be Multiple choice, True or false, or Short answer."]);
        }

        var pointsValue = 1m;
        if (points.Length > 0 && !decimal.TryParse(points, NumberStyles.Number, CultureInfo.InvariantCulture, out pointsValue))
        {
            return (null, ["the points must be a number."]);
        }

        var question = new QuestionInput { Text = text, Kind = kind, Points = pointsValue };
        if (kind == QuestionKinds.MultipleChoice)
        {
            var options = choices.Split([';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            question.OptionsText = string.Join('\n', options);
            // The right answer is the text of the right choice, or its letter (A is the first).
            var byText = options.FirstOrDefault(o => string.Equals(o, answer, StringComparison.OrdinalIgnoreCase));
            question.CorrectAnswer = byText ?? (answer.Length == 1 && char.ToUpperInvariant(answer[0]) - 'A' is >= 0 and var index && index < options.Count ? options[index] : answer);
        }
        else if (kind == QuestionKinds.TrueFalse)
        {
            question.CorrectAnswer = answer.ToLowerInvariant() switch { "true" or "t" or "yes" => "True", "false" or "f" or "no" => "False", _ => answer };
        }
        else
        {
            question.CorrectAnswer = answer;
        }

        var found = ModuleService.ValidateQuestions([question]);
        if (found.Count > 0)
        {
            return (null, [.. found.Select(e => e.Replace("Question 1: ", ""))]);
        }

        return (question, []);
    }

    /// <summary>Reads the questions of an Excel file (the template, or an export of a quiz).</summary>
    public static QuestionImportParse ParseQuestions(byte[] data)
    {
        IReadOnlyList<XlsxSheetData> sheets;
        try
        {
            sheets = XlsxReader.Read(data);
        }
        catch (InvalidDataException problem)
        {
            return new QuestionImportParse([], [], problem.Message);
        }

        var sheet = sheets.FirstOrDefault();
        if (sheet is null || !sheet.Cell(1, 1).Trim().Equals("Question", StringComparison.OrdinalIgnoreCase))
        {
            return new QuestionImportParse([], [], "This is not the template: the first row must have the column titles (Question, Type, Choices ...). Download the template again.");
        }

        var questions = new List<QuestionInput>();
        var errors = new List<string>();
        for (var r = 2; r <= sheet.Rows.Count; r++)
        {
            var text = sheet.Cell(r, 1).Trim();
            var type = sheet.Cell(r, 2).Trim();
            var choices = sheet.Cell(r, 3).Trim();
            var answer = sheet.Cell(r, 4).Trim();
            var points = sheet.Cell(r, 5).Trim();
            if (text.Length == 0 && type.Length == 0 && choices.Length == 0 && answer.Length == 0 && points.Length == 0)
            {
                continue;
            }

            if (questions.Count >= MaxQuestions)
            {
                return new QuestionImportParse(questions, errors, $"The file has more than {MaxQuestions} questions. Import it in parts.");
            }

            var label = $"Row {r}";
            var (question, problems) = ParseQuestionRow(text, type, choices, answer, points);
            if (question is null)
            {
                errors.AddRange(problems.Select(p => $"{label}: {p}"));
                continue;
            }

            questions.Add(question);
        }

        return questions.Count == 0 && errors.Count == 0
            ? new QuestionImportParse([], [], "The file has no questions. Fill in the rows under the column titles.")
            : new QuestionImportParse(questions, errors, null);
    }
}
