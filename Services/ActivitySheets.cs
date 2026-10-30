using System.Globalization;
using SEAITT_LMS.Components.Shared;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>One thing to add to a module that the workbook describes: an assignment, a quiz (with its questions) or a task performance, and the files it names.</summary>
public sealed record ActivityWork(string Kind, ItemInput Input, IReadOnlyList<QuestionInput> Questions, IReadOnlyList<string> FileNames);

/// <summary>A row of the workbook: where it is (sheet and row), its title, and either the work that is ready or what is wrong with it.</summary>
public sealed record ActivityImportRow(string Sheet, int Row, string Title, ActivityWork? Work, IReadOnlyList<string> Errors);

public sealed record ActivityImportParse(IReadOnlyList<ActivityImportRow> Rows, string? FileError)
{
    /// <summary>Every file name the rows that are ready ask for (the Files column), without repeats.</summary>
    public IReadOnlyList<string> FileNamesWanted => Rows.Where(r => r.Work is not null).SelectMany(r => r.Work!.FileNames).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}

/// <summary>What a module has now, for the export: the work with its questions and the names of its files.</summary>
public sealed record ActivityExportItem(string Kind, ItemExportRow Row, IReadOnlyList<QuestionInput> Questions, IReadOnlyList<string> FileNames);

/// <summary>
/// The one Excel workbook for the assignments, quizzes and task performances of a module (the teacher's and the academic provider's "Add Activities"): the sheets Assignments, Quizzes,
/// Quiz Questions and Task Performances, with the column Files to name the files that go with each one (they are chosen when the workbook is imported, and matched by name). The
/// questions of a quiz are connected to it by the title of the quiz: the column Quiz of the sheet Quiz Questions. The export of a module is the same workbook.
/// </summary>
public static class ActivitySheets
{
    public const int MaxRows = ItemSheets.MaxRows;

    public const string AssignmentsSheet = "Assignments";
    public const string QuizzesSheet = "Quizzes";
    public const string QuestionsSheet = "Quiz Questions";
    public const string TasksSheet = "Task Performances";

    private const int TemplateRows = 20;

    private static readonly string[] WorkHeaders = ["Title", "Instructions", "Highest score", "Appears on", "Due date", "Files"];
    private static readonly string[] QuizHeaders = ["Title", "Instructions", "Time limit (minutes)", "Appears on", "Due date", "Files"];
    private static readonly string[] QuestionHeaders = ["Quiz", "Question", "Type", "Choices", "Right answer", "Points"];

    public static string FileName(string what) => FileDownloads.CleanName($"{what} - Activities.xlsx");

    public static byte[] Template() => Build([], example: true);

    public static byte[] Export(IEnumerable<ActivityExportItem> items) => Build(items.ToList(), example: false);

    private static string DateText(DateTime? utc) => utc is { } at ? PhilippineTime.ToLocal(at).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : "";

    private static string TypeText(string kind) => kind switch
    {
        QuestionKinds.TrueFalse => "True or false",
        QuestionKinds.ShortAnswer => "Short answer",
        _ => "Multiple choice",
    };

