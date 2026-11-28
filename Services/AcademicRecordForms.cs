using SEAITT_LMS.Components.Shared;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>
/// Round 46: the academic records a former student (or the school) asks for, made from the archive: <b>Form 137</b>, the permanent record (every school year and term with the courses, the final grades, the
/// descriptors and the remarks), and <b>Form 138</b>, the report card of the last school year (the courses with the grade of each term and the final grade). They are Excel files, made to be printed.
/// Only an administrator makes them (from Archive Accounts): an archived record is never deleted and nobody else can open it.
/// </summary>
public static class AcademicRecordForms
{
    public static string FileName137(ArchivedStudent student) => FileDownloads.CleanName($"Form 137 - {student.Name} ({student.Lrn}).xlsx");

    public static string FileName138(ArchivedStudent student) => FileDownloads.CleanName($"Form 138 - {student.Name} ({student.Lrn}).xlsx");

    private static string G(decimal? grade) => grade is { } g ? Math.Round(g, MidpointRounding.AwayFromZero).ToString("0") : "";

    private static void LearnerBlock(XlsxSheet sheet, ArchivedStudent student, string schoolName, string schoolAddress, string title, ref int row)
    {
        sheet.Set(1, 1, title, XlsxStyle.Title);
        sheet.Set(2, 1, schoolName + (string.IsNullOrWhiteSpace(schoolAddress) ? "" : " · " + schoolAddress), XlsxStyle.Note);
        row = 4;
        sheet.Set(row, 1, "Name of the learner", XlsxStyle.Bold).Set(row, 2, student.Name);
        row++;
        sheet.Set(row, 1, "LRN", XlsxStyle.Bold).Set(row, 2, student.Lrn, XlsxStyle.Text);
        row++;
        sheet.Set(row, 1, "Sex", XlsxStyle.Bold).Set(row, 2, student.Sex.ForFile());
        row++;
        sheet.Set(row, 1, "Record status", XlsxStyle.Bold).Set(row, 2, StudentStatus.Display(student.Status) + (student.ChangedAtUtc is { } at ? " on " + PhilippineTime.FormatDate(at) : ""));
        row += 2;
    }

    /// <summary>Form 137: the permanent record, every term from the first to the last.</summary>
    public static byte[] Build137(ArchivedStudent student, IReadOnlyList<HistoryTerm> terms, string schoolName, string schoolAddress)
    {
        var book = new XlsxBook();
        var sheet = book.AddSheet("Form 137");
        var row = 0;
        LearnerBlock(sheet, student, schoolName, schoolAddress, "FORM 137 · PERMANENT RECORD OF THE LEARNER", ref row);
        var completed = 0;
        var passed = 0;
        var failed = 0;
        var gradeSum = 0m;
        var graded = 0;
        foreach (var term in terms)
        {
            sheet.Set(row, 1, $"{term.SchoolYear} · {term.Term} · Grade {term.GradeLevel} {term.Strand} {term.Section}", XlsxStyle.Bold);
            row++;
            sheet.Row(row, ["Course", "Teacher", "Final grade", "Descriptor", "Remarks"], XlsxStyle.Header);
            row++;
            foreach (var course in term.Courses)
            {
                sheet.Set(row, 1, course.Subject).Set(row, 2, course.Teacher);
                if (course.Grade is { } grade)
                {
                    sheet.Set(row, 3, Math.Round(grade, MidpointRounding.AwayFromZero), XlsxStyle.Integer).Set(row, 4, GradingSystem.Descriptor(grade)).Set(row, 5, GradingSystem.Remark(grade).ToUpperInvariant());
                    if (term.Status != EnrollmentStatus.Dropped.ToString())
                    {
                        completed++;
                        gradeSum += Math.Round(grade, MidpointRounding.AwayFromZero);
                        graded++;
                        if (GradingSystem.Remark(grade) == "Passed") { passed++; } else { failed++; }
                    }
                }

                row++;
            }

            if (term.Courses.Count == 0)
            {
                sheet.Set(row, 1, "No courses in this term.", XlsxStyle.Note);
                row++;
            }

            if (term.Status == EnrollmentStatus.Dropped.ToString())
            {
                sheet.Set(row, 1, "The learner dropped out in this term.", XlsxStyle.Note);
                row++;
            }

            row++;
        }

        if (terms.Count == 0)
        {
            sheet.Set(row, 1, "This learner was never enrolled, so there are no grades.", XlsxStyle.Note);
            row += 2;
        }

        sheet.Set(row, 1, "Courses completed", XlsxStyle.Bold).Set(row, 3, completed, XlsxStyle.Integer);
        row++;
        sheet.Set(row, 1, "Passed (75 and over)", XlsxStyle.Bold).Set(row, 3, passed, XlsxStyle.Integer);
        row++;
        sheet.Set(row, 1, "Failed", XlsxStyle.Bold).Set(row, 3, failed, XlsxStyle.Integer);
        row++;
        sheet.Set(row, 1, "General average", XlsxStyle.Bold).Set(row, 3, graded == 0 ? null : Math.Round(gradeSum / graded, 2), XlsxStyle.Decimal);
        row += 3;
        sheet.Set(row, 1, "Prepared by the Registrar: ______________________").Set(row, 4, "Date: ______________");
        row += 2;
        sheet.Set(row, 1, "Certified true copy of the records kept in the archive of the school.", XlsxStyle.Note);
        sheet.Width(1, 36).Width(2, 26).Width(3, 13).Width(4, 16).Width(5, 12);
        return book.ToBytes();
    }

