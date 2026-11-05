using System.Text.RegularExpressions;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>A picture that goes with a question (or with one of its choices): the file name, what it is and its bytes.</summary>
public sealed class QuestionPicture
{
    public string Name { get; set; } = "";
    public string ContentType { get; set; } = "image/png";
    public byte[] Data { get; set; } = [];

    /// <summary>The biggest picture a question can have.</summary>
    public const int MaxBytes = 2 * 1024 * 1024;

    /// <summary>The kinds of picture that are accepted (what a browser shows in an img tag).</summary>
    public static readonly string[] Types = ["image/png", "image/jpeg", "image/gif", "image/webp"];

    public string DataUri => $"data:{ContentType};base64,{Convert.ToBase64String(Data)}";
}

/// <summary>
/// What each kind of question means: how its right answer is written, what is wrong with it and how an answer is scored. A summative exam of the library, a quiz of a teacher and the
/// copy a student takes all use these rules, so a question means the same everywhere.
/// <list type="bullet">
/// <item>Multiple choice: the choices one per line, the right answer is the text of one choice.</item>
/// <item>True or false: the right answer is True or False.</item>
/// <item>Short answer and identification: the student types it; the right answers are separated by |.</item>
/// <item>Fill in the blank: the text has blanks (___); the right answer has the answer of each blank on its own line (a blank can have several right answers separated by |). Each right blank gives its share of the points.</item>
/// <item>Matching type: the text is Column A (one item per line), the choices are Column B (one per line), the right answer has the Column B item of each Column A line, one per line. Each right pair gives its share of the points.</item>
/// <item>Multiple select: the choices one per line, the right answer is every right choice, one per line; all of them and no other one gives the points.</item>
/// <item>Essay: the student writes; it has no right answer and the teacher gives the score.</item>
/// </list>
/// </summary>
public static partial class QuestionRules
{
    /// <summary>The most characters of an answer that is kept (an essay is longer than a short answer).</summary>
    public const int MaxAnswerLength = 4000;

    /// <summary>The blanks of a text: three or more underscores in a row.</summary>
    [GeneratedRegex("_{3,}")]
    private static partial Regex BlankPattern();

    public static int BlankCount(string? text) => BlankPattern().Matches(text ?? "").Count;

    /// <summary>The text cut at its blanks: the pieces around them (one more than the blanks).</summary>
    public static IReadOnlyList<string> SplitAtBlanks(string? text) => BlankPattern().Split(text ?? "");

    public static List<string> Lines(string? text) =>
        (text ?? "").Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();

    /// <summary>The lines of a text, empty ones kept (the answer of each blank or of each Column A line has its place even when it is empty).</summary>
    public static List<string> Slots(string? text) =>
        (text ?? "").Replace("\r", "").Split('\n').Select(l => l.Trim()).ToList();

    /// <summary>The right answer as it is kept in the database, from what the editor gave.</summary>
    public static (string? Options, string Correct) ToStored(QuestionInput q) => q.Kind switch
    {
        QuestionKinds.MultipleChoice => (string.Join('\n', ModuleService.SplitOptions(q.OptionsText)), ModuleService.SplitOptions(q.OptionsText).First(o => string.Equals(o, q.CorrectAnswer.Trim(), StringComparison.OrdinalIgnoreCase))),
        QuestionKinds.MultipleSelect => (string.Join('\n', ModuleService.SplitOptions(q.OptionsText)), string.Join('\n', Lines(q.CorrectAnswer))),
        QuestionKinds.Matching => (string.Join('\n', ModuleService.SplitOptions(q.OptionsText)), string.Join('\n', Slots(q.CorrectAnswer))),
        QuestionKinds.FillInTheBlank => (null, string.Join('\n', Slots(q.CorrectAnswer))),
        QuestionKinds.Essay => (null, ""),
        _ => (null, q.CorrectAnswer.Trim()),
    };

