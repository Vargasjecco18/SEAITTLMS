using SEAITT_LMS.Data.Entities;
using SEAITT_LMS.Services;

namespace SEAITT_LMS.Components.Shared;

/// <summary>
/// One row of the sheet of evaluation questions (<see cref="EvaluationSheetDialog"/>): the question, how the students answer it (one of the seven types), the area it is about, and the
/// options of a ranking or select-all question. <see cref="ToInput"/> gives the question the service keeps.
/// </summary>
public sealed class EvaluationSheetRow
{
    public int Id { get; init; }
    public string Text { get; set; } = "";
    public string Kind { get; set; } = EvaluationQuestionKinds.Likert;
    public string Category { get; set; } = "";

    /// <summary>The options of a ranking or select-all question (the sheet shows as many boxes as the columns it has).</summary>
    public List<string> Options { get; set; } = [];

    /// <summary>A row with nothing in it is ignored when the sheet is saved.</summary>
    public bool IsFilled => Text.Trim().Length > 0 || Options.Any(o => o.Trim().Length > 0);

    public static EvaluationSheetRow From(EvaluationQuestionInput q, int id) => new()
    {
        Id = id, Text = q.Text, Kind = q.Kind, Category = q.Category ?? "", Options = [.. q.Options],
    };

    public EvaluationQuestionInput ToInput() => new()
    {
        Text = Text.Trim(),
        Kind = Kind,
        Category = Category.Trim().Length == 0 ? null : Category.Trim(),
        Options = EvaluationQuestionKinds.HasOptions(Kind) ? EvaluationService.CleanOptions(Options) : [],
    };

    /// <summary>Makes room for the boxes of the options of the sheet (never takes an option away).</summary>
    public void Fit(int columns)
    {
        while (Options.Count < columns)
        {
            Options.Add("");
        }
    }

    /// <summary>A new type clears the options when the new type has none.</summary>
    public void ChangeKind(string kind)
    {
        Kind = kind;
        if (!EvaluationQuestionKinds.HasOptions(kind))
        {
            Options = Options.Select(_ => "").ToList();
        }
    }

    /// <summary>What is missing in this row, one short sentence each (empty when it is ready).</summary>
    public List<string> Problems()
    {
        var list = new List<string>();
        if (Text.Trim().Length == 0)
        {
            list.Add("Write the question.");
        }
        else if (Text.Trim().Length > 500)
        {
            list.Add("The question can have at most 500 characters.");
        }

        if (Category.Trim().Length > 60)
        {
            list.Add("The area can have at most 60 characters.");
        }

        if (EvaluationQuestionKinds.HasOptions(Kind))
        {
            var options = EvaluationService.CleanOptions(Options);
            if (options.Count is < EvaluationQuestionKinds.MinOptions or > EvaluationQuestionKinds.MaxOptions)
            {
                list.Add($"Write {EvaluationQuestionKinds.MinOptions} to {EvaluationQuestionKinds.MaxOptions} different options.");
            }
            else if (options.Any(o => o.Length > 120))
            {
                list.Add("An option can have at most 120 characters.");
            }
        }

        return list;
    }
}
