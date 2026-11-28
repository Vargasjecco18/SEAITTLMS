using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>
/// The Excel file of a gradebook, worked out with the grading system of SEAITT (Round 46): Written/Oral Works (activities and quizzes) 20%, Performance Task 50%, Summative Test 15%, Term Examination 15% (Round 47).
/// The sheet "Grades" has a column for every activity, quiz, task performance and exam, then the percent of each of the four parts, the final grade, its descriptor and its remarks, all as live
/// formulas (change a score and the numbers change). The sheet "Summary" has the two tables of the class: the descriptors (Advancing ... Emerging) and Passed/Failed, with male, female and total.
/// The sheet "How it is worked out" says the rule in words. A student whose gradebook is not complete (a score is missing) has no final grade, as in the gradebook (round 48: the grade is 0 based).
/// The same file can be filled in and imported back (Import class grades): the first columns are Student and LRN and then the code of each column.
/// </summary>
public static class GradebookExport
{
    private const int PartRow = 5;
    private const int TitleRow = 6;
    private const int MaxRowOfHeader = 7;
    private const int FirstStudentRow = 8;
    private const int FirstItemColumn = 3;

    public static string FileName(SubjectGradebook book) => FileDownloads.CleanName($"Gradebook - {book.SubjectName} {book.SectionLabel}.xlsx");

    /// <summary>The file of every course of a teacher: the sheets Grades and Summary for each course, then the sheet that says the rule in words once.</summary>
    public static string FileNameAll() => FileDownloads.CleanName("Gradebook - all courses.xlsx");

    public static byte[] Build(SubjectGradebook book)
    {
        var workbook = new XlsxBook();
        AddCourse(workbook, book, "Grades", "Summary");
        AddRules(workbook);
        return workbook.ToBytes();
    }

    /// <summary>One workbook with all the courses: a sheet <c>1. Course</c> and a sheet <c>1. Summary Course</c> for each, and the rule in words at the end.</summary>
    public static byte[] BuildAll(IReadOnlyList<SubjectGradebook> books)
    {
        var workbook = new XlsxBook();
        for (var i = 0; i < books.Count; i++)
        {
            AddCourse(workbook, books[i], $"{i + 1}. {books[i].SubjectName}", $"{i + 1}. Summary {books[i].SubjectName}");
        }

        AddRules(workbook);
        return workbook.ToBytes();
    }

    /// <summary>A sheet name as a formula writes it: in single quotes, a quote inside it doubled.</summary>
    private static string Quote(string sheetName) => "'" + sheetName.Replace("'", "''") + "'";