    private static byte[] Build(IReadOnlyList<ActivityExportItem> items, bool example)
    {
        var book = new XlsxBook();

        void WorkSheet(string name, string kind)
        {
            var isQuiz = kind == ItemKinds.Quiz;
            var sheet = book.AddSheet(name);
            sheet.Row(1, isQuiz ? QuizHeaders : WorkHeaders, XlsxStyle.Header);
            sheet.Width(1, 32).Width(2, 46).Width(3, isQuiz ? 20 : 14).Width(4, 18).Width(5, 18).Width(6, 36);
            var row = 2;
            foreach (var item in items.Where(i => i.Kind == kind))
            {
                sheet.Set(row, 1, item.Row.Title, XlsxStyle.InputText).Set(row, 2, item.Row.Instructions ?? "", XlsxStyle.Wrap)
                    .Set(row, 3, isQuiz ? item.Row.TimeLimitMinutes : item.Row.MaxScore, XlsxStyle.Input)
                    .Set(row, 4, DateText(item.Row.VisibleFromUtc), XlsxStyle.InputText).Set(row, 5, DateText(item.Row.DueAtUtc), XlsxStyle.InputText)
                    .Set(row, 6, string.Join("; ", item.FileNames), XlsxStyle.InputText);
                row++;
            }

            if (example)
            {
                sheet.Set(row, 1, isQuiz ? "Quiz 1: Debits and credits" : $"{ItemKinds.Display(kind)} 1: Journal entries", XlsxStyle.InputText)
                    .Set(row, 2, isQuiz ? "Answer all the questions." : "Read the lesson, then answer the questions.", XlsxStyle.Wrap)
                    .Set(row, 3, isQuiz ? 30 : kind == ItemKinds.TaskPerformance ? 100 : 50, XlsxStyle.Input)
                    .Set(row, 4, "", XlsxStyle.InputText).Set(row, 5, "2026-11-20 17:00", XlsxStyle.InputText)
                    .Set(row, 6, isQuiz ? "" : "worksheet.pdf", XlsxStyle.InputText);
                row++;
            }

            for (; row <= Math.Max(TemplateRows + 1, items.Count + 2); row++)
            {
                for (var column = 1; column <= WorkHeaders.Length; column++)
                {
                    sheet.Set(row, column, null, column is 1 or 4 or 5 or 6 ? XlsxStyle.InputText : column == 2 ? XlsxStyle.Wrap : XlsxStyle.Input);
                }
            }

            sheet.FrozenRows = 1;
            var last = Math.Max(TemplateRows + 1, items.Count + 2);
            sheet.Tip($"A2:A{last}", "Type the title (needed).").Tip($"B2:B{last}", "Type what the students must do or read.");
            if (isQuiz)
            {
                sheet.Number($"C2:C{last}", 1, 600, "Type the minutes a student has to answer, from 1 to 600. Leave it empty for no limit.");
            }
            else
            {
                sheet.Number($"C2:C{last}", 1, 1000, "Type the highest score, a number from 1 to 1000. Leave it empty for the usual one (50 or 100).");
            }

            sheet.Tip($"D2:E{last}", "Type the day and the hour (Philippine time) like 2026-11-20 17:00. Leave it empty if it has none.");
            sheet.Tip($"F2:F{last}", "Type the names of the files with a semicolon between them, like worksheet.pdf; rubric.docx. Choose the same files in the import box.");
        }

        WorkSheet(AssignmentsSheet, ItemKinds.Activity);
        WorkSheet(QuizzesSheet, ItemKinds.Quiz);

        var questions = book.AddSheet(QuestionsSheet);
        questions.Row(1, QuestionHeaders, XlsxStyle.Header);
        questions.Width(1, 32).Width(2, 50).Width(3, 16).Width(4, 40).Width(5, 24).Width(6, 8);
        var qRow = 2;
        foreach (var quiz in items.Where(i => i.Kind == ItemKinds.Quiz))
        {
            foreach (var q in quiz.Questions)
            {
                questions.Set(qRow, 1, quiz.Row.Title, XlsxStyle.InputText).Set(qRow, 2, q.Text, XlsxStyle.Wrap).Set(qRow, 3, TypeText(q.Kind), XlsxStyle.InputText)
                    .Set(qRow, 4, q.Kind == QuestionKinds.MultipleChoice ? string.Join("; ", ModuleService.SplitOptions(q.OptionsText)) : "", XlsxStyle.Wrap)
                    .Set(qRow, 5, q.CorrectAnswer, XlsxStyle.InputText).Set(qRow, 6, q.Points, XlsxStyle.Input);
                qRow++;
            }
        }

        if (example)
        {
            questions.Set(qRow, 1, "Quiz 1: Debits and credits", XlsxStyle.InputText).Set(qRow, 2, "Which account is increased by a debit?", XlsxStyle.Wrap).Set(qRow, 3, "Multiple choice", XlsxStyle.InputText)
                .Set(qRow, 4, "Asset; Liability; Income", XlsxStyle.Wrap).Set(qRow, 5, "Asset", XlsxStyle.InputText).Set(qRow, 6, 2, XlsxStyle.Input);
            qRow++;
            questions.Set(qRow, 1, "Quiz 1: Debits and credits", XlsxStyle.InputText).Set(qRow, 2, "Every debit has an equal credit.", XlsxStyle.Wrap).Set(qRow, 3, "True or false", XlsxStyle.InputText)
                .Set(qRow, 4, "", XlsxStyle.Wrap).Set(qRow, 5, "True", XlsxStyle.InputText).Set(qRow, 6, 1, XlsxStyle.Input);
            qRow++;
            questions.Set(qRow, 1, "Quiz 1: Debits and credits", XlsxStyle.InputText).Set(qRow, 2, "What is the left side of the accounting equation?", XlsxStyle.Wrap).Set(qRow, 3, "Short answer", XlsxStyle.InputText)
                .Set(qRow, 4, "", XlsxStyle.Wrap).Set(qRow, 5, "Assets", XlsxStyle.InputText).Set(qRow, 6, 1, XlsxStyle.Input);
            qRow++;
        }

        var lastQuestionRow = Math.Max(TemplateRows + 1, qRow + 20);
        for (; qRow <= lastQuestionRow; qRow++)
        {
            for (var column = 1; column <= QuestionHeaders.Length; column++)
            {
                questions.Set(qRow, column, null, column is 2 or 4 ? XlsxStyle.Wrap : column is 1 or 3 or 5 ? XlsxStyle.InputText : XlsxStyle.Input);
            }
        }

        questions.FrozenRows = 1;
        questions.ListChoice($"C2:C{lastQuestionRow}", ["Multiple choice", "True or false", "Short answer"], "Choose the type from the list.");
        questions.ListFrom($"A2:A{lastQuestionRow}", $"{QuizzesSheet}!$A$2:$A${MaxRows + 1}", "Choose the quiz from the list: it shows the titles you wrote in the sheet " + QuizzesSheet + ".");
        questions.Tip($"B2:B{lastQuestionRow}", "Type the question.");
        questions.Tip($"D2:D{lastQuestionRow}", "Multiple choice only: type the choices with a semicolon between them, like Asset; Liability; Income.");
        questions.Tip($"E2:E{lastQuestionRow}", "Type the right answer: the right choice (or its letter), True or False, or the answer. Other accepted answers go after a | sign.");
        questions.Number($"F2:F{lastQuestionRow}", 0.01m, 100, "Type the points, from 0.01 to 100. Leave it empty for 1.");

        WorkSheet(TasksSheet, ItemKinds.TaskPerformance);

        const string Needed = "Needed", Optional = "Optional";
        TemplateGuide.Add(book, "How to fill the workbook",
        [
            new(AssignmentsSheet + ", " + TasksSheet, "Title", Needed, "The name of the work, like Assignment 1: Journal entries.", TemplateGuide.TypeIt),
            new(AssignmentsSheet + ", " + TasksSheet, "Instructions", Optional, "What the students must do.", TemplateGuide.TypeIt),
            new(AssignmentsSheet + ", " + TasksSheet, "Highest score", Optional, "A number from 1 to 1000. Empty: 50 for an assignment, 100 for a task performance.", TemplateGuide.TypeNumber),
            new(AssignmentsSheet + ", " + TasksSheet, "Appears on", Optional, "The day and hour the students can see it (Philippine time). Empty: it appears with its module.", TemplateGuide.DateHow),
            new(AssignmentsSheet + ", " + TasksSheet, "Due date", Optional, "The last day and hour to hand it in (Philippine time). Empty: no due date.", TemplateGuide.DateHow),
            new(AssignmentsSheet + ", " + TasksSheet, "Files", Optional, "The names of the files that go with it, like worksheet.pdf; rubric.docx.", "Type the names with a semicolon (;) between them. In the import box, choose the same files."),
            new(QuizzesSheet, "Title", Needed, "The name of the quiz, like Quiz 1: Debits and credits.", TemplateGuide.TypeIt),
            new(QuizzesSheet, "Instructions", Optional, "What the students read before they start.", TemplateGuide.TypeIt),
            new(QuizzesSheet, "Time limit (minutes)", Optional, "The minutes a student has once they start. Empty: no limit.", TemplateGuide.TypeNumber),
            new(QuizzesSheet, "Appears on, Due date, Files", Optional, "The same as the other sheets.", "The same as above."),
            new(QuestionsSheet, "Quiz", Needed, "The quiz the question belongs to. This is what connects a question to its quiz.", "Choose the title from the list (it lists the titles of the sheet " + QuizzesSheet + ")."),
            new(QuestionsSheet, "Question", Needed, "The question the students read.", TemplateGuide.TypeIt),
            new(QuestionsSheet, "Type", Needed, "Multiple choice, True or false, or Short answer.", "Choose it from the list."),
            new(QuestionsSheet, "Choices", "Multiple choice only", "The choices, at least two, like Asset; Liability; Income.", TemplateGuide.Semicolons),
            new(QuestionsSheet, "Right answer", Needed, "Multiple choice: the right choice (or its letter, A for the first). True or false: True or False. Short answer: the answer; other accepted answers go after a | sign.", TemplateGuide.TypeIt),
            new(QuestionsSheet, "Points", Optional, "From 0.01 to 100. Empty: 1.", TemplateGuide.TypeNumber),
        ],
            "Leave a sheet empty to add nothing of that kind.",
            $"At most {MaxRows} rows in a sheet.");
        return book.ToBytes();
    }

