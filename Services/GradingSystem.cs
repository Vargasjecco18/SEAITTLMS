using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>The four parts of the grading system of SEAITT.</summary>
public enum GradePart
{
    /// <summary>Written/Oral Works: the activities (assignments, seatwork, recitation, others) and the quizzes.</summary>
    WrittenOral,

    /// <summary>Performance Task.</summary>
    Performance,

    /// <summary>Summative Test: the summative exam of the academic provider.</summary>
    Summative,

    /// <summary>Term Examination (TE): the exam of the term, added from the gradebook.</summary>
    TermExam,
}

/// <summary>How much each part is worth. The school's are 20 / 50 / 15 / 15 (they add up to 100, so the grade is the weighted average; if the administrator changes them so they no longer add up to 100 the grade is scaled to 100%); the administrator can change them in System Settings.</summary>
public sealed record GradingWeights(decimal WrittenOral, decimal Performance, decimal Summative, decimal TermExam)
{
    public static GradingWeights Default { get; } = new(20, 50, 15, 15);

    public decimal Of(GradePart part) => part switch
    {
        GradePart.WrittenOral => WrittenOral,
        GradePart.Performance => Performance,
        GradePart.Summative => Summative,
        _ => TermExam,
    };

    public decimal Total => WrittenOral + Performance + Summative + TermExam;
}

/// <summary>One column of the gradebook for one student: what kind of work it is, its highest score and the score (empty while the work has no score yet).</summary>
public sealed record GradeWork(string Kind, bool Summative, decimal Max, decimal? Score);

/// <param name="Parts">The percent of each part that has work (points earned ÷ points possible of the graded work); a part with no work is not in the list.</param>
/// <param name="Running">The average, scaled to 100%, of the parts that have graded work: how the student is doing so far (empty when nothing is graded yet). The analytics and the warnings use it; it is not the grade.</param>
/// <param name="Final">Round 48, the final grade, a whole number: the 0 based total (see <paramref name="Weighted"/>) rounded, once every score of the gradebook is in. Empty before that, and when the gradebook has no work yet.</param>
/// <param name="Missing">How many columns still have no score.</param>
/// <param name="Weighted">Round 48, the 0 based grade: every part counts out of its full weight and a part with no scores counts 0 (nothing is scaled up). Empty when nothing is graded yet.</param>
public sealed record GradeResult(IReadOnlyDictionary<GradePart, decimal> Parts, decimal? Running, int? Final, int Missing, int Total, decimal? Weighted = null)
{
    /// <summary>True when the gradebook has work and every column has a score for this student.</summary>
    public bool Complete => Total > 0 && Missing == 0;

    public decimal? PartPercent(GradePart part) => Parts.TryGetValue(part, out var p) ? p : null;
}

/// <summary>
/// The grading system of SEAITT (Round 46): Written/Oral Works 20%, Performance Task 50%, Summative Test 15%, Term Examination 15% (Round 47: it was 30%). Round 48: it is 0 based. The grade of a student in a course
/// is the percent of each of the four parts times its weight, added up: a part with no scores yet counts 0 and nothing is scaled up to 100%.
/// The final grade of the gradebook only exists when every activity, quiz and exam has a score; before that there is no final grade (so no descriptor, no Passed and no Failed).
/// </summary>
public static class GradingSystem
{
    private static volatile GradingWeights weights = GradingWeights.Default;

    /// <summary>The weights now (System Settings keeps them in the database and puts them here when the app starts and when they are saved).</summary>
    public static GradingWeights Weights => weights;

    public static void Use(GradingWeights value) => weights = value;

    public static readonly GradePart[] Parts = [GradePart.WrittenOral, GradePart.Performance, GradePart.Summative, GradePart.TermExam];

    public static string Name(GradePart part) => part switch
    {
        GradePart.WrittenOral => "Written/Oral Works",
        GradePart.Performance => "Performance Task",
        GradePart.Summative => "Summative Test",
        _ => "Term Examination",
    };

    public static string Short(GradePart part) => part switch
    {
        GradePart.WrittenOral => "WW",
        GradePart.Performance => "PT",
        GradePart.Summative => "ST",
        _ => "TE",
    };

    /// <summary>Which part a column belongs to: the exam of the academic provider is the Summative Test, an exam of the teacher is the Term Examination, a task performance is the Performance Task, the rest are Written/Oral Works.</summary>
    public static GradePart PartOf(string kind, bool summative) => kind switch
    {
        ItemKinds.TaskPerformance => GradePart.Performance,
        ItemKinds.Exam => summative ? GradePart.Summative : GradePart.TermExam,
        _ => GradePart.WrittenOral,
    };