    private static void AddCourse(XlsxBook workbook, SubjectGradebook book, string gradesName, string summaryName)
    {
        var grades = workbook.AddSheet(gradesName);
        var gradesRef = Quote(grades.Name);
        var n = book.Columns.Count;
        var lastItem = FirstItemColumn + Math.Max(n, 1) - 1;
        var sexColumn = lastItem + 1;
        var partColumn = new Dictionary<GradePart, int>();
        var next = sexColumn + 1;
        foreach (var part in GradingSystem.Parts)
        {
            partColumn[part] = next++;
        }

        var finalColumn = next++;
        var descriptorColumn = next++;
        var remarksColumn = next;
        var itemFirst = XlsxSheet.ColumnName(FirstItemColumn);
        var itemLast = XlsxSheet.ColumnName(lastItem);
        var weights = GradingSystem.Weights;

        grades.Set(1, 1, $"Gradebook · {book.SubjectName} · {book.SectionLabel}", XlsxStyle.Title);
        grades.Set(2, 1, $"Grading system: {GradingSystem.Describe()}. Each part is points earned ÷ points possible of its work; the grade is 0 based (a part with no scores counts 0, nothing is scaled up) and the final grade is empty until every score is in. The formulas are alive.", XlsxStyle.Note);

        // The titles of the columns: the code, the part, the name of the work and its highest score.
        grades.Row(4, ["Student", "LRN"], XlsxStyle.Header);
        grades.Set(PartRow, 1, "Part", XlsxStyle.Bold).Set(TitleRow, 1, "Work", XlsxStyle.Bold).Set(MaxRowOfHeader, 1, "Highest score", XlsxStyle.Bold);
        for (var i = 0; i < n; i++)
        {
            var column = book.Columns[i];
            var c = FirstItemColumn + i;
            grades.Set(4, c, column.Code, XlsxStyle.Header);
            grades.Set(PartRow, c, GradingSystem.Short(column.Part));
            grades.Set(TitleRow, c, column.Title, XlsxStyle.Wrap);
            grades.Set(MaxRowOfHeader, c, column.Max, XlsxStyle.Decimal);
            grades.Width(c, 13);
        }

        grades.Set(4, sexColumn, "Sex", XlsxStyle.Header).Width(sexColumn, 9);
        foreach (var part in GradingSystem.Parts)
        {
            var c = partColumn[part];
            grades.Set(4, c, $"{GradingSystem.Short(part)} (%)", XlsxStyle.Header);
            grades.Set(PartRow, c, GradingSystem.Name(part), XlsxStyle.Wrap);
            grades.Set(TitleRow, c, "weight", XlsxStyle.Note);
            grades.Set(MaxRowOfHeader, c, weights.Of(part), XlsxStyle.Decimal);
            grades.Width(c, 15);
        }

        grades.Set(4, finalColumn, "Final grade", XlsxStyle.Header).Set(4, descriptorColumn, "Descriptor", XlsxStyle.Header).Set(4, remarksColumn, "Remarks", XlsxStyle.Header);
        grades.Width(1, 30).Width(2, 16).Width(finalColumn, 12).Width(descriptorColumn, 15).Width(remarksColumn, 11);

        var partRange = $"${itemFirst}${PartRow}:${itemLast}${PartRow}";
        var maxRange = $"${itemFirst}${MaxRowOfHeader}:${itemLast}${MaxRowOfHeader}";
        var row = FirstStudentRow;
        foreach (var student in book.Rows)
        {
            grades.Set(row, 1, student.Name).Set(row, 2, student.Lrn ?? "", XlsxStyle.Text);
            for (var i = 0; i < n; i++)
            {
                var cell = student.Cells[i];
                if (cell.Status == SubmissionStatus.Graded && cell.Score is { } score)
                {
                    grades.Set(row, FirstItemColumn + i, score, XlsxStyle.Decimal);
                }
                else
                {
                    grades.Set(row, FirstItemColumn + i, null, XlsxStyle.Decimal);
                }
            }

            grades.Set(row, sexColumn, student.Sex.ForFile());
            var scores = $"{itemFirst}{row}:{itemLast}{row}";

            // the percent of each part: points earned ÷ points possible of the work of that part that has a score (blank when the part has no scored work)
            foreach (var part in GradingSystem.Parts)
            {
                var code = GradingSystem.Short(part);
                var possible = $"SUMPRODUCT(({partRange}=\"{code}\")*({scores}<>\"\")*{maxRange})";
                var earned = $"SUMIF({partRange},\"{code}\",{scores})";
                var cached = student.Grade?.PartPercent(part);
                grades.Formula(row, partColumn[part], $"IF({possible}=0,\"\",ROUND({earned}/{possible}*100,2))", XlsxStyle.Decimal, cached is { } p ? (double)p : null);
            }

            // the final grade (round 48, 0 based): empty until every column has a score, then each part counts out of its full weight (a part with no scored work counts 0) and the total is rounded to a whole number
            var cellOf = GradingSystem.Parts.ToDictionary(part => part, part => XlsxSheet.Ref(row, partColumn[part]));
            var weightOf = GradingSystem.Parts.ToDictionary(part => part, part => $"{XlsxSheet.ColumnName(partColumn[part])}${MaxRowOfHeader}");
            var top = string.Join("+", GradingSystem.Parts.Select(part => $"IF({cellOf[part]}=\"\",0,{cellOf[part]}*{weightOf[part]})"));
            var bottom = string.Join("+", GradingSystem.Parts.Select(part => weightOf[part]));
            var finalCell = XlsxSheet.Ref(row, finalColumn);
            var finalFormula = n == 0 ? "\"\"" : $"IF(COUNT({scores})<{n},\"\",IF(({bottom})=0,0,ROUND(({top})/({bottom}),0)))";
            grades.Formula(row, finalColumn, finalFormula, XlsxStyle.BoldDecimal, student.Final is { } f ? (double)f : null);
            grades.Formula(row, descriptorColumn, $"IF({finalCell}=\"\",\"\",IF({finalCell}>=90,\"Advancing\",IF({finalCell}>=80,\"Benchmarking\",IF({finalCell}>=75,\"Connecting\",IF({finalCell}>=65,\"Developing\",\"Emerging\")))))", XlsxStyle.Normal);
            grades.Formula(row, remarksColumn, $"IF({finalCell}=\"\",\"\",IF({finalCell}>=75,\"PASSED\",\"FAILED\"))", XlsxStyle.Normal);
            row++;
        }

        grades.FrozenRows = MaxRowOfHeader;
        grades.FrozenColumns = 2;

        // ---- the summary: the two tables of the class, male / female / total, worked out with formulas from the sheet Grades
        var summary = workbook.AddSheet(summaryName);
        var last = Math.Max(FirstStudentRow, FirstStudentRow + book.Rows.Count - 1);
        var finalRange = $"{gradesRef}!{XlsxSheet.ColumnName(finalColumn)}${FirstStudentRow}:{XlsxSheet.ColumnName(finalColumn)}${last}";
        var sexRange = $"{gradesRef}!{XlsxSheet.ColumnName(sexColumn)}${FirstStudentRow}:{XlsxSheet.ColumnName(sexColumn)}${last}";
        var (descriptorLines, remarkLines, male, female, total) = book.Summary;

        summary.Set(1, 1, $"Summary of the final grades · {book.SubjectName} · {book.SectionLabel}", XlsxStyle.Title);
        summary.Set(2, 1, "Worked out with formulas from the sheet Grades. A student whose gradebook is not complete has no final grade yet, so he or she is not in these tables.", XlsxStyle.Note);

        var r1 = 4;
        summary.Row(r1, ["DESCRIPTOR", "MALE", "FEMALE", "TOTAL", "% MALE", "% FEMALE", "% TOTAL"], XlsxStyle.Header);
        WriteTable(summary, r1 + 1, GradingSystem.Descriptors.Select(d => (d.From, d.To, d.Name)).ToList(), descriptorLines, finalRange, sexRange, male, female, total);

        var r2 = r1 + GradingSystem.Descriptors.Length + 4;
        summary.Row(r2, ["REMARKS", "MALE", "FEMALE", "TOTAL", "% MALE", "% FEMALE", "% TOTAL"], XlsxStyle.Header);
        WriteTable(summary, r2 + 1, GradingSystem.Remarks.Select(d => (d.From, d.To, d.Name.ToUpperInvariant())).ToList(), remarkLines, finalRange, sexRange, male, female, total);

        // Round 47: no blank first column and no numerical grade column: the name of the line, then male, female, total and the percents.
        summary.Width(1, 18);
        for (var c = 2; c <= 7; c++)
        {
            summary.Width(c, 11);
        }
    }

