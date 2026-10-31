using System.Globalization;
using SEAITT_LMS.Components.Shared;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>One thing to add to a module that the workbook describes: a seatwork, an assignment, a quiz (with its questions) or a task performance, the module it belongs to and the files it names.</summary>
public sealed record ActivityWork(string Kind, ItemInput Input, IReadOnlyList<QuestionInput> Questions, IReadOnlyList<string> FileNames, string Module = "");

/// <summary>A row of the workbook: where it is (sheet and row), its title, and either the work that is ready or what is wrong with it.</summary>
public sealed record ActivityImportRow(string Sheet, int Row, string Title, ActivityWork? Work, IReadOnlyList<string> Errors);

public sealed record ActivityImportParse(IReadOnlyList<ActivityImportRow> Rows, string? FileError)
{
    /// <summary>Every file name the rows that are ready ask for (the Files column), without repeats.</summary>
    public IReadOnlyList<string> FileNamesWanted => Rows.Where(r => r.Work is not null).SelectMany(r => r.Work!.FileNames).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}

/// <summary>What a module has now, for the export: the work with its questions and the names of its files.</summary>
public sealed record ActivityExportItem(string Kind, ItemExportRow Row, IReadOnlyList<QuestionInput> Questions, IReadOnlyList<string> FileNames);

/// <summary>A module of the course with the work it has now (the export).</summary>
public sealed record ActivityModuleExport(string Title, IReadOnlyList<ActivityExportItem> Items);

/// <summary>
/// The one Excel workbook of a course for the seatwork, assignments, quizzes and task performances of all its modules (the teacher's "Add activities"). The sheet <b>Activities</b> goes module
/// by module: the name of the module, then one row for each kind in this order: Seatwork, Assignment, Quiz, Task performance. The teacher types the title and the rest of the cells of
/// the rows they want and leaves the others empty. The column Files names the files that go with each one (they are chosen when the workbook is imported, and matched by name). The
/// questions of a quiz are in the sheet <b>Quiz Questions</b>, connected to the quiz by its title. The export of a course is the same workbook. (The recitation is only a column of
/// the gradebook, so it is not here.)
/// </summary>
public static class ActivitySheets
{
    public const int MaxRows = 300;

    public const string ActivitiesSheet = "Activities";
    public const string QuestionsSheet = "Quiz Questions";

    private const int ExtraRows = 6;

    /// <summary>The kinds of the sheet, in order, with the word the Type column uses.</summary>
    private static readonly (string Kind, string Word)[] Kinds =
    [
        (ItemKinds.Seatwork, "Seatwork"),
        (ItemKinds.Activity, "Assignment"),
        (ItemKinds.Quiz, "Quiz"),
        (ItemKinds.TaskPerformance, "Task performance"),
    ];

    private static readonly string[] Headers = ["Module", "Type", "Title", "Instructions", "Highest score", "Time limit (minutes)", "Appears on", "Due date", "Files"];
    private static readonly string[] QuestionHeaders = ["Quiz", "Question", "Type", "Choices", "Right answer", "Points"];

    public static string FileName(string what) => FileDownloads.CleanName($"{what} - Activities.xlsx");

    /// <summary>The template of a course: a block for each module it has (or Module 1 to 3 when it has none yet), with a row for each kind.</summary>
    public static byte[] Template(IReadOnlyList<string>? moduleTitles = null) =>
        Build([.. (moduleTitles is { Count: > 0 } ? moduleTitles : ["Module 1", "Module 2", "Module 3"]).Select(t => new ActivityModuleExport(t, []))], example: true);

    public static byte[] Export(IReadOnlyList<ActivityModuleExport> modules) => Build(modules, example: false);

    private static string DateText(DateTime? utc) => utc is { } at ? PhilippineTime.ToLocal(at).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : "";

    private static string TypeText(string kind) => kind switch
    {
        QuestionKinds.TrueFalse => "True or false",
        QuestionKinds.ShortAnswer => "Short answer",
        _ => "Multiple choice",
    };