    /// <summary>What is wrong with a question, in words for the person who writes it (nothing when it is right). <paramref name="label"/> is "Question 3".</summary>
    public static List<string> Validate(QuestionInput q, string label)
    {
        var errors = new List<string>();
        var kind = q.Kind;
        if (!QuestionKinds.All.Contains(kind))
        {
            errors.Add($"{label}: unknown kind of question.");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(q.Text) || q.Text.Trim().Length > 1000)
        {
            errors.Add($"{label}: write the {(kind == QuestionKinds.Matching ? "Column A items" : "question")} (up to 1000 characters).");
        }

        if (q.Points is <= 0 or > 100)
        {
            errors.Add($"{label}: the points must be between 0.01 and 100.");
        }

        switch (kind)
        {
            case QuestionKinds.MultipleChoice:
                var options = ModuleService.SplitOptions(q.OptionsText);
                if (options.Count < 2)
                {
                    errors.Add($"{label}: write at least two choices, one per line.");
                }
                else if (options.Distinct(StringComparer.OrdinalIgnoreCase).Count() != options.Count)
                {
                    errors.Add($"{label}: two choices are the same.");
                }
                else if (!options.Contains(q.CorrectAnswer.Trim(), StringComparer.OrdinalIgnoreCase))
                {
                    errors.Add($"{label}: pick the right choice.");
                }

                break;
            case QuestionKinds.TrueFalse:
                if (q.CorrectAnswer is not ("True" or "False"))
                {
                    errors.Add($"{label}: say whether the statement is true or false.");
                }

                break;
            case QuestionKinds.ShortAnswer:
            case QuestionKinds.Identification:
                if (string.IsNullOrWhiteSpace(q.CorrectAnswer) || q.CorrectAnswer.Length > 500)
                {
                    errors.Add($"{label}: write the right answer (several can be separated by |).");
                }

                break;
            case QuestionKinds.FillInTheBlank:
                var blanks = BlankCount(q.Text);
                var answers = Slots(q.CorrectAnswer);
                if (blanks == 0)
                {
                    errors.Add($"{label}: write at least one blank in the sentence with three underscores (___).");
                }
                else if (answers.Count != blanks || answers.Any(a => a.Length == 0))
                {
                    errors.Add($"{label}: write the answer of each of the {blanks} {(blanks == 1 ? "blank" : "blanks")}.");
                }
                else if (q.CorrectAnswer.Length > 1500)
                {
                    errors.Add($"{label}: the answers are too long.");
                }

                break;
            case QuestionKinds.MultipleSelect:
                var choices = ModuleService.SplitOptions(q.OptionsText);
                var right = Lines(q.CorrectAnswer);
                if (choices.Count < 2)
                {
                    errors.Add($"{label}: write at least two choices, one per line.");
                }
                else if (choices.Distinct(StringComparer.OrdinalIgnoreCase).Count() != choices.Count)
                {
                    errors.Add($"{label}: two choices are the same.");
                }
                else if (right.Count == 0 || right.Any(r => !choices.Contains(r, StringComparer.OrdinalIgnoreCase)))
                {
                    errors.Add($"{label}: tick every right choice (at least one).");
                }

                break;
            case QuestionKinds.Matching:
                var columnA = Lines(q.Text);
                var columnB = ModuleService.SplitOptions(q.OptionsText);
                var pairs = Slots(q.CorrectAnswer);
                if (columnA.Count < 2 || columnB.Count < 2)
                {
                    errors.Add($"{label}: Column A and Column B need at least two rows each.");
                }
                else if (columnB.Distinct(StringComparer.OrdinalIgnoreCase).Count() != columnB.Count)
                {
                    errors.Add($"{label}: two rows of Column B are the same.");
                }
                else if (pairs.Count != columnA.Count || pairs.Any(p => !columnB.Contains(p, StringComparer.OrdinalIgnoreCase)))
                {
                    errors.Add($"{label}: choose the right Column B row for every Column A row.");
                }

                break;
            case QuestionKinds.Essay:
                break;
        }

        return errors;
    }

    /// <summary>What is wrong with the pictures of a question (the size, the kind of file).</summary>
    public static List<string> ValidatePictures(QuestionInput q, string label)
    {
        var errors = new List<string>();
        foreach (var (picture, what) in new[] { (q.Image, "the question") }.Concat(q.OptionImages.Select((p, n) => (p, $"choice {n + 1}"))))
        {
            if (picture is null)
            {
                continue;
            }

            if (picture.Data.Length == 0 || picture.Data.Length > QuestionPicture.MaxBytes)
            {
                errors.Add($"{label}: the picture of {what} has to be at most {QuestionPicture.MaxBytes / (1024 * 1024)} MB.");
            }
            else if (!QuestionPicture.Types.Contains(picture.ContentType))
            {
                errors.Add($"{label}: the picture of {what} has to be a PNG, JPG, GIF or WebP file.");
            }
        }

        return errors;
    }