    /// <summary>Form 138: the report card of the last school year: each course with the grade of every term, the final grade and the remarks.</summary>
    public static byte[] Build138(ArchivedStudent student, IReadOnlyList<HistoryTerm> terms, string schoolName, string schoolAddress)
    {
        var book = new XlsxBook();
        var sheet = book.AddSheet("Form 138");
        var row = 0;
        LearnerBlock(sheet, student, schoolName, schoolAddress, "FORM 138 · REPORT CARD", ref row);
        var year = terms.LastOrDefault()?.SchoolYear;
        var ofYear = terms.Where(t => t.SchoolYear == year && t.Status != EnrollmentStatus.Dropped.ToString()).ToList();
        if (year is null || ofYear.Count == 0)
        {
            sheet.Set(row, 1, "There is no school year with grades to put in a report card.", XlsxStyle.Note);
            sheet.Width(1, 40);
            return book.ToBytes();
        }

        var last = ofYear[^1];
        sheet.Set(row, 1, $"School year {year} · Grade {last.GradeLevel} {last.Strand} {last.Section}", XlsxStyle.Bold);
        row += 2;
        var header = new List<object?> { "Course" };
        header.AddRange(ofYear.Select(t => t.Term));
        header.AddRange(["Final grade", "Descriptor", "Remarks"]);
        sheet.Row(row, header, XlsxStyle.Header);
        row++;
        var courseNames = ofYear.SelectMany(t => t.Courses.Select(c => c.Subject)).Distinct().OrderBy(n => n).ToList();
        var finals = new List<decimal>();
        foreach (var name in courseNames)
        {
            sheet.Set(row, 1, name);
            var grades = new List<decimal>();
            for (var i = 0; i < ofYear.Count; i++)
            {
                var grade = ofYear[i].Courses.FirstOrDefault(c => c.Subject == name)?.Grade;
                if (grade is { } g)
                {
                    sheet.Set(row, 2 + i, Math.Round(g, MidpointRounding.AwayFromZero), XlsxStyle.Integer);
                    grades.Add(g);
                }
            }

            if (grades.Count > 0)
            {
                var final = Math.Round(grades.Average(), MidpointRounding.AwayFromZero);
                finals.Add(final);
                sheet.Set(row, 2 + ofYear.Count, final, XlsxStyle.Integer).Set(row, 3 + ofYear.Count, GradingSystem.Descriptor(final)).Set(row, 4 + ofYear.Count, GradingSystem.Remark(final).ToUpperInvariant());
            }

            row++;
        }

        row++;
        sheet.Set(row, 1, "General average", XlsxStyle.Bold).Set(row, 2 + ofYear.Count, finals.Count == 0 ? null : Math.Round(finals.Average(), 2), XlsxStyle.Decimal);
        if (finals.Count > 0)
        {
            sheet.Set(row, 3 + ofYear.Count, GradingSystem.Descriptor(finals.Average())).Set(row, 4 + ofYear.Count, GradingSystem.Remark(finals.Average()).ToUpperInvariant());
        }

        row += 2;
        sheet.Set(row, 1, "Grading system: 90-100 Advancing · 80-89 Benchmarking · 75-79 Connecting · 65-74 Developing · 0-64 Emerging. 75 and over is PASSED.", XlsxStyle.Note);
        row += 3;
        sheet.Set(row, 1, "Class adviser: ______________________").Set(row, 2 + Math.Max(0, ofYear.Count - 1), "Registrar: ______________________");
        sheet.Width(1, 36);
        for (var c = 2; c <= 4 + ofYear.Count; c++)
        {
            sheet.Width(c, 14);
        }

        return book.ToBytes();
    }
}