    private static byte[] Build(IReadOnlyList<ActivityModuleExport> modules, bool example)
    {
        var book = new XlsxBook();
        var sheet = book.AddSheet(ActivitiesSheet);
        sheet.Row(1, Headers, XlsxStyle.Header);
        sheet.Width(1, 30).Width(2, 18).Width(3, 32).Width(4, 44).Width(5, 14).Width(6, 20).Width(7, 18).Width(8, 18).Width(9, 32);

        void Fill(int row, string module, string word, ItemExportRow? item, IReadOnlyList<string>? files)
        {
            sheet.Set(row, 1, module, XlsxStyle.InputText).Set(row, 2, word, XlsxStyle.InputText);
            if (item is null)
            {
                for (var column = 3; column <= Headers.Length; column++)
                {
                    sheet.Set(row, column, null, column == 4 ? XlsxStyle.Wrap : column is 5 or 6 ? XlsxStyle.Input : XlsxStyle.InputText);
                }

                return;
            }

            var isQuiz = word == "Quiz";
            sheet.Set(row, 3, item.Title, XlsxStyle.InputText).Set(row, 4, item.Instructions ?? "", XlsxStyle.Wrap)
                .Set(row, 5, isQuiz ? null : item.MaxScore, XlsxStyle.Input).Set(row, 6, isQuiz ? item.TimeLimitMinutes : null, XlsxStyle.Input)
                .Set(row, 7, DateText(item.VisibleFromUtc), XlsxStyle.InputText).Set(row, 8, DateText(item.DueAtUtc), XlsxStyle.InputText)
                .Set(row, 9, string.Join("; ", files ?? []), XlsxStyle.InputText);
        }

        var rowNumber = 2;
        var first = true;
        var quizzes = new List<ActivityExportItem>();
        foreach (var module in modules)
        {
            var firstOfBlock = true;
            foreach (var (kind, word) in Kinds)
            {
                var items = module.Items.Where(i => i.Kind == kind).ToList();
                var rows = Math.Max(1, items.Count) + (example || items.Count == 0 ? 0 : 0);
                for (var n = 0; n < rows; n++)
                {
                    var name = firstOfBlock ? module.Title : "";
                    firstOfBlock = false;
                    if (n < items.Count)
                    {
                        Fill(rowNumber, name, word, items[n].Row, items[n].FileNames);
                        if (kind == ItemKinds.Quiz)
                        {
                            quizzes.Add(items[n]);
                        }
                    }
                    else if (example && first && kind == ItemKinds.Seatwork)
                    {
                        Fill(rowNumber, name, word, new ItemExportRow("Seatwork 1: Journal practice", "Answer in your notebook.", 20, null, null, null), []);
                    }
                    else if (example && first && kind == ItemKinds.Quiz)
                    {
                        Fill(rowNumber, name, word, new ItemExportRow("Quiz 1: Debits and credits", "Answer all the questions.", 0, null, null, 30), []);
                    }
                    else
                    {
                        Fill(rowNumber, name, word, null, null);
                    }

                    rowNumber++;
                }
            }

            first = false;
        }

        for (var extra = 0; extra < ExtraRows; extra++, rowNumber++)
        {
            for (var column = 1; column <= Headers.Length; column++)
            {
                sheet.Set(rowNumber, column, null, column == 4 ? XlsxStyle.Wrap : column is 5 or 6 ? XlsxStyle.Input : XlsxStyle.InputText);
            }
        }

        sheet.FrozenRows = 1;
        var last = rowNumber - 1;
        var titles = modules.Select(m => m.Title).ToList();
        if (titles.Count > 0)
        {
            sheet.ListChoice($"A2:A{last}", [.. titles], "Choose the module from the list. Write it only on the first row of its block: the rows under it with no module belong to the same module.");
        }

        sheet.ListChoice($"B2:B{last}", [.. Kinds.Select(k => k.Word)], "Choose the type from the list.");
        sheet.Tip($"C2:C{last}", "Type the title of what you want to add. Leave the row empty to add nothing.").Tip($"D2:D{last}", "Type what the students must do or read.");
        sheet.Number($"E2:E{last}", 1, 1000, "Type the highest score, a number from 1 to 1000. Leave it empty for the usual one (50, or 100 for a task performance). A quiz is worth the points of its questions.");
        sheet.Number($"F2:F{last}", 1, 600, "Quiz only: type the minutes a student has to answer, from 1 to 600. Leave it empty for no limit.");
        sheet.Tip($"G2:H{last}", "Type the day and the hour (Philippine time) like 2026-11-20 17:00. Leave it empty if it has none.");
        sheet.Tip($"I2:I{last}", "Type the names of the files with a semicolon between them, like worksheet.pdf; rubric.docx. Choose the same files in the import box.");

        // ---- the questions of the quizzes
        var questions = book.AddSheet(QuestionsSheet);
        questions.Row(1, QuestionHeaders, XlsxStyle.Header);
        questions.Width(1, 32).Width(2, 50).Width(3, 16).Width(4, 40).Width(5, 24).Width(6, 8);
        var qRow = 2;
        foreach (var quiz in quizzes)
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
            const string title = "Quiz 1: Debits and credits";
            questions.Set(qRow, 1, title, XlsxStyle.InputText).Set(qRow, 2, "Which account is increased by a debit?", XlsxStyle.Wrap).Set(qRow, 3, "Multiple choice", XlsxStyle.InputText)
                .Set(qRow, 4, "Asset; Liability; Income", XlsxStyle.Wrap).Set(qRow, 5, "Asset", XlsxStyle.InputText).Set(qRow, 6, 2, XlsxStyle.Input);
            qRow++;
            questions.Set(qRow, 1, title, XlsxStyle.InputText).Set(qRow, 2, "Every debit has an equal credit.", XlsxStyle.Wrap).Set(qRow, 3, "True or false", XlsxStyle.InputText)
                .Set(qRow, 4, "", XlsxStyle.Wrap).Set(qRow, 5, "True", XlsxStyle.InputText).Set(qRow, 6, 1, XlsxStyle.Input);
            qRow++;
        }

