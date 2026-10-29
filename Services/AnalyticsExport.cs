using SEAITT_LMS.Components.Shared;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>
/// The Analytics pages as Excel files: the same numbers as the page, one sheet for each table, with the title and the day it was made on the first rows.
/// (The answers are numbers in the cells, not text, so the school can sort them and work with them in Excel.)
/// </summary>
public static class AnalyticsExport
{
    /// <summary>The student analytics of the administrator, the guidance counselor and a teacher.</summary>
    public static byte[] ForStudents(AnalyticsReport r, string madeFor, DateTime nowPhilippine)
    {
        var book = new XlsxBook();

        var summary = book.AddSheet("Summary");
        Title(summary, "Student analytics", $"{r.ScopeText} · for {madeFor} · {nowPhilippine:MMMM d, yyyy}");
        summary.Row(4, ["What", "Number"], XlsxStyle.Header);
        var line = 5;
        void Row(string what, object? value, XlsxStyle style = XlsxStyle.Integer) => summary.Set(line, 1, what).Set(line++, 2, value, style);
        Row("Students", r.Students);
        Row("Average grade (%)", r.AverageGrade, XlsxStyle.Decimal);
        Row("Attendance (present or late, %)", r.AttendanceRate, XlsxStyle.Decimal);
        Row($"Failing students (below {GradeRules.PassingPercent:0} in a course)", r.FailingStudents);
        Row("Students at risk", r.AtRiskStudents);
        Row("Open referrals", r.OpenReferrals);
        summary.Width(1, 52).Width(2, 14);

        var bands = book.AddSheet("Grades");
        Title(bands, r.BandsTitle, r.ScopeText);
        bands.Row(4, ["Group", "Outstanding (90+)", "Very satisfactory (85-89)", "Satisfactory (80-84)", "Fairly satisfactory (70-79)", "Below 70", "Total"], XlsxStyle.Header);
        line = 5;
        foreach (var group in r.Bands)
        {
            var b = group.Bands;
            bands.Set(line, 1, group.Label).Set(line, 2, b.Outstanding, XlsxStyle.Integer).Set(line, 3, b.VerySatisfactory, XlsxStyle.Integer).Set(line, 4, b.Satisfactory, XlsxStyle.Integer)
                .Set(line, 5, b.Fair, XlsxStyle.Integer).Set(line, 6, b.Failing, XlsxStyle.Integer)
                .Formula(line, 7, $"SUM(B{line}:F{line})", XlsxStyle.Integer, b.Total);
            line++;
        }

        bands.Width(1, 34);
        for (var c = 2; c <= 7; c++) { bands.Width(c, 18); }

        var attendance = book.AddSheet("Attendance");
        Title(attendance, "Attendance", r.ScopeText);
        attendance.Row(4, ["Group", "Present", "Late", "Absent", "Marks", "Attendance (%)"], XlsxStyle.Header);
        attendance.Set(5, 1, "All", XlsxStyle.Bold).Set(5, 2, r.Attendance.Present, XlsxStyle.Integer).Set(5, 3, r.Attendance.Late, XlsxStyle.Integer).Set(5, 4, r.Attendance.Absent, XlsxStyle.Integer)
            .Formula(5, 5, "SUM(B5:D5)", XlsxStyle.Integer, r.Attendance.Marks).Set(5, 6, r.Attendance.Rate, XlsxStyle.Decimal);
        line = 6;
        foreach (var g in r.Attendance.Groups)
        {
            attendance.Set(line, 1, g.Label).Set(line, 2, g.Present, XlsxStyle.Integer).Set(line, 3, g.Late, XlsxStyle.Integer).Set(line, 4, g.Absent, XlsxStyle.Integer)
                .Formula(line, 5, $"SUM(B{line}:D{line})", XlsxStyle.Integer, g.Present + g.Late + g.Absent).Set(line, 6, g.Rate, XlsxStyle.Decimal);
            line++;
        }

        attendance.Width(1, 34).Width(2, 12).Width(3, 12).Width(4, 12).Width(5, 12).Width(6, 16);

        var concerns = book.AddSheet("Concerns");
        Title(concerns, r.ConcernTitle, r.ScopeText);
        concerns.Row(4, ["Reason", "Students"], XlsxStyle.Header);
        line = 5;
        foreach (var c in r.Concerns)
        {
            concerns.Set(line, 1, ReferralReasons.Display(c.Reason)).Set(line++, 2, c.Count, XlsxStyle.Integer);
        }

        concerns.Set(line + 1, 1, "Referrals solved").Set(line + 1, 2, r.Resolved, XlsxStyle.Integer);
        concerns.Set(line + 2, 1, "... and the student improved").Set(line + 2, 2, r.Improved, XlsxStyle.Integer);
        concerns.Width(1, 34).Width(2, 14);

        if (r.Interventions.Count > 0)
        {
            var help = book.AddSheet("Help given");
            Title(help, "Help given in the referrals", r.ScopeText);
            help.Row(4, ["Help", "Referrals", "Student improved", "Improved (%)"], XlsxStyle.Header);
            line = 5;
            foreach (var i in r.Interventions)
            {
                help.Set(line, 1, i.Intervention).Set(line, 2, i.Total, XlsxStyle.Integer).Set(line, 3, i.Improved, XlsxStyle.Integer)
                    .Formula(line, 4, $"IF(B{line}=0,0,C{line}/B{line})", XlsxStyle.Percent, i.Total == 0 ? 0 : (double)i.Improved / i.Total);
                line++;
            }

            help.Width(1, 40).Width(2, 12).Width(3, 18).Width(4, 14);
        }

        var performance = book.AddSheet("Performance");
        Title(performance, r.PerformanceTitle, r.ScopeText);
        performance.Row(4, ["Group", "Students", "Average grade (%)", "Attendance (%)", "Failing"], XlsxStyle.Header);
        line = 5;
        foreach (var p in r.Performance)
        {
            performance.Set(line, 1, p.Label).Set(line, 2, p.Students, XlsxStyle.Integer).Set(line, 3, p.Average, XlsxStyle.Decimal).Set(line, 4, p.Attendance, XlsxStyle.Decimal).Set(line, 5, p.Failing, XlsxStyle.Integer);
            line++;
        }

        performance.Width(1, 40).Width(2, 12).Width(3, 18).Width(4, 16).Width(5, 12);
        return book.ToBytes();
    }