    /// <summary>The pictures of one question as rows of slots: 0 for the question, 1 and up for the choices.</summary>
    public static IEnumerable<(int Slot, QuestionPicture Picture)> SlotsOf(QuestionInput q)
    {
        if (q.Image is { } image)
        {
            yield return (0, image);
        }

        var count = QuestionKinds.HasOptions(q.Kind) ? ModuleService.SplitOptions(q.OptionsText).Count : 0;
        for (var n = 0; n < q.OptionImages.Count && n < count; n++)
        {
            if (q.OptionImages[n] is { } picture)
            {
                yield return (n + 1, picture);
            }
        }
    }

    /// <summary>Puts the stored pictures (slot and file) back on a question the editor shows.</summary>
    public static void AttachPictures(QuestionInput q, IEnumerable<(int Slot, QuestionPicture Picture)> stored)
    {
        var count = QuestionKinds.HasOptions(q.Kind) ? ModuleService.SplitOptions(q.OptionsText).Count : 0;
        q.OptionImages = [.. Enumerable.Repeat<QuestionPicture?>(null, count)];
        foreach (var (slot, picture) in stored)
        {
            if (slot == 0)
            {
                q.Image = picture;
            }
            else if (slot <= count)
            {
                q.OptionImages[slot - 1] = picture;
            }
        }
    }

    /// <summary>The points an answer earns (0 to <paramref name="points"/>). An essay earns nothing here: the teacher scores it.</summary>
    public static decimal Earned(string kind, string text, string? optionsText, string correct, decimal points, string? given)
    {
        if (string.IsNullOrWhiteSpace(given) || kind == QuestionKinds.Essay)
        {
            return 0;
        }

        switch (kind)
        {
            case QuestionKinds.ShortAnswer:
            case QuestionKinds.Identification:
                return AnyOf(correct, given) ? points : 0;
            case QuestionKinds.FillInTheBlank:
            {
                var rights = Slots(correct);
                var answers = Slots(given);
                var good = rights.Select((r, n) => n < answers.Count && AnyOf(r, answers[n])).Count(ok => ok);
                return rights.Count == 0 ? 0 : Math.Round(points * good / rights.Count, 2);
            }
            case QuestionKinds.MultipleSelect:
            {
                var rights = Lines(correct).Select(Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var picked = Lines(given).Select(Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);
                return rights.Count > 0 && rights.SetEquals(picked) ? points : 0;
            }
            case QuestionKinds.Matching:
            {
                var rights = Slots(correct);
                var answers = Slots(given);
                var good = rights.Select((r, n) => n < answers.Count && answers[n].Length > 0 && string.Equals(Normalize(r), Normalize(answers[n]), StringComparison.OrdinalIgnoreCase)).Count(ok => ok);
                return rights.Count == 0 ? 0 : Math.Round(points * good / rights.Count, 2);
            }
            default:
                return string.Equals(correct.Trim(), given.Trim(), StringComparison.OrdinalIgnoreCase) ? points : 0;
        }
    }

    /// <summary>The right answer in one line, for the teacher and for the student who sees the result.</summary>
    public static string Show(string kind, string correct) => kind switch
    {
        QuestionKinds.ShortAnswer or QuestionKinds.Identification => correct.Replace("|", " / "),
        QuestionKinds.FillInTheBlank => string.Join("; ", Slots(correct).Select((a, n) => $"{n + 1}. {a.Replace("|", " / ")}")),
        QuestionKinds.MultipleSelect => string.Join("; ", Lines(correct)),
        QuestionKinds.Matching => string.Join("; ", Slots(correct).Select((a, n) => $"{n + 1} → {a}")),
        QuestionKinds.Essay => "Scored by your teacher",
        _ => correct,
    };

    /// <summary>The answer a student gave, in one line (the answers of blanks and pairs are on their own lines).</summary>
    public static string ShowGiven(string kind, string? given)
    {
        if (string.IsNullOrWhiteSpace(given))
        {
            return "";
        }

        return kind switch
        {
            QuestionKinds.FillInTheBlank => string.Join("; ", Slots(given).Select((a, n) => $"{n + 1}. {a}")),
            QuestionKinds.MultipleSelect => string.Join("; ", Lines(given)),
            QuestionKinds.Matching => string.Join("; ", Slots(given).Select((a, n) => $"{n + 1} → {(a.Length == 0 ? "—" : a)}")),
            _ => given,
        };
    }

    private static bool AnyOf(string alternatives, string given) =>
        alternatives.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Any(a => string.Equals(Normalize(a), Normalize(given), StringComparison.OrdinalIgnoreCase));

    private static string Normalize(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