        var lastQuestionRow = Math.Max(31, qRow + 20);
        for (; qRow <= lastQuestionRow; qRow++)
        {
            for (var column = 1; column <= QuestionHeaders.Length; column++)
            {
                questions.Set(qRow, column, null, column is 2 or 4 ? XlsxStyle.Wrap : column is 1 or 3 or 5 ? XlsxStyle.InputText : XlsxStyle.Input);
            }
        }

        questions.FrozenRows = 1;
        questions.ListChoice($"C2:C{lastQuestionRow}", ["Multiple choice", "True or false", "Short answer"], "Choose the type from the list.");
        questions.Tip($"A2:A{lastQuestionRow}", "Type the title of the quiz this question belongs to, exactly as in the sheet " + ActivitiesSheet + ".");
        questions.Tip($"B2:B{lastQuestionRow}", "Type the question.");
        questions.Tip($"D2:D{lastQuestionRow}", "Multiple choice only: type the choices with a semicolon between them, like Asset; Liability; Income.");
        questions.Tip($"E2:E{lastQuestionRow}", "Type the right answer: the right choice (or its letter), True or False, or the answer. Other accepted answers go after a | sign.");
        questions.Number($"F2:F{lastQuestionRow}", 0.01m, 100, "Type the points, from 0.01 to 100. Leave it empty for 1.");