    /// <summary>The analytics of the academic provider: what was sent to the teachers and how it was used.</summary>
    public static byte[] ForProvider(ProviderAnalytics a, DateTime nowPhilippine)
    {
        var book = new XlsxBook();

        var summary = book.AddSheet("Summary");
        Title(summary, "Analytics of the academic provider", $"{nowPhilippine:MMMM d, yyyy}");
        summary.Row(4, ["What", "Number"], XlsxStyle.Header);
        summary.Set(5, 1, "Syllabi and modules sent").Set(5, 2, a.Sent, XlsxStyle.Integer);
        summary.Set(6, 1, "Teachers who opened it (%)").Set(6, 2, a.TeacherOpenedPercent, XlsxStyle.Integer);
        summary.Set(7, 1, "Posted for the students (%)").Set(7, 2, a.PostedPercent, XlsxStyle.Integer);
        summary.Set(8, 1, "Average hours until a teacher opened it").Set(8, 2, a.AverageHoursToOpen, XlsxStyle.Decimal);
        summary.Set(9, 1, "Average hours until it was posted").Set(9, 2, a.AverageHoursToPost, XlsxStyle.Decimal);
        summary.Set(10, 1, "Modules opened by the students (%)").Set(10, 2, a.StudentModulePercent, XlsxStyle.Integer);
        summary.Set(11, 1, "Assignments opened by the students (%)").Set(11, 2, a.StudentItemPercent, XlsxStyle.Integer);
        summary.Width(1, 48).Width(2, 14);

        var courses = book.AddSheet("By course");
        Title(courses, "By course", "What was sent for each course and how it was used");
        courses.Row(4, ["Course", "Sent", "Teachers", "Teachers who opened it", "Posted", "Modules opened by students", "Modules expected", "Assignments opened", "Assignments expected"], XlsxStyle.Header);
        var line = 5;
        foreach (var c in a.ByCourse)
        {
            courses.Set(line, 1, c.Name).Set(line, 2, c.Sent, XlsxStyle.Integer).Set(line, 3, c.Teachers, XlsxStyle.Integer).Set(line, 4, c.TeacherOpened, XlsxStyle.Integer).Set(line, 5, c.Posted, XlsxStyle.Integer)
                .Set(line, 6, c.ModuleOpens, XlsxStyle.Integer).Set(line, 7, c.ModuleExpected, XlsxStyle.Integer).Set(line, 8, c.ItemOpens, XlsxStyle.Integer).Set(line, 9, c.ItemExpected, XlsxStyle.Integer);
            line++;
        }

        courses.Width(1, 40);
        for (var col = 2; col <= 9; col++) { courses.Width(col, 16); }

        var kinds = book.AddSheet("By kind");
        Title(kinds, "By kind", "Syllabi, modules, assignments, quizzes and task performances");
        kinds.Row(4, ["Kind", "Sent", "Opened by students", "Expected", "Opened (%)"], XlsxStyle.Header);
        line = 5;
        foreach (var k in a.ByKind)
        {
            kinds.Set(line, 1, k.Kind).Set(line, 2, k.Sent, XlsxStyle.Integer).Set(line, 3, k.Opens, XlsxStyle.Integer).Set(line, 4, k.Expected, XlsxStyle.Integer)
                .Formula(line, 5, $"IF(D{line}=0,0,C{line}/D{line})", XlsxStyle.Percent, k.Expected == 0 ? 0 : (double)k.Opens / k.Expected);
            line++;
        }

        kinds.Width(1, 32).Width(2, 12).Width(3, 20).Width(4, 12).Width(5, 14);

        var sections = book.AddSheet("By section");
        Title(sections, "By section", "How much of what was posted the students opened");
        sections.Row(4, ["Section", "Students", "Opened", "Expected", "Opened (%)"], XlsxStyle.Header);
        line = 5;
        foreach (var s in a.BySection)
        {
            sections.Set(line, 1, s.SectionLabel).Set(line, 2, s.Students, XlsxStyle.Integer).Set(line, 3, s.Opens, XlsxStyle.Integer).Set(line, 4, s.Expected, XlsxStyle.Integer)
                .Formula(line, 5, $"IF(D{line}=0,0,C{line}/D{line})", XlsxStyle.Percent, s.Expected == 0 ? 0 : (double)s.Opens / s.Expected);
            line++;
        }

        sections.Width(1, 30).Width(2, 12).Width(3, 12).Width(4, 12).Width(5, 14);

        var weeks = book.AddSheet("By week");
        Title(weeks, "By week", "What was sent each week");
        weeks.Row(4, ["Week starting", "Sent", "Opened by a teacher", "Posted"], XlsxStyle.Header);
        line = 5;
        foreach (var w in a.Weekly)
        {
            weeks.Set(line, 1, w.WeekStart, XlsxStyle.Date).Set(line, 2, w.Sent, XlsxStyle.Integer).Set(line, 3, w.OpenedByTeacher, XlsxStyle.Integer).Set(line, 4, w.Posted, XlsxStyle.Integer);
            line++;
        }

        weeks.Width(1, 16).Width(2, 10).Width(3, 20).Width(4, 10);
        return book.ToBytes();
    }