    /// <summary>The lines of one summary table and its TOTAL line: counts with COUNTIFS on the final grades, and the percents of the male, the female and all the students.</summary>
    private static void WriteTable(XlsxSheet sheet, int firstRow, List<(int From, int To, string Name)> bands, IReadOnlyList<GradingSystem.SummaryLine> lines, string finalRange, string sexRange, int male, int female, int total)
    {
        var totalRow = firstRow + bands.Count;
        for (var i = 0; i < bands.Count; i++)
        {
            var (from, to, name) = bands[i];
            var row = firstRow + i;
            var line = lines[i];
            var range = $"{finalRange},\">={from}\",{finalRange},\"<={to}\"";
            sheet.Set(row, 1, name);
            sheet.Formula(row, 2, $"COUNTIFS({sexRange},\"Male\",{range})", XlsxStyle.Integer, line.Male);
            sheet.Formula(row, 3, $"COUNTIFS({sexRange},\"Female\",{range})", XlsxStyle.Integer, line.Female);
            sheet.Formula(row, 4, $"COUNTIFS({range})", XlsxStyle.Integer, line.Total);
            sheet.Formula(row, 5, $"IF(B${totalRow}=0,0,B{row}/B${totalRow})", XlsxStyle.Percent, male == 0 ? 0 : (double)line.Male / male);
            sheet.Formula(row, 6, $"IF(C${totalRow}=0,0,C{row}/C${totalRow})", XlsxStyle.Percent, female == 0 ? 0 : (double)line.Female / female);
            sheet.Formula(row, 7, $"IF(D${totalRow}=0,0,D{row}/D${totalRow})", XlsxStyle.Percent, total == 0 ? 0 : (double)line.Total / total);
        }

        var first = firstRow;
        var lastRow = totalRow - 1;
        sheet.Set(totalRow, 1, "TOTAL", XlsxStyle.Bold);
        sheet.Formula(totalRow, 2, $"SUM(B{first}:B{lastRow})", XlsxStyle.Integer, male);
        sheet.Formula(totalRow, 3, $"SUM(C{first}:C{lastRow})", XlsxStyle.Integer, female);
        sheet.Formula(totalRow, 4, $"SUM(D{first}:D{lastRow})", XlsxStyle.Integer, total);
        sheet.Formula(totalRow, 5, $"SUM(E{first}:E{lastRow})", XlsxStyle.Percent, male == 0 ? 0 : 1);
        sheet.Formula(totalRow, 6, $"SUM(F{first}:F{lastRow})", XlsxStyle.Percent, female == 0 ? 0 : 1);
        sheet.Formula(totalRow, 7, $"SUM(G{first}:G{lastRow})", XlsxStyle.Percent, total == 0 ? 0 : 1);
    }

