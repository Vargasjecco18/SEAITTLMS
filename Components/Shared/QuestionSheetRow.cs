using System.Globalization;
using SEAITT_LMS.Data.Entities;
using SEAITT_LMS.Services;

namespace SEAITT_LMS.Components.Shared;

/// <summary>
/// One row of the sheet of questions (<see cref="QuestionsSheetDialog"/>), with everything the box of a question can edit. The sheet shows the first four choices in its columns; the box of the row
/// has all of them, the pictures, and the two columns of a matching type. <see cref="ToQuestion"/> gives the question the services keep.
/// </summary>
public sealed class QuestionSheetRow
{
    /// <summary>The most choices (or Column B rows) a question can have in the box.</summary>
    public const int MaxOptions = 8;

    /// <summary>The most Column A rows of a matching type.</summary>
    public const int MaxPairs = 10;

    public int Id { get; init; }
    public string Text { get; set; } = "";
    public string Kind { get; set; } = QuestionKinds.MultipleChoice;

    /// <summary>The choices (multiple choice, multiple select) or the rows of Column B (matching). At least four places: the sheet has four columns for them.</summary>
    public List<string> Options { get; set; } = ["", "", "", ""];

    /// <summary>The Column A rows of a matching type (the text of the question is not used for it).</summary>
    public List<string> ColumnA { get; set; } = ["", ""];

    /// <summary>The right Column B row of each Column A row (the text of the row), same places as <see cref="ColumnA"/>.</summary>
    public List<string> Pairs { get; set; } = ["", ""];

    /// <summary>The right answer of a multiple choice (the letter A to H) or of a true or false (True or False).</summary>
    public string Right { get; set; } = "";

    /// <summary>The places (0 is A) of the right choices of a multiple select.</summary>
    public HashSet<int> RightSet { get; set; } = [];

    /// <summary>The right answer of a short answer or an identification (several accepted ones separated by |).</summary>
    public string Short { get; set; } = "";

    /// <summary>The answers of the blanks of a fill in the blank, in the order of the blanks.</summary>
    public List<string> Blanks { get; set; } = [];

    public string Points { get; set; } = "1";

    public QuestionPicture? Image { get; set; }

    /// <summary>The pictures of the choices (or of the rows of Column B), same places as <see cref="Options"/>.</summary>
    public List<QuestionPicture?> OptionImages { get; set; } = [null, null, null, null];

    public decimal PointsValue => decimal.TryParse(Points, NumberStyles.Number, CultureInfo.InvariantCulture, out var p) ? p : 0;

    public static string Letter(int n) => ((char)('A' + n)).ToString();

    /// <summary>The list has the place (the lists of the row always have the same length as the one they go with).</summary>
    public void Fit()
    {
        while (Options.Count < 4)
        {
            Options.Add("");
        }

        while (OptionImages.Count < Options.Count)
        {
            OptionImages.Add(null);
        }

        if (OptionImages.Count > Options.Count)
        {
            OptionImages.RemoveRange(Options.Count, OptionImages.Count - Options.Count);
        }

        while (ColumnA.Count < 2)
        {
            ColumnA.Add("");
        }

        while (Pairs.Count < ColumnA.Count)
        {
            Pairs.Add("");
        }

        if (Pairs.Count > ColumnA.Count)
        {
            Pairs.RemoveRange(ColumnA.Count, Pairs.Count - ColumnA.Count);
        }
    }

    public bool IsFilled =>
        Text.Trim().Length > 0 || Options.Any(o => o.Trim().Length > 0) || ColumnA.Any(a => a.Trim().Length > 0) || Right.Length > 0 || RightSet.Count > 0
        || Short.Trim().Length > 0 || Blanks.Any(b => b.Trim().Length > 0) || Image is not null;

    /// <summary>The number of blanks in the sentence of a fill in the blank.</summary>
    public int BlankCount => QuestionRules.BlankCount(Text);

    /// <summary>A copy to edit in the box of the row (Cancel throws it away).</summary>
    public QuestionSheetRow Clone() => new()
    {
        Id = Id, Text = Text, Kind = Kind, Options = [.. Options], ColumnA = [.. ColumnA], Pairs = [.. Pairs], Right = Right, RightSet = [.. RightSet], Short = Short,
        Blanks = [.. Blanks], Points = Points, Image = Image, OptionImages = [.. OptionImages],
    };

    public void CopyFrom(QuestionSheetRow other)
    {
        Text = other.Text;
        Kind = other.Kind;
        Options = [.. other.Options];
        ColumnA = [.. other.ColumnA];
        Pairs = [.. other.Pairs];
        Right = other.Right;
        RightSet = [.. other.RightSet];
        Short = other.Short;
        Blanks = [.. other.Blanks];
        Points = other.Points;
        Image = other.Image;
        OptionImages = [.. other.OptionImages];
        Fit();
    }

    /// <summary>A new type clears what only fits the old one (the right answer, the choices when the new type has none).</summary>
    public void ChangeKind(string kind)
    {
        if (kind == Kind || !QuestionKinds.All.Contains(kind))
        {
            return;
        }

        var hadChoices = QuestionKinds.HasOptions(Kind) && Kind != QuestionKinds.Matching;
        var keepsChoices = kind is QuestionKinds.MultipleChoice or QuestionKinds.MultipleSelect;
        Kind = kind;
        Right = "";
        RightSet = [];
        Short = "";
        Blanks = [];
        if (!(hadChoices && keepsChoices))
        {
            Options = ["", "", "", ""];
            OptionImages = [null, null, null, null];
        }

        if (kind == QuestionKinds.Matching)
        {
            ColumnA = Text.Trim().Length > 0 ? [.. QuestionRules.Lines(Text), ""] : ["", ""];
            Pairs = ColumnA.Select(_ => "").ToList();
        }

        Fit();
    }