    // ======================================================================= reading

    /// <summary>
    /// Reads the workbook: each row of the sheets Assignments, Quizzes and Task Performances becomes one thing to add (checked like the box of the page checks it), and the rows of Quiz Questions
    /// are given to the quiz whose title is in their Quiz column. A row with a problem is not ready and says why.
    /// </summary>
    public static ActivityImportParse Parse(byte[] data)
    {
        IReadOnlyList<XlsxSheetData> sheets;
        try
        {
            sheets = XlsxReader.Read(data);
        }
        catch (InvalidDataException problem)
        {
            return new ActivityImportParse([], problem.Message);
        }

        var assignments = Find(sheets, AssignmentsSheet, "Assignment", "Activities", "Activity");
        var quizzes = Find(sheets, QuizzesSheet, "Quiz");
        var questions = Find(sheets, QuestionsSheet, "Questions", "Quiz Question");
        var tasks = Find(sheets, TasksSheet, "Task Performance", "Tasks", "TP");
        if (assignments is null && quizzes is null && tasks is null)
        {
            return new ActivityImportParse([], $"This is not the template: it has no sheet called {AssignmentsSheet}, {QuizzesSheet} or {TasksSheet}. Download the template again.");
        }

        var rows = new List<ActivityImportRow>();
        var quizRows = new List<(ActivityImportRow Row, List<QuestionInput> Questions)>();

        if (assignments is not null)
        {
            var error = ReadWork(assignments, ItemKinds.Activity, rows, null);
            if (error is not null)
            {
                return new ActivityImportParse(rows, error);
            }
        }

        if (quizzes is not null)
        {
            var error = ReadWork(quizzes, ItemKinds.Quiz, rows, quizRows);
            if (error is not null)
            {
                return new ActivityImportParse(rows, error);
            }
        }

        if (tasks is not null)
        {
            var error = ReadWork(tasks, ItemKinds.TaskPerformance, rows, null);
            if (error is not null)
            {
                return new ActivityImportParse(rows, error);
            }
        }

        // the questions: each one goes to the quiz of its title
        if (questions is not null)
        {
            var problems = ReadQuestions(questions, quizRows, rows);
            if (problems is not null)
            {
                return new ActivityImportParse(rows, problems);
            }
        }

        // the quizzes now have their questions
        for (var i = 0; i < quizRows.Count; i++)
        {
            var (row, list) = quizRows[i];
            var index = rows.IndexOf(row);
            if (row.Work is null)
            {
                continue;
            }

            var errors = row.Errors.ToList();
            if (list.Count > 0 && row.Work.FileNames.Count > 0)
            {
                errors.Add("A quiz that has a file has no questions: the students answer with the file and you give the score. Take the file or the questions out.");
            }

            var problem = list.Count == 0 ? [] : ModuleService.ValidateQuestions(list);
            errors.AddRange(problem);
            rows[index] = errors.Count == 0 ? row with { Work = row.Work with { Questions = list } } : row with { Work = null, Errors = errors };
        }

        return rows.Count == 0
            ? new ActivityImportParse([], "The file has nothing to add. Fill in the rows under the column titles.")
            : new ActivityImportParse(rows.OrderBy(r => SheetOrder(r.Sheet)).ThenBy(r => r.Row).ToList(), null);
    }