    /// <summary>The analytics of the teacher evaluation chosen by the academic coordinator.</summary>
    public static byte[] ForEvaluation(EvaluationAnalytics a, DateTime nowPhilippine)
    {
        var book = new XlsxBook();

        var summary = book.AddSheet("Summary");
        Title(summary, "Teacher evaluation analytics", $"{a.Title} · {nowPhilippine:MMMM d, yyyy}");
        summary.Row(4, ["What", "Number"], XlsxStyle.Header);
        summary.Set(5, 1, "Answers expected").Set(5, 2, a.Expected, XlsxStyle.Integer);
        summary.Set(6, 1, "Answers received").Set(6, 2, a.Responses, XlsxStyle.Integer);
        summary.Set(7, 1, "Answered (%)").Formula(7, 2, "IF(B5=0,0,B6/B5)", XlsxStyle.Percent, a.Expected == 0 ? 0 : (double)a.Responses / a.Expected);
        summary.Set(8, 1, "Average rating (of 5)").Set(8, 2, a.Average, XlsxStyle.Decimal);
        summary.Width(1, 34).Width(2, 14);

        var spread = book.AddSheet("Ratings");
        Title(spread, "How the ratings are spread", "The number of ratings of 5, 4, 3, 2 and 1");
        spread.Row(4, ["Rating", "Number"], XlsxStyle.Header);
        for (var rating = a.Distribution.Count; rating >= 1; rating--)
        {
            var row = 5 + (a.Distribution.Count - rating);
            spread.Set(row, 1, rating).Set(row, 2, a.Distribution[rating - 1], XlsxStyle.Integer);
        }

        spread.Width(1, 12).Width(2, 12);

        var teachers = book.AddSheet("Teachers");
        Title(teachers, "Teachers", "Ranked by the average rating");
        teachers.Row(4, ["Rank", "Teacher", "Courses", "Answers expected", "Answers received", "Answered (%)", "Average rating (of 5)"], XlsxStyle.Header);
        var line = 5;
        foreach (var t in a.Teachers)
        {
            teachers.Set(line, 1, t.Rank, XlsxStyle.Integer).Set(line, 2, t.TeacherName).Set(line, 3, t.Courses, XlsxStyle.Integer).Set(line, 4, t.Expected, XlsxStyle.Integer)
                .Set(line, 5, t.Responses, XlsxStyle.Integer).Formula(line, 6, $"IF(D{line}=0,0,E{line}/D{line})", XlsxStyle.Percent, t.Expected == 0 ? 0 : (double)t.Responses / t.Expected).Set(line, 7, t.Average, XlsxStyle.Decimal);
            line++;
        }

        teachers.Width(1, 8).Width(2, 32).Width(3, 10).Width(4, 18).Width(5, 18).Width(6, 14).Width(7, 22);

        var questions = book.AddSheet("Questions");
        Title(questions, "Questions", "The average rating of each question");
        questions.Row(4, ["Question", "Category", "Answers", "Average rating (of 5)"], XlsxStyle.Header);
        line = 5;
        foreach (var q in a.Questions)
        {
            questions.Set(line, 1, q.Text, XlsxStyle.Wrap).Set(line, 2, q.Category).Set(line, 3, q.Answers, XlsxStyle.Integer).Set(line, 4, q.Average, XlsxStyle.Decimal);
            line++;
        }

        questions.Width(1, 70).Width(2, 24).Width(3, 10).Width(4, 22);

        var categories = book.AddSheet("Categories");
        Title(categories, "Categories", "The average rating of each group of questions");
        categories.Row(4, ["Category", "Answers", "Average rating (of 5)"], XlsxStyle.Header);
        line = 5;
        foreach (var c in a.Categories)
        {
            categories.Set(line, 1, c.Text).Set(line, 2, c.Answers, XlsxStyle.Integer).Set(line, 3, c.Average, XlsxStyle.Decimal);
            line++;
        }

        categories.Width(1, 40).Width(2, 10).Width(3, 22);

        if (a.OtherQuestions is { Count: > 0 } others)
        {
            var other = book.AddSheet("Other questions");
            Title(other, "Yes or No, rankings and choices", "Each option with its count and value (the share in percent, or the average place of a ranking)");
            other.Row(4, ["Question", "Kind", "Option", "Count", "Value"], XlsxStyle.Header);
            line = 5;
            foreach (var q in others)
            {
                foreach (var o in q.Options)
                {
                    other.Set(line, 1, q.Text, XlsxStyle.Wrap).Set(line, 2, EvaluationQuestionKinds.Display(q.Kind)).Set(line, 3, o.Label).Set(line, 4, o.Count, XlsxStyle.Integer).Set(line, 5, o.Value, XlsxStyle.Decimal);
                    line++;
                }
            }

            other.Width(1, 60).Width(2, 18).Width(3, 30).Width(4, 10).Width(5, 12);
        }

        return book.ToBytes();
    }

    private static void Title(XlsxSheet sheet, string title, string note)
    {
        sheet.Set(1, 1, title, XlsxStyle.Title);
        sheet.Set(2, 1, note, XlsxStyle.Note);
    }

    /// <summary>A file name made of words and the day: "Student analytics 2026-10-29.xlsx".</summary>
    public static string FileName(string what, DateTime nowPhilippine) => $"{what} {nowPhilippine:yyyy-MM-dd}.xlsx";
}
