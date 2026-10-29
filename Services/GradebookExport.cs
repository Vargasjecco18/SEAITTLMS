using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>
/// The Excel file of a gradebook: the scores of every student, and the calculation of the grade written out with formulas, so a teacher (or a parent, or the school)
/// can see how each average was worked out and change a score to see what happens. The sheet "Grades" has a column for every assignment, quiz and task performance and,
/// for each student, the points earned, the points possible and the average; the sheet "Breakdown" works out the same for each kind of work apart; the sheet
/// "How it is worked out" says the rule in words. Only graded work counts: a cell that is empty is left out of the points possible, exactly as the gradebook does.
/// </summary>
public static class GradebookExport
{
    private static readonly string[] Kinds = ItemKinds.Listed;

    private const int KindRow = 5;
    private const int TitleRow = 6;
    private const int MaxRowOfHeader = 7;
    private const int FirstStudentRow = 8;
    private const int FirstItemColumn = 3;

    public static string FileName(SubjectGradebook book) => FileDownloads.CleanName($"Gradebook - {book.SubjectName} {book.SectionLabel}.xlsx");

    private static string ShortKind(string kind) => ItemKinds.Code(kind);

    public static byte[] Build(SubjectGradebook book)
    {
        var workbook = new XlsxBook();
        var grades = workbook.AddSheet("Grades");
        var n = book.Columns.Count;
        var lastItem = FirstItemColumn + Math.Max(n, 1) - 1;
        var earnedColumn = lastItem + 1;
        var possibleColumn = lastItem + 2;
        var averageColumn = lastItem + 3;
        var finalColumn = lastItem + 4;
        var itemFirst = XlsxSheet.ColumnName(FirstItemColumn);
        var itemLast = XlsxSheet.ColumnName(lastItem);

        grades.Set(1, 1, $"Gradebook · {book.SubjectName} · {book.SectionLabel}", XlsxStyle.Title);
        grades.Set(2, 1, "Average = points earned ÷ points possible of the graded work. A cell with no score is not graded yet and is left out. The formulas are alive: change a score and the numbers change.", XlsxStyle.Note);

        // The titles of the columns: the code, the kind, the name of the work and its highest score.
        grades.Row(4, ["Student", "LRN"], XlsxStyle.Header);
        grades.Set(KindRow, 1, "Kind", XlsxStyle.Bold).Set(TitleRow, 1, "Work", XlsxStyle.Bold).Set(MaxRowOfHeader, 1, "Highest score", XlsxStyle.Bold);
        var counters = new Dictionary<string, int>();
        for (var i = 0; i < n; i++)
        {
            var column = book.Columns[i];
            var c = FirstItemColumn + i;
            counters[column.Kind] = counters.GetValueOrDefault(column.Kind) + 1;
            grades.Set(4, c, $"{ShortKind(column.Kind)}{counters[column.Kind]}", XlsxStyle.Header);
            grades.Set(KindRow, c, ItemKinds.Display(column.Kind));
            grades.Set(TitleRow, c, column.Title, XlsxStyle.Wrap);
            grades.Set(MaxRowOfHeader, c, column.Max, XlsxStyle.Decimal);
            grades.Width(c, 13);
        }

        grades.Set(4, earnedColumn, "Points earned", XlsxStyle.Header).Set(4, possibleColumn, "Points possible", XlsxStyle.Header)
            .Set(4, averageColumn, "Average (%)", XlsxStyle.Header).Set(4, finalColumn, "Final grade", XlsxStyle.Header);
        grades.Width(1, 30).Width(2, 16).Width(earnedColumn, 14).Width(possibleColumn, 15).Width(averageColumn, 13).Width(finalColumn, 12);

        var maxOf = book.Columns.Select(c => c.Max).ToList();
        var row = FirstStudentRow;
        foreach (var student in book.Rows)
        {
            grades.Set(row, 1, student.Name).Set(row, 2, student.Lrn ?? "", XlsxStyle.Text);
            decimal earned = 0, possible = 0;
            for (var i = 0; i < n; i++)
            {
                var cell = student.Cells[i];
                if (cell.Status == SubmissionStatus.Graded && cell.Score is { } score)
                {
                    grades.Set(row, FirstItemColumn + i, score, XlsxStyle.Decimal);
                    earned += score;
                    possible += maxOf[i];
                }
                else
                {
                    grades.Set(row, FirstItemColumn + i, null, XlsxStyle.Decimal);
                }
            }

            var earnedCell = XlsxSheet.Ref(row, earnedColumn);
            var possibleCell = XlsxSheet.Ref(row, possibleColumn);
            var range = $"{itemFirst}{row}:{itemLast}{row}";
            var maxRange = $"{itemFirst}${MaxRowOfHeader}:{itemLast}${MaxRowOfHeader}";
            grades.Formula(row, earnedColumn, $"SUM({range})", XlsxStyle.BoldDecimal, (double)earned);
            grades.Formula(row, possibleColumn, $"SUMPRODUCT(--({range}<>\"\"),{maxRange})", XlsxStyle.BoldDecimal, (double)possible);
            grades.Formula(row, averageColumn, $"IF({possibleCell}=0,\"\",ROUND({earnedCell}/{possibleCell}*100,1))", XlsxStyle.BoldDecimal, possible == 0 ? null : (double)Math.Round(earned / possible * 100, 1));
            grades.Set(row, finalColumn, student.FinalGrade, XlsxStyle.Decimal);
            row++;
        }

        grades.FrozenRows = MaxRowOfHeader;
        grades.FrozenColumns = 2;

        // ---- the breakdown: the same calculation for each kind of work apart
        var breakdown = workbook.AddSheet("Breakdown");
        breakdown.Set(1, 1, $"Breakdown of the grades · {book.SubjectName} · {book.SectionLabel}", XlsxStyle.Title);
        breakdown.Set(2, 1, "Points earned and possible of each kind of work, worked out with formulas from the sheet Grades.", XlsxStyle.Note);
        breakdown.Row(4, ["Student", "LRN"], XlsxStyle.Header);
        var column2 = 3;
        var kindColumns = new Dictionary<string, (int Earned, int Possible, int Percent)>();
        foreach (var kind in Kinds)
        {
            breakdown.Set(4, column2, $"{ItemKinds.Plural(kind)} earned", XlsxStyle.Header).Set(4, column2 + 1, $"{ItemKinds.Plural(kind)} possible", XlsxStyle.Header).Set(4, column2 + 2, $"{ItemKinds.Plural(kind)} (%)", XlsxStyle.Header);
            kindColumns[kind] = (column2, column2 + 1, column2 + 2);
            column2 += 3;
        }

        var totalEarned = column2;
        var totalPossible = column2 + 1;
        var totalAverage = column2 + 2;
        var totalFinal = column2 + 3;
        breakdown.Set(4, totalEarned, "Total earned", XlsxStyle.Header).Set(4, totalPossible, "Total possible", XlsxStyle.Header)
            .Set(4, totalAverage, "Average (%)", XlsxStyle.Header).Set(4, totalFinal, "Final grade", XlsxStyle.Header);
        breakdown.Width(1, 30).Width(2, 16);
        for (var c = 3; c <= totalFinal; c++)
        {
            breakdown.Width(c, 14);
        }

        var kindRange = $"Grades!${itemFirst}${KindRow}:${itemLast}${KindRow}";
        var maxRowRange = $"Grades!${itemFirst}${MaxRowOfHeader}:${itemLast}${MaxRowOfHeader}";
        row = 5;
        var gradesRow = FirstStudentRow;
        foreach (var student in book.Rows)
        {
            breakdown.Set(row, 1, student.Name).Set(row, 2, student.Lrn ?? "", XlsxStyle.Text);
            decimal allEarned = 0, allPossible = 0;
            foreach (var kind in Kinds)
            {
                decimal earned = 0, possible = 0;
                for (var i = 0; i < n; i++)
                {
                    if (book.Columns[i].Kind == kind && student.Cells[i] is { Status: SubmissionStatus.Graded, Score: { } score })
                    {
                        earned += score;
                        possible += maxOf[i];
                    }
                }

                allEarned += earned;
                allPossible += possible;
                var (e, p, pc) = kindColumns[kind];
                var name = ItemKinds.Display(kind);
                var scores = $"Grades!{itemFirst}{gradesRow}:{itemLast}{gradesRow}";
                breakdown.Formula(row, e, $"SUMIF({kindRange},\"{name}\",{scores})", XlsxStyle.Decimal, (double)earned);
                breakdown.Formula(row, p, $"SUMPRODUCT(({kindRange}=\"{name}\")*({scores}<>\"\")*{maxRowRange})", XlsxStyle.Decimal, (double)possible);
                breakdown.Formula(row, pc, $"IF({XlsxSheet.Ref(row, p)}=0,\"\",ROUND({XlsxSheet.Ref(row, e)}/{XlsxSheet.Ref(row, p)}*100,1))", XlsxStyle.Decimal, possible == 0 ? null : (double)Math.Round(earned / possible * 100, 1));
            }

            var earnedParts = string.Join("+", kindColumns.Values.Select(k => XlsxSheet.Ref(row, k.Earned)));
            var possibleParts = string.Join("+", kindColumns.Values.Select(k => XlsxSheet.Ref(row, k.Possible)));
            breakdown.Formula(row, totalEarned, earnedParts, XlsxStyle.BoldDecimal, (double)allEarned);
            breakdown.Formula(row, totalPossible, possibleParts, XlsxStyle.BoldDecimal, (double)allPossible);
            breakdown.Formula(row, totalAverage, $"IF({XlsxSheet.Ref(row, totalPossible)}=0,\"\",ROUND({XlsxSheet.Ref(row, totalEarned)}/{XlsxSheet.Ref(row, totalPossible)}*100,1))", XlsxStyle.BoldDecimal,
                allPossible == 0 ? null : (double)Math.Round(allEarned / allPossible * 100, 1));
            breakdown.Formula(row, totalFinal, $"IF(Grades!{XlsxSheet.Ref(gradesRow, finalColumn)}=\"\",\"\",Grades!{XlsxSheet.Ref(gradesRow, finalColumn)})", XlsxStyle.Decimal, student.FinalGrade is { } f ? (double)f : null);
            row++;
            gradesRow++;
        }

        breakdown.FrozenRows = 4;
        breakdown.FrozenColumns = 2;

        // ---- the rule in words
        var how = workbook.AddSheet("How it is worked out");
        how.Set(1, 1, "How the grades are worked out", XlsxStyle.Title);
        var lines = new[]
        {
            "1. Every graded assignment, quiz and task performance has a score and a highest score (the row Highest score in the sheet Grades).",
            "2. Points earned = the sum of the scores of the student.",
            "3. Points possible = the sum of the highest scores of the work that has a score. Work that is not graded yet is left out, so it does not lower the average.",
            "4. Average (%) = points earned ÷ points possible × 100, rounded to one decimal.",
            $"5. A student whose average is below {GradeRules.PassingPercent:0}% is failing.",
            "6. The sheet Breakdown does the same for each kind of work apart (assignments, quizzes, task performances) and then for all of it together.",
            "7. The final grade is the number the teacher wrote in the gradebook (it is shown next to the average and is not worked out from it).",
        };
        for (var i = 0; i < lines.Length; i++)
        {
            how.Set(3 + i, 1, lines[i], XlsxStyle.Wrap);
        }

        how.Width(1, 110);
        return workbook.ToBytes();
    }
}