        const string Needed = "Needed", Optional = "Optional";
        TemplateGuide.Add(book, "How to fill the workbook",
        [
            new(ActivitiesSheet, "Module", Needed, "The module the rows belong to. Write it once, on the first row of its block; the rows under it with no module belong to the same one.", "Choose it from the list."),
            new(ActivitiesSheet, "Type", Needed, "Seatwork, Assignment, Quiz or Task performance.", "Choose it from the list."),
            new(ActivitiesSheet, "Title", Needed, "The name of the work, like Assignment 1: Journal entries. A row with no title adds nothing.", TemplateGuide.TypeIt),
            new(ActivitiesSheet, "Instructions", Optional, "What the students must do.", TemplateGuide.TypeIt),
            new(ActivitiesSheet, "Highest score", Optional, "A number from 1 to 1000. Empty: 50 (100 for a task performance). A quiz is worth the points of its questions.", TemplateGuide.TypeNumber),
            new(ActivitiesSheet, "Time limit (minutes)", Optional, "Quiz only: the minutes a student has once they start. Empty: no limit.", TemplateGuide.TypeNumber),
            new(ActivitiesSheet, "Appears on", Optional, "The day and hour the students can see it (Philippine time). Empty: it appears with its module.", TemplateGuide.DateHow),
            new(ActivitiesSheet, "Due date", Optional, "The last day and hour to hand it in (Philippine time). Empty: no due date.", TemplateGuide.DateHow),
            new(ActivitiesSheet, "Files", Optional, "The names of the files that go with it, like worksheet.pdf; rubric.docx.", "Type the names with a semicolon (;) between them. In the import box, choose the same files."),
            new(QuestionsSheet, "Quiz", Needed, "The quiz the question belongs to: its title, written like in the sheet " + ActivitiesSheet + ". This is what connects a question to its quiz.", TemplateGuide.TypeIt),
            new(QuestionsSheet, "Question", Needed, "The question the students read.", TemplateGuide.TypeIt),
            new(QuestionsSheet, "Type", Needed, "Multiple choice, True or false, or Short answer.", "Choose it from the list."),
            new(QuestionsSheet, "Choices", "Multiple choice only", "The choices, at least two, like Asset; Liability; Income.", TemplateGuide.Semicolons),
            new(QuestionsSheet, "Right answer", Needed, "Multiple choice: the right choice (or its letter, A for the first). True or false: True or False. Short answer: the answer; other accepted answers go after a | sign.", TemplateGuide.TypeIt),
            new(QuestionsSheet, "Points", Optional, "From 0.01 to 100. Empty: 1.", TemplateGuide.TypeNumber),
        ],
            "The first rows of the template are examples: change them or clear them.",
            "The recitation is not here: it is only a column of the gradebook.",
            $"At most {MaxRows} rows in the sheet {ActivitiesSheet}.");
        return book.ToBytes();
    }

    // ======================================================================= reading

    /// <summary>
    /// Reads the workbook: each row with a title in the sheet Activities becomes one thing to add to its module (checked like the box of the page checks it), and the rows of Quiz Questions
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

        var activities = Find(sheets, ActivitiesSheet, "Activity", "Work");
        var questions = Find(sheets, QuestionsSheet, "Questions", "Quiz Question");
        if (activities is null)
        {
            return new ActivityImportParse([], $"This is not the template: it has no sheet called {ActivitiesSheet}. Download the template again.");
        }

        var headers = Enumerable.Range(1, 12).Select(c => activities.Cell(1, c).Trim().ToLowerInvariant()).ToList();
        int Column(string name, int fallback) => headers.IndexOf(name) is >= 0 and var i ? i + 1 : fallback;
        if (!headers.Contains("title") || !headers.Contains("type"))
        {
            return new ActivityImportParse([], $"The sheet {ActivitiesSheet} needs the columns Module, Type and Title in its first row. Download the template again.");
        }

        var moduleColumn = Column("module", 1);
        var typeColumn = Column("type", 2);
        var titleColumn = Column("title", 3);
        var instructionsColumn = Column("instructions", 4);
        var scoreColumn = Column("highest score", 5);
        var limitColumn = Column("time limit (minutes)", 6);
        var appearsColumn = Column("appears on", 7);
        var dueColumn = Column("due date", 8);
        var filesColumn = Column("files", 9);

        var rows = new List<ActivityImportRow>();
        var quizRows = new List<(ActivityImportRow Row, List<QuestionInput> Questions)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var currentModule = "";
        var count = 0;
        for (var r = 2; r <= activities.Rows.Count; r++)
        {
            var module = activities.Cell(r, moduleColumn).Trim();
            if (module.Length > 0)
            {
                currentModule = module;
            }

            var type = activities.Cell(r, typeColumn).Trim();
            var title = activities.Cell(r, titleColumn).Trim();
            var instructions = activities.Cell(r, instructionsColumn).Trim();
            var score = activities.Cell(r, scoreColumn).Trim();
            var limit = activities.Cell(r, limitColumn).Trim();
            var appears = activities.Cell(r, appearsColumn).Trim();
            var due = activities.Cell(r, dueColumn).Trim();
            var files = activities.Cell(r, filesColumn).Trim();

            // A row with nothing typed in it (the module and the type are already written in the template) adds nothing.
            if (title.Length == 0 && instructions.Length == 0 && score.Length == 0 && limit.Length == 0 && appears.Length == 0 && due.Length == 0 && files.Length == 0)
            {
                continue;
            }

            if (++count > MaxRows)
            {
                return new ActivityImportParse(rows, $"The sheet {ActivitiesSheet} has more than {MaxRows} rows. Import it in parts.");
            }

            var errors = new List<string>();
            var kind = KindOf(type);
            if (kind is null)
            {
                errors.Add("The type must be Seatwork, Assignment, Quiz or Task performance.");
            }

            if (currentModule.Length == 0)
            {
                errors.Add("Write the module this row belongs to.");
            }

            if (title.Length == 0)
            {
                errors.Add("Write the title.");
            }

            var isQuiz = kind == ItemKinds.Quiz;
            var input = new ItemInput { Kind = kind ?? ItemKinds.Activity, Title = title, Instructions = instructions.Length == 0 ? null : instructions, MaxScore = kind == ItemKinds.TaskPerformance ? 100 : 50 };
            if (score.Length > 0 && !isQuiz)
            {
                if (decimal.TryParse(score, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                {
                    input.MaxScore = number;
                }
                else
                {
                    errors.Add("Highest score must be a number.");
                }
            }

            if (limit.Length > 0 && isQuiz)
            {
                if (decimal.TryParse(limit, NumberStyles.Number, CultureInfo.InvariantCulture, out var minutes))
                {
                    input.TimeLimitMinutes = (int)Math.Round(minutes);
                }
                else
                {
                    errors.Add("Time limit must be a number of minutes.");
                }
            }

            input.VisibleFromUtc = Moment(appears, "Appears on", errors);
            input.DueAtUtc = Moment(due, "Due date", errors);
            if (isQuiz && title.Length > 0 && !seen.Add(title))
            {
                errors.Add("Another quiz has the same title, so the questions can't tell which one they belong to. Give each quiz its own title.");
            }

            if (errors.Count == 0)
            {
                errors.AddRange(ModuleService.ValidateItem(input));
            }

            var work = errors.Count == 0 ? new ActivityWork(kind!, input, [], SplitFiles(files), currentModule) : null;
            var row = new ActivityImportRow(ActivitiesSheet, r, title, work, errors);
            rows.Add(row);
            if (isQuiz)
            {
                quizRows.Add((row, []));
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
        foreach (var (row, list) in quizRows)
        {
            var index = rows.FindIndex(x => x.Sheet == row.Sheet && x.Row == row.Row);
            var current = rows[index];
            if (current.Work is null)
            {
                continue;
            }

            var errors = current.Errors.ToList();
            if (list.Count > 0 && current.Work.FileNames.Count > 0)
            {
                errors.Add("A quiz that has a file has no questions: the students answer with the file and you give the score. Take the file or the questions out.");
            }

            errors.AddRange(list.Count == 0 ? [] : ModuleService.ValidateQuestions(list));
            rows[index] = errors.Count == 0 ? current with { Work = current.Work with { Questions = list } } : current with { Work = null, Errors = errors };
        }

        return rows.Count == 0
            ? new ActivityImportParse([], "The file has nothing to add. Type the title of what you want to add in the rows of the sheet.")
            : new ActivityImportParse(rows.OrderBy(r => r.Sheet == ActivitiesSheet ? 0 : 1).ThenBy(r => r.Row).ToList(), null);
    }

    private static string? KindOf(string type) => type.ToLowerInvariant().Replace(" ", "").Replace("-", "") switch
    {
        "seatwork" or "seatworks" => ItemKinds.Seatwork,
        "assignment" or "assignments" or "activity" or "activities" => ItemKinds.Activity,
        "quiz" or "quizzes" => ItemKinds.Quiz,
        "taskperformance" or "taskperformances" or "tp" or "task" => ItemKinds.TaskPerformance,
        _ => null,
    };

    private static XlsxSheetData? Find(IReadOnlyList<XlsxSheetData> sheets, params string[] names) =>
        sheets.FirstOrDefault(s => names.Any(n => string.Equals(s.Name.Trim(), n, StringComparison.OrdinalIgnoreCase)));

    private static List<string> SplitFiles(string text) =>
        text.Split([';', '\n', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(n => n.Length > 0).ToList();

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
                    : $"There is no quiz called \"{quizTitle}\" in the sheet {ActivitiesSheet}. Write its title exactly as it is there."]));
                continue;
            }

            var (question, found) = ItemSheets.ParseQuestionRow(text, type, choices, answer, points);
            if (question is null)
            {
                // the quiz cannot be added half-done: it gets the problem
                var index = rows.FindIndex(x => x.Sheet == target.Row.Sheet && x.Row == target.Row.Row);
                var current = rows[index];
                var updated = current with { Work = null, Errors = [.. current.Errors, .. found.Select(f => $"{label}: {f}")] };
                rows[index] = updated;
                var at = quizzes.FindIndex(q => q.Row.Sheet == target.Row.Sheet && q.Row.Row == target.Row.Row);
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
