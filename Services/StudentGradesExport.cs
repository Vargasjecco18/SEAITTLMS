using SEAITT_LMS.Components.Shared;

namespace SEAITT_LMS.Services;

/// <summary>
/// The grades of an archived student for the administrator, from the first term to the last: every term with its courses (what the student started in, a shift to another
/// strand, and the term of graduation). The Excel file of one student has a sheet with the terms one after the other; the file of all the archived students is one flat
/// table (one line for each course of each term) that can be filtered.
/// </summary>
public static class StudentGradesExport
{
    /// <summary>What a term means in the story of the student: where they started, a shift to another strand, the end.</summary>
    public static string Milestone(IReadOnlyList<HistoryTerm> terms, int index, string studentStatus)
    {
        var term = terms[index];
        var labels = new List<string>();
        if (index == 0)
        {
            labels.Add("Started");
        }
        else if (!string.Equals(terms[index - 1].Strand, term.Strand, StringComparison.OrdinalIgnoreCase))
        {
            labels.Add($"Shifted from {terms[index - 1].Strand} to {term.Strand}");
        }

        if (index == terms.Count - 1 && studentStatus == Data.Entities.StudentStatus.Graduated)
        {
            labels.Add("Graduated");
        }

        return string.Join(" · ", labels);
    }

    private static string Grade(decimal? value) => value is { } v ? v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) : "";

    public static string FileName(ArchivedStudent student) => FileDownloads.CleanName($"Grades - {student.Name} ({student.Lrn}).xlsx");

    public static string FileNameAll() => FileDownloads.CleanName("Grades - archived students.xlsx");

    /// <summary>The file of one student: a sheet with all the terms.</summary>
    public static byte[] Build(ArchivedStudent student, IReadOnlyList<HistoryTerm> terms)
    {
        var book = new XlsxBook();
        var sheet = book.AddSheet("Grades");
        sheet.Set(1, 1, $"Grades · {student.Name} · LRN {student.Lrn}", XlsxStyle.Title);
        sheet.Set(2, 1, $"{Data.Entities.StudentStatus.Display(student.Status)}" + (student.ChangedAtUtc is { } at ? $" on {PhilippineTime.FormatDate(at)}" : "") +
            ". The grade is the final grade the teacher sent, or the percent of the graded work when there is none.", XlsxStyle.Note);

        var row = 4;
        if (terms.Count == 0)
        {
            sheet.Set(row, 1, "This student was never enrolled, so there are no grades.", XlsxStyle.Note);
        }

        for (var i = 0; i < terms.Count; i++)
        {
            var term = terms[i];
            var milestone = Milestone(terms, i, student.Status);
            sheet.Set(row, 1, $"{term.SchoolYear} · {term.Term} · Grade {term.GradeLevel} {term.Strand} {term.Section}" + (milestone.Length > 0 ? $" · {milestone}" : ""), XlsxStyle.Bold);
            row++;
            sheet.Row(row, ["Course", "Teacher", "Percent of graded work", "Final grade", "Grade"], XlsxStyle.Header);
            row++;
            foreach (var course in term.Courses)
            {
                sheet.Set(row, 1, course.Subject).Set(row, 2, course.Teacher);
                if (course.Percent is { } percent) { sheet.Set(row, 3, percent); }
                if (course.Final is { } final) { sheet.Set(row, 4, final); }
                if (course.Grade is { } grade) { sheet.Set(row, 5, grade); }
                row++;
            }

            sheet.Set(row, 1, "Average of the term", XlsxStyle.Bold);
            if (term.Average is { } average) { sheet.Set(row, 5, average, XlsxStyle.Bold); }
            row += 2;
        }

        sheet.Width(1, 46).Width(2, 28).Width(3, 24).Width(4, 14).Width(5, 12);
        return book.ToBytes();
    }

    /// <summary>The file of all the students: one table, one line for each course of each term.</summary>
    public static byte[] BuildAll(IReadOnlyList<(ArchivedStudent Student, IReadOnlyList<HistoryTerm> Terms)> students)
    {
        var book = new XlsxBook();
        var sheet = book.AddSheet("All grades");
        string[] header = ["Student", "LRN", "Status", "School year", "Term", "Grade level", "Strand", "Section", "Story", "Course", "Teacher", "Percent", "Final grade", "Grade"];
        sheet.Row(1, header, XlsxStyle.Header);
        var row = 2;
        foreach (var (student, terms) in students)
        {
            if (terms.Count == 0)
            {
                sheet.Row(row++, [student.Name, student.Lrn, Data.Entities.StudentStatus.Display(student.Status), "", "", "", "", "", "Never enrolled"]);
                continue;
            }

            for (var i = 0; i < terms.Count; i++)
            {
                var term = terms[i];
                var story = Milestone(terms, i, student.Status);
                foreach (var course in term.Courses.DefaultIfEmpty(new HistoryCourse("", "", null, null)))
                {
                    sheet.Row(row, [student.Name, student.Lrn, Data.Entities.StudentStatus.Display(student.Status), term.SchoolYear, term.Term, term.GradeLevel, term.Strand, term.Section, story, course.Subject, course.Teacher]);
                    if (course.Percent is { } percent) { sheet.Set(row, 12, percent); }
                    if (course.Final is { } final) { sheet.Set(row, 13, final); }
                    if (course.Grade is { } grade) { sheet.Set(row, 14, grade); }
                    row++;
                }
            }
        }

        sheet.FrozenRows = 1;
        sheet.AutoFilter = (1, header.Length);
        sheet.Width(1, 28).Width(2, 15).Width(3, 12).Width(4, 12).Width(5, 14).Width(6, 11).Width(7, 10).Width(8, 12).Width(9, 24).Width(10, 36).Width(11, 26).Width(12, 9).Width(13, 12).Width(14, 9);
        return book.ToBytes();
    }
}