    /// <summary>The rule in words: one sheet at the end of the workbook, for the course (or the courses) in it.</summary>
    private static void AddRules(XlsxBook workbook)
    {
        var how = workbook.AddSheet("How it is worked out");
        how.Set(1, 1, "How the grades are worked out (the grading system of SEAITT)", XlsxStyle.Title);
        var w = GradingSystem.Weights;
        var lines = new[]
        {
            $"1. A grade has four parts: Written/Oral Works (the activities and the quizzes) {w.WrittenOral:0.##}%, Performance Task {w.Performance:0.##}%, Summative Test (the summative exam) {w.Summative:0.##}% and Term Examination (TE) {w.TermExam:0.##}%.",
            "2. The percent of a part = the points earned ÷ the points possible of the work of that part that has a score, × 100.",
            "3. The grade is 0 based: the final grade = the percent of each part × its weight, added up (the four weights make 100) and rounded to a whole number. A part with no scores counts 0; nothing is scaled up.",
            "4. The final grade is empty until every activity, quiz and exam of the gradebook has a score for the student. It is worked out only when all of them are in, and then the grade is 0 based: each part counts out of its full weight, and a part with no scores counts 0 (nothing is scaled up).",
            "5. A quiz or an exam with questions is checked by the system; only an essay is scored by the teacher.",
            "6. Descriptors: 90-100 Advancing, 80-89 Benchmarking, 75-79 Connecting, 65-74 Developing, 0-64 Emerging. Remarks: 75-100 PASSED, 0-74 FAILED.",
            "7. The sheet Summary counts the students of each descriptor and of Passed/Failed, male, female and in all, with their percents.",
            "8. To give the scores from a file: type them in the sheet Grades (keep the first two columns, Student and LRN, and the code of each column) and use Import class grades in the gradebook.",
        };
        for (var i = 0; i < lines.Length; i++)
        {
            how.Set(3 + i, 1, lines[i], XlsxStyle.Wrap);
        }

        how.Width(1, 110);
    }
}