    public static QuestionSheetRow From(QuestionInput q, int id)
    {
        var row = new QuestionSheetRow { Id = id, Text = q.Text, Kind = q.Kind, Points = q.Points.ToString("0.##", CultureInfo.InvariantCulture), Image = q.Image };
        switch (q.Kind)
        {
            case QuestionKinds.MultipleChoice:
            case QuestionKinds.MultipleSelect:
                row.Options = ModuleService.SplitOptions(q.OptionsText);
                if (q.Kind == QuestionKinds.MultipleChoice)
                {
                    var at = row.Options.FindIndex(o => string.Equals(o, q.CorrectAnswer.Trim(), StringComparison.OrdinalIgnoreCase));
                    row.Right = at is >= 0 and < MaxOptions ? Letter(at) : "";
                }
                else
                {
                    var right = QuestionRules.Lines(q.CorrectAnswer);
                    row.RightSet = [.. row.Options.Select((o, n) => (o, n)).Where(x => right.Contains(x.o, StringComparer.OrdinalIgnoreCase)).Select(x => x.n)];
                }

                break;
            case QuestionKinds.Matching:
                row.ColumnA = QuestionRules.Lines(q.Text);
                row.Options = ModuleService.SplitOptions(q.OptionsText);
                row.Pairs = QuestionRules.Slots(q.CorrectAnswer);
                break;
            case QuestionKinds.TrueFalse:
                row.Right = q.CorrectAnswer;
                break;
            case QuestionKinds.FillInTheBlank:
                row.Blanks = QuestionRules.Slots(q.CorrectAnswer);
                break;
            case QuestionKinds.Essay:
                break;
            default:
                row.Short = q.CorrectAnswer;
                break;
        }

        row.OptionImages = [.. q.OptionImages];
        row.Fit();
        return row;
    }

    /// <summary>The question to keep: empty choices are left out, and the pictures, the right choices and the pairs follow their choices to their new places.</summary>
    public QuestionInput ToQuestion()
    {
        var question = new QuestionInput { Text = Text.Trim(), Kind = Kind, Points = PointsValue, Image = Image };
        switch (Kind)
        {
            case QuestionKinds.MultipleChoice:
            case QuestionKinds.MultipleSelect:
            {
                var kept = Options.Select((o, n) => (Text: o.Trim(), At: n)).Where(x => x.Text.Length > 0).ToList();
                question.OptionsText = string.Join('\n', kept.Select(x => x.Text));
                question.OptionImages = [.. kept.Select(x => x.At < OptionImages.Count ? OptionImages[x.At] : null)];
                question.CorrectAnswer = Kind == QuestionKinds.MultipleChoice
                    ? (Right.Length == 1 && Right[0] - 'A' is >= 0 and < MaxOptions && Right[0] - 'A' < Options.Count ? Options[Right[0] - 'A'].Trim() : "")
                    : string.Join('\n', kept.Where(x => RightSet.Contains(x.At)).Select(x => x.Text));
                break;
            }

            case QuestionKinds.Matching:
            {
                var rowsA = ColumnA.Select((a, n) => (Text: a.Trim(), At: n)).Where(x => x.Text.Length > 0).ToList();
                var keptB = Options.Select((o, n) => (Text: o.Trim(), At: n)).Where(x => x.Text.Length > 0).ToList();
                question.Text = string.Join('\n', rowsA.Select(x => x.Text));
                question.OptionsText = string.Join('\n', keptB.Select(x => x.Text));
                question.OptionImages = [.. keptB.Select(x => x.At < OptionImages.Count ? OptionImages[x.At] : null)];
                question.CorrectAnswer = string.Join('\n', rowsA.Select(x => x.At < Pairs.Count ? Pairs[x.At].Trim() : ""));
                break;
            }

            case QuestionKinds.TrueFalse:
                question.CorrectAnswer = Right;
                break;
            case QuestionKinds.FillInTheBlank:
            {
                var blanks = BlankCount;
                question.CorrectAnswer = string.Join('\n', Enumerable.Range(0, blanks).Select(n => n < Blanks.Count ? Blanks[n].Trim() : ""));
                break;
            }

            case QuestionKinds.Essay:
                break;
            default:
                question.CorrectAnswer = Short.Trim();
                break;
        }

        return question;
    }

    /// <summary>What is wrong with the row, in words (nothing when it is ready).</summary>
    public List<string> Problems()
    {
        var problems = new List<string>();
        if (Points.Trim().Length > 0 && PointsValue <= 0)
        {
            problems.Add("The points must be a number from 0.01 to 100.");
            return problems;
        }

        var question = ToQuestion();
        if (Points.Trim().Length == 0)
        {
            question.Points = 1;
        }

        if (Kind == QuestionKinds.MultipleChoice && Right.Length == 0 && Options.Count(o => o.Trim().Length > 0) >= 2)
        {
            problems.Add("Choose the right answer.");
            return problems;
        }

        problems.AddRange(ModuleService.ValidateQuestions([question]).Select(e => e.Replace("Question 1: ", "")));
        return problems;
    }
}