    private static int SheetOrder(string sheet) => sheet switch { AssignmentsSheet => 0, QuizzesSheet => 1, _ => 2 };

    private static XlsxSheetData? Find(IReadOnlyList<XlsxSheetData> sheets, params string[] names) =>
        sheets.FirstOrDefault(s => names.Any(n => string.Equals(s.Name.Trim(), n, StringComparison.OrdinalIgnoreCase)));

    private static List<string> SplitFiles(string text) =>
        text.Split([';', '\n', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(n => n.Length > 0).ToList();

    private static string? ReadWork(XlsxSheetData sheet, string kind, List<ActivityImportRow> rows, List<(ActivityImportRow, List<QuestionInput>)>? quizRows)
    {
        var isQuiz = kind == ItemKinds.Quiz;
        var headers = Enumerable.Range(1, 10).Select(c => sheet.Cell(1, c).Trim().ToLowerInvariant()).ToList();
        int Column(string name, int fallback) => headers.IndexOf(name) is >= 0 and var i ? i + 1 : fallback;
        if (!headers.Contains("title"))
        {
            return $"The sheet {sheet.Name} has no column Title in its first row. Download the template again.";
        }

        var title = Column("title", 1);
        var instructions = Column("instructions", 2);
        var third = isQuiz ? Column("time limit (minutes)", 3) : Column("highest score", 3);
        var appears = Column("appears on", 4);
        var due = Column("due date", 5);
        var files = Column("files", 6);
        var sheetName = isQuiz ? QuizzesSheet : kind == ItemKinds.TaskPerformance ? TasksSheet : AssignmentsSheet;
        var count = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var r = 2; r <= sheet.Rows.Count; r++)
        {
            var cells = new[] { title, instructions, third, appears, due, files }.Select(c => sheet.Cell(r, c).Trim()).ToList();
            if (cells.All(c => c.Length == 0))
            {
                continue;
            }

            if (++count > MaxRows)
            {
                return $"The sheet {sheet.Name} has more than {MaxRows} rows. Import it in parts.";
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

            input.VisibleFromUtc = Moment(cells[3], "Appears on", errors);
            input.DueAtUtc = Moment(cells[4], "Due date", errors);
            if (isQuiz && cells[0].Length > 0 && !seen.Add(cells[0]))
            {
                errors.Add("Another quiz has the same title, so the questions can't tell which one they belong to. Give each quiz its own title.");
            }

            if (errors.Count == 0)
            {
                errors.AddRange(ModuleService.ValidateItem(input));
            }

            var work = errors.Count == 0 ? new ActivityWork(kind, input, [], SplitFiles(cells[5])) : null;
            var row = new ActivityImportRow(sheetName, r, cells[0], work, errors);
            rows.Add(row);
            quizRows?.Add((row, []));
        }

        return null;
    }

    private static string? ReadQuestions(XlsxSheetData sheet, List<(ActivityImportRow Row, List<QuestionInput> Questions)> quizzes, List<ActivityImportRow> rows)
    {
        var headers = Enumerable.Range(1, 8).Select(c => sheet.Cell(1, c).Trim().ToLowerInvariant()).ToList();
        if (!headers.Contains("quiz") || !headers.Contains("question"))
        {
            return $"The sheet {sheet.Name} needs the columns Quiz and Question in its first row. Download the template again.";
        }

        int Column(string name, int fallback) => headers.IndexOf(name) is >= 0 and var i ? i + 1 : fallback;
        var quizColumn = Column("quiz", 1);
        var textColumn = Column("question", 2);
        var typeColumn = Column("type", 3);
        var choicesColumn = Column("choices", 4);
        var answerColumn = Column("right answer", 5);
        var pointsColumn = Column("points", 6);
        var count = 0;
        for (var r = 2; r <= sheet.Rows.Count; r++)
        {
            var quizTitle = sheet.Cell(r, quizColumn).Trim();
            var text = sheet.Cell(r, textColumn).Trim();
            var type = sheet.Cell(r, typeColumn).Trim();
            var choices = sheet.Cell(r, choicesColumn).Trim();
            var answer = sheet.Cell(r, answerColumn).Trim();
            var points = sheet.Cell(r, pointsColumn).Trim();
            if (quizTitle.Length == 0 && text.Length == 0 && type.Length == 0 && choices.Length == 0 && answer.Length == 0 && points.Length == 0)
            {
                continue;
            }

            if (++count > ItemSheets.MaxQuestions)
            {
                return $"The sheet {sheet.Name} has more than {ItemSheets.MaxQuestions} questions. Import it in parts.";
            }

            var label = $"{sheet.Name}, row {r}";
            var target = quizzes.FirstOrDefault(q => string.Equals(q.Row.Title, quizTitle, StringComparison.OrdinalIgnoreCase));
            if (quizTitle.Length == 0 || target.Row is null)
            {
                // A question that names no quiz of the workbook is a row of its own with the problem, so nothing is lost without a word.
                rows.Add(new ActivityImportRow(QuestionsSheet, r, text, null, [quizTitle.Length == 0
                    ? "Write the title of the quiz this question belongs to in the column Quiz."
                    : $"There is no quiz called \"{quizTitle}\" in the sheet {QuizzesSheet}. Write its title exactly as it is there."]));
                continue;
            }

            var (question, found) = ItemSheets.ParseQuestionRow(text, type, choices, answer, points);
            if (question is null)
            {
                // the quiz cannot be added half-done: it gets the problem
                var index = rows.IndexOf(target.Row);
                var current = rows[index];
                var updated = current with { Work = null, Errors = [.. current.Errors, .. found.Select(f => $"{label}: {f}")] };
                rows[index] = updated;
                var at = quizzes.FindIndex(q => ReferenceEquals(q.Row, target.Row));
                quizzes[at] = (updated, target.Questions);
                continue;
            }

            if (target.Row.Work is not null)
            {
                target.Questions.Add(question);
            }
        }

        return null;
    }

    private static DateTime? Moment(string text, string label, List<string> errors)
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
}