    /// <summary>"Written/Oral Works 20% · Performance Task 50% · Summative Test 15% · Term Examination 15%".</summary>
    public static string Describe() => string.Join(" · ", Parts.Select(p => $"{Name(p)} {weights.Of(p):0.##}%"));

    public static GradeResult Compute(IEnumerable<GradeWork> works) => Compute(works, weights);

    public static GradeResult Compute(IEnumerable<GradeWork> works, GradingWeights w)
    {
        var all = works.ToList();
        var parts = new Dictionary<GradePart, decimal>();
        foreach (var part in Parts)
        {
            var graded = all.Where(x => PartOf(x.Kind, x.Summative) == part && x.Score is not null && x.Max > 0).ToList();
            var possible = graded.Sum(x => x.Max);
            if (possible > 0)
            {
                parts[part] = Math.Round(graded.Sum(x => x.Score!.Value) / possible * 100, 2);
            }
        }

        var weightSum = parts.Keys.Sum(p => w.Of(p));
        decimal? running = parts.Count == 0 || weightSum <= 0 ? null : Math.Round(parts.Sum(kv => kv.Value * w.Of(kv.Key)) / weightSum, 2);

        // Round 48: the grade is 0 based: every part counts out of its full weight (20 / 50 / 15 / 15) and a part with no scores yet counts 0. Nothing is scaled up. The final grade is that total,
        // rounded, and it only exists when every score of the gradebook is in (before that there is no final grade, so no descriptor and no Passed or Failed).
        var weightAll = Parts.Sum(p => w.Of(p));
        decimal? weighted = parts.Count == 0 || weightAll <= 0 ? null : Math.Round(parts.Sum(kv => kv.Value * w.Of(kv.Key)) / weightAll, 2);
        var missing = all.Count(x => x.Score is null);
        int? final = all.Count == 0 || missing > 0 ? null : weighted is { } t ? (int)Math.Round(t, MidpointRounding.AwayFromZero) : 0;
        return new GradeResult(parts, running, final, missing, all.Count, weighted);
    }

    // ---------------------------------------------------------------- the two tables of the summary

    /// <summary>The descriptors of a final grade, the best first: the lowest and highest grade of each and its name.</summary>
    public static readonly (int From, int To, string Name)[] Descriptors =
    [
        (90, 100, "Advancing"),
        (80, 89, "Benchmarking"),
        (75, 79, "Connecting"),
        (65, 74, "Developing"),
        (0, 64, "Emerging"),
    ];

    /// <summary>Passed from 75 to 100, Failed from 0 to 74.</summary>
    public static readonly (int From, int To, string Name)[] Remarks = [(75, 100, "Passed"), (0, 74, "Failed")];

    /// <summary>The descriptor of a grade (a decimal is rounded to the whole number first: 74.6 is 75).</summary>
    public static string Descriptor(decimal grade)
    {
        var whole = (int)Math.Round(Math.Clamp(grade, 0, 100), MidpointRounding.AwayFromZero);
        return Descriptors.First(d => whole >= d.From).Name;
    }

    public static string Remark(decimal grade) => Math.Round(grade, MidpointRounding.AwayFromZero) >= 75 ? "Passed" : "Failed";

    /// <summary>One line of a summary table: how many male and female students are in it.</summary>
    public sealed record SummaryLine(string Range, string Name, int Male, int Female, int Total);

    /// <summary>The descriptor table and the passed/failed table for the final grades of a class. <paramref name="students"/> are the final grade (0 to 100) and the sex of each student; a student without a sex is not counted as male or female but is in the total.</summary>
    public static (IReadOnlyList<SummaryLine> Descriptor, IReadOnlyList<SummaryLine> Remark, int Male, int Female, int Total) Summarize(IEnumerable<(decimal Grade, Sex? Sex)> students)
    {
        var list = students.Select(s => (Whole: (int)Math.Round(Math.Clamp(s.Grade, 0, 100), MidpointRounding.AwayFromZero), s.Sex)).ToList();
        SummaryLine Line((int From, int To, string Name) d)
        {
            var inside = list.Where(s => s.Whole >= d.From && s.Whole <= d.To).ToList();
            return new SummaryLine($"{d.From} - {d.To}", d.Name, inside.Count(s => s.Sex == Sex.Male), inside.Count(s => s.Sex == Sex.Female), inside.Count);
        }

        return (Descriptors.Select(Line).ToList(), Remarks.Select(r => Line((r.From, r.To, r.Name.ToUpperInvariant()))).ToList(),
            list.Count(s => s.Sex == Sex.Male), list.Count(s => s.Sex == Sex.Female), list.Count);
    }
}
