using System.Globalization;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

// ---- what the academic setup holds, as the workbook lists it (names and codes, not numbers: a person can read and type them)
public sealed record SetupCurriculumRow(string Strand, int Grade, int Term, string Course);

public sealed record SetupSectionRow(string Year, string Strand, int Grade, string Name, string? Adviser);

public sealed record SetupClassRow(string Year, string Strand, int Grade, string Section, string Course, string Part, string Days, TimeOnly Start, TimeOnly End);

/// <summary>Everything of the academic setup that the workbook holds (the export), or nothing (the template).</summary>
public sealed record SetupSnapshot(
    IReadOnlyList<SchoolYearRow> Years, IReadOnlyList<TermRow> Terms, IReadOnlyList<TrackRow> Tracks, IReadOnlyList<StrandRow> Strands, IReadOnlyList<SubjectRow> Courses,
    IReadOnlyList<SetupCurriculumRow> Curriculum, IReadOnlyList<SetupSectionRow> Sections, IReadOnlyList<DayPart> DayParts, IReadOnlyList<SetupClassRow> Classes)
{
    public static SetupSnapshot Empty { get; } = new([], [], [], [], [], [], [], [], []);

    /// <summary>The rows of all the sheets (the number on the button that exports them).</summary>
    public int Total => Years.Count + Terms.Count + Tracks.Count + Strands.Count + Courses.Count + Curriculum.Count + Sections.Count + DayParts.Count + Classes.Count;
}

/// <summary>Where a row of the workbook is, what it is called and what is wrong with it (nothing: it is ready).</summary>
public sealed record SetupLine(string Sheet, int Row, string Title, IReadOnlyList<string> Errors)
{
    public string Place => $"{Sheet}, row {Row}";

    public bool IsReady => Errors.Count == 0;
}

public sealed record SetupYear(SetupLine Line, string Name, DateOnly Start, DateOnly End);

public sealed record SetupTerm(SetupLine Line, string Year, int Number, string Name, DateOnly Start, DateOnly End, bool Current);

public sealed record SetupTrack(SetupLine Line, string Code, string Name, bool Active);

public sealed record SetupStrand(SetupLine Line, string Code, string Name, string Track, bool Active);

public sealed record SetupCourse(SetupLine Line, string Code, string Name, decimal Units, string? Description, bool Active);

public sealed record SetupCurriculum(SetupLine Line, string Strand, int Grade, int Term, string Course);

public sealed record SetupSection(SetupLine Line, string Year, string Strand, int Grade, string Name, string? Adviser);

public sealed record SetupDayPart(SetupLine Line, string Part, TimeOnly Start, TimeOnly End);

public sealed record SetupClass(SetupLine Line, string Year, string Strand, int Grade, string Section, string Course, string Part, string Days, TimeOnly Start, TimeOnly End);

/// <summary>A workbook as it was read: every row of every sheet, each with its problems, in the order the sheets have to be saved.</summary>
public sealed record SetupBook(
    IReadOnlyList<SetupDayPart> DayParts, IReadOnlyList<SetupYear> Years, IReadOnlyList<SetupTerm> Terms, IReadOnlyList<SetupTrack> Tracks, IReadOnlyList<SetupStrand> Strands,
    IReadOnlyList<SetupCourse> Courses, IReadOnlyList<SetupCurriculum> Curriculum, IReadOnlyList<SetupSection> Sections, IReadOnlyList<SetupClass> Classes, string? FileError)
{
    /// <summary>Every row, sheet by sheet, in the order of the workbook.</summary>
    public IReadOnlyList<SetupLine> Lines => [.. Years.Select(r => r.Line), .. Terms.Select(r => r.Line), .. Tracks.Select(r => r.Line), .. Strands.Select(r => r.Line), .. Courses.Select(r => r.Line),
        .. Curriculum.Select(r => r.Line), .. Sections.Select(r => r.Line), .. DayParts.Select(r => r.Line), .. Classes.Select(r => r.Line)];
}

/// <summary>
/// The one Excel workbook for the academic setup (the academic coordinator's page): the sheets School Years, Terms, Tracks, Strands, Courses, Curriculum (the courses of each strand by grade level and
/// term), Sections, Day Parts (the hours of AM, MID and PM) and Class Schedule. The template is empty and says how to fill each column; the export is the same workbook with everything the setup has,
/// so it can be changed and imported again. The columns that point to another sheet (the school year of a term, the track of a strand ...) have a drop-down list made from that sheet.
/// </summary>
public static class SetupSheets
{
    public const int MaxRows = 500;

    public const string YearsSheet = "School Years";
    public const string TermsSheet = "Terms";
    public const string TracksSheet = "Tracks";
    public const string StrandsSheet = "Strands";
    public const string CoursesSheet = "Courses";
    public const string CurriculumSheet = "Curriculum";
    public const string SectionsSheet = "Sections";
    public const string DayPartsSheet = "Day Parts";
    public const string ClassesSheet = "Class Schedule";

    /// <summary>The rows the drop-down lists that point to another sheet reach (the first column of it).</summary>
    private const int ListRows = 400;

    private const int ExtraRows = 25;

    private static readonly string[] YearHeaders = ["Name", "First day", "Last day"];
    private static readonly string[] TermHeaders = ["School year", "Term", "Name", "First day", "Last day", "Current term"];
    private static readonly string[] TrackHeaders = ["Code", "Name", "Active"];
    private static readonly string[] StrandHeaders = ["Code", "Name", "Track", "Active"];
    private static readonly string[] CourseHeaders = ["Code", "Name", "Units", "Description", "Active"];
    private static readonly string[] CurriculumHeaders = ["Strand", "Grade", "Term", "Course"];
    private static readonly string[] SectionHeaders = ["School year", "Strand", "Grade", "Section", "Adviser (employee number)"];
    private static readonly string[] DayPartHeaders = ["Part", "Starts", "Ends"];
    private static readonly string[] ClassHeaders = ["School year", "Strand", "Grade", "Section", "Course", "Part", "Days", "Starts", "Ends"];

    public static string FileName(string what) => FileDownloads.CleanName($"{what} - Academic setup.xlsx");

    public static byte[] Template() => Build(SetupSnapshot.Empty);

    public static byte[] Export(SetupSnapshot snapshot) => Build(snapshot);

    private static string Day(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string YesNo(bool value) => value ? "Yes" : "No";

    private static string Refs(string sheet) => $"'{sheet}'!$A$2:$A${ListRows + 1}";

    private static byte[] Build(SetupSnapshot data)
    {
        var book = new XlsxBook();

        // A sheet with its title row, the rows of the data and empty yellow rows after them. Each row is an array of the cells (text).
        XlsxSheet Sheet(string name, string[] headers, int[] widths, IReadOnlyList<string[]> rows, int[]? wrapColumns = null)
        {
            var sheet = book.AddSheet(name);
            sheet.Row(1, headers, XlsxStyle.Header);
            for (var c = 0; c < widths.Length; c++)
            {
                sheet.Width(c + 1, widths[c]);
            }

            var row = 2;
            foreach (var cells in rows)
            {
                for (var c = 0; c < headers.Length; c++)
                {
                    sheet.Set(row, c + 1, c < cells.Length ? cells[c] : "", wrapColumns?.Contains(c) == true ? XlsxStyle.Wrap : XlsxStyle.InputText);
                }

                row++;
            }

            var last = Math.Max(row + ExtraRows, 31);
            for (; row <= last; row++)
            {
                for (var c = 0; c < headers.Length; c++)
                {
                    sheet.Set(row, c + 1, null, XlsxStyle.InputText);
                }
            }

            sheet.FrozenRows = 1;
            return sheet;
        }

        int Last(IReadOnlyList<object> rows) => Math.Max(rows.Count + 2 + ExtraRows, 31);

        var years = Sheet(YearsSheet, YearHeaders, [16, 14, 14], [.. data.Years.OrderBy(y => y.StartDate).Select(y => new[] { y.Name, Day(y.StartDate), Day(y.EndDate) })]);
        var yearsLast = Last([.. data.Years]);
        years.Tip($"A2:A{yearsLast}", "Type the school year, like 2027-2028.").Tip($"B2:C{yearsLast}", "Type the day like 2027-06-07 (year-month-day).");

        var terms = Sheet(TermsSheet, TermHeaders, [16, 8, 18, 14, 14, 14], [.. data.Terms.OrderBy(t => t.StartDate).Select(t => new[] { t.SchoolYear, t.TermNumber.ToString(CultureInfo.InvariantCulture), t.Name, Day(t.StartDate), Day(t.EndDate), YesNo(t.IsCurrent) })]);
        var termsLast = Last([.. data.Terms]);
        terms.ListFromOrOther($"A2:A{termsLast}", Refs(YearsSheet), "Choose the school year from the list (it shows the school years of the sheet School Years).")
            .ListChoice($"B2:B{termsLast}", ["1", "2", "3", "4"], "Choose the term number: 1 for the 1st term, 2 for the 2nd term.")
            .Tip($"C2:C{termsLast}", "Type the name of the term, like 1st Term.").Tip($"D2:E{termsLast}", "Type the day like 2027-06-07 (year-month-day). A term must be inside its school year.")
            .ListChoice($"F2:F{termsLast}", ["Yes", "No"], "Yes for the term that is running now. Only one term is the current one.");

        var tracks = Sheet(TracksSheet, TrackHeaders, [14, 46, 10], [.. data.Tracks.Select(t => new[] { t.Code, t.Name, YesNo(t.IsActive) })]);
        var tracksLast = Last([.. data.Tracks]);
        tracks.Tip($"A2:A{tracksLast}", "Type the code of the track, like ACAD (up to 20 characters).").Tip($"B2:B{tracksLast}", "Type the name of the track, like Academic.")
            .ListChoice($"C2:C{tracksLast}", ["Yes", "No"], "Yes: the track can be used. No: it is switched off.");

        var strands = Sheet(StrandsSheet, StrandHeaders, [16, 52, 14, 10], [.. data.Strands.Select(s => new[] { s.Code, s.Name, s.Track, YesNo(s.IsActive) })]);
        var strandsLast = Last([.. data.Strands]);
        strands.Tip($"A2:A{strandsLast}", "Type the code of the strand, like BUSSENT (up to 20 characters).").Tip($"B2:B{strandsLast}", "Type the name of the strand.")
            .ListFromOrOther($"C2:C{strandsLast}", Refs(TracksSheet), "Choose the track from the list (it shows the codes of the sheet Tracks).")
            .ListChoice($"D2:D{strandsLast}", ["Yes", "No"], "Yes: the strand can be used. No: it is switched off.");

        var courses = Sheet(CoursesSheet, CourseHeaders, [16, 46, 8, 40, 10], [.. data.Courses.OrderBy(c => c.Name).Select(c => new[] { c.Code, c.Name, c.Units.ToString("0.#", CultureInfo.InvariantCulture), c.Description ?? "", YesNo(c.IsActive) })], [3]);
        var coursesLast = Last([.. data.Courses]);
        courses.Tip($"A2:A{coursesLast}", "Type the code of the course (up to 30 characters).").Tip($"B2:B{coursesLast}", "Type the name of the course.")
            .Number($"C2:C{coursesLast}", 0.5m, 12, "Type the units, from 0.5 to 12, like 3 or 1.5.").Tip($"D2:D{coursesLast}", "Optional: a short description (up to 500 characters).")
            .ListChoice($"E2:E{coursesLast}", ["Yes", "No"], "Yes: the course can be used. No: it is switched off.");

        var curriculum = Sheet(CurriculumSheet, CurriculumHeaders, [16, 8, 8, 16], [.. data.Curriculum.Select(c => new[] { c.Strand, c.Grade.ToString(CultureInfo.InvariantCulture), c.Term.ToString(CultureInfo.InvariantCulture), c.Course })]);
        var curriculumLast = Last([.. data.Curriculum]);
        curriculum.ListFromOrOther($"A2:A{curriculumLast}", Refs(StrandsSheet), "Choose the strand from the list (it shows the codes of the sheet Strands).")
            .ListChoice($"B2:B{curriculumLast}", ["11", "12"], "Choose the grade level: 11 or 12.").ListChoice($"C2:C{curriculumLast}", ["1", "2", "3", "4"], "Choose the term number: 1 or 2.")
            .ListFromOrOther($"D2:D{curriculumLast}", Refs(CoursesSheet), "Choose the course from the list (it shows the codes of the sheet Courses). One row for each course of the strand in a term.");

        var sections = Sheet(SectionsSheet, SectionHeaders, [16, 16, 8, 12, 26], [.. data.Sections.Select(s => new[] { s.Year, s.Strand, s.Grade.ToString(CultureInfo.InvariantCulture), s.Name, s.Adviser ?? "" })]);
        var sectionsLast = Last([.. data.Sections]);
        sections.ListFromOrOther($"A2:A{sectionsLast}", Refs(YearsSheet), "Choose the school year from the list.").ListFromOrOther($"B2:B{sectionsLast}", Refs(StrandsSheet), "Choose the strand from the list.")
            .ListChoice($"C2:C{sectionsLast}", ["11", "12"], "Choose the grade level: 11 or 12.").Tip($"D2:D{sectionsLast}", "Type the name of the section, like A (letters, numbers, spaces and dashes).")
            .Tip($"E2:E{sectionsLast}", "Optional: the employee number of the teacher who is the adviser. Leave it empty for no adviser.");

        var parts = Sheet(DayPartsSheet, DayPartHeaders, [10, 12, 12], [.. data.DayParts.Select(p => new[] { p.Code, ClassTime.Box(p.Start), ClassTime.Box(p.End) })]);
        var partsLast = Last([.. data.DayParts]);
        parts.ListChoice($"A2:A{partsLast}", ["AM", "MID", "PM"], "Choose the part of the day: AM, MID or PM.").Tip($"B2:C{partsLast}", "Type the hour like 07:30 or 13:00 (24 hours). A part is at least one hour long, between 5:00 AM and 10:00 PM.");

        var classes = Sheet(ClassesSheet, ClassHeaders, [16, 16, 8, 12, 16, 8, 8, 10, 10], [.. data.Classes.Select(c => new[] { c.Year, c.Strand, c.Grade.ToString(CultureInfo.InvariantCulture), c.Section, c.Course, c.Part, c.Days, ClassTime.Box(c.Start), ClassTime.Box(c.End) })]);
        var classesLast = Last([.. data.Classes]);
        classes.ListFromOrOther($"A2:A{classesLast}", Refs(YearsSheet), "Choose the school year from the list.").ListFromOrOther($"B2:B{classesLast}", Refs(StrandsSheet), "Choose the strand from the list.")
            .ListChoice($"C2:C{classesLast}", ["11", "12"], "Choose the grade level: 11 or 12.").Tip($"D2:D{classesLast}", "Type the name of the section, like A. It must be a section of the sheet Sections (or one that already exists).")
            .ListFromOrOther($"E2:E{classesLast}", Refs(CoursesSheet), "Choose the course from the list. It must be one of the courses of the strand in that grade level.")
            .ListChoice($"F2:F{classesLast}", ["AM", "MID", "PM"], "Choose the part of the day: AM, MID or PM. The class time must be inside its hours.")
            .ListChoice($"G2:G{classesLast}", ["MWF", "TTH"], "Choose the days: MWF (Monday, Wednesday, Friday) or TTH (Tuesday, Thursday).")
            .Tip($"H2:I{classesLast}", "Type the hour like 08:00 or 13:30 (24 hours). A class lasts from 30 minutes to 4 hours.");

        const string Needed = "Needed", Optional = "Optional";
        TemplateGuide.AddWithoutExample(book, "How to fill the academic setup workbook",
        [
            new(YearsSheet, "Name", Needed, "The school year, like 2027-2028 (up to 20 characters). A school year that already exists is changed, not added twice.", TemplateGuide.TypeIt),
            new(YearsSheet, "First day, Last day", Needed, "The first and the last day of the school year, like 2027-06-07 and 2028-03-31.", "Type them like 2027-06-07 (year-month-day)."),
            new(TermsSheet, "School year", Needed, "The school year of the term, written as in the sheet School Years.", "Choose it from the list."),
            new(TermsSheet, "Term", Needed, "1 for the 1st term, 2 for the 2nd term (a number from 1 to 9).", "Choose it from the list."),
            new(TermsSheet, "Name", Needed, "The name of the term, like 1st Term (up to 50 characters).", TemplateGuide.TypeIt),
            new(TermsSheet, "First day, Last day", Needed, "The days the term starts and ends. They must be inside the school year.", "Type them like 2027-06-07."),
            new(TermsSheet, "Current term", Optional, "Yes for the term that is running now (only one term is the current one). Empty: No.", "Choose it from the list."),
            new(TracksSheet, "Code, Name", Needed, "The code of the track, like ACAD (up to 20 characters), and its name.", TemplateGuide.TypeIt),
            new(TracksSheet, "Active", Optional, "Yes or No. Empty: Yes.", "Choose it from the list."),
            new(StrandsSheet, "Code, Name", Needed, "The code of the strand, like BUSSENT (up to 20 characters), and its name.", TemplateGuide.TypeIt),
            new(StrandsSheet, "Track", Needed, "The code of the track the strand belongs to.", "Choose it from the list (the codes of the sheet Tracks)."),
            new(StrandsSheet, "Active", Optional, "Yes or No. Empty: Yes.", "Choose it from the list."),
            new(CoursesSheet, "Code, Name", Needed, "The code of the course (up to 30 characters) and its name.", TemplateGuide.TypeIt),
            new(CoursesSheet, "Units", Optional, "From 0.5 to 12, one decimal at most. Empty: 3.", TemplateGuide.TypeNumber),
            new(CoursesSheet, "Description", Optional, "A short description (up to 500 characters).", TemplateGuide.TypeIt),
            new(CoursesSheet, "Active", Optional, "Yes or No. Empty: Yes. A course that is switched off can't be put in a strand.", "Choose it from the list."),
            new(CurriculumSheet, "Strand, Course", Needed, "One row for each course a strand takes in a term of a grade level: the code of the strand and the code of the course.", "Choose them from the lists."),
            new(CurriculumSheet, "Grade, Term", Needed, "The grade level (11 or 12) and the term number (1 or 2).", "Choose them from the lists."),
            new(SectionsSheet, "School year, Strand", Needed, "The school year and the strand of the section, written as in the sheets School Years and Strands.", "Choose them from the lists."),
            new(SectionsSheet, "Grade, Section", Needed, "The grade level (11 or 12) and the name of the section, like A.", "Choose the grade from the list; type the name."),
            new(SectionsSheet, "Adviser (employee number)", Optional, "The employee number of the teacher who advises the section. Empty: no adviser.", TemplateGuide.TypeIt),
            new(DayPartsSheet, "Part, Starts, Ends", Needed, "The hours of AM, MID and PM, like 07:30 and 11:30 (at least one hour, between 05:00 and 22:00).", "Choose the part from the list; type the hours like 07:30."),
            new(ClassesSheet, "School year, Strand, Grade, Section", Needed, "The section whose class time it is. The section must exist (in the sheet Sections, or already in the system).", "Choose them from the lists; type the section."),
            new(ClassesSheet, "Course", Needed, "The code of the course. It must be one of the courses of the strand in that grade level (the sheet Curriculum).", "Choose it from the list."),
            new(ClassesSheet, "Part, Days", Needed, "The part of the day (AM, MID or PM) and the days (MWF or TTH).", "Choose them from the lists."),
            new(ClassesSheet, "Starts, Ends", Needed, "The class time like 08:00 and 09:30. It must be inside the hours of its part of the day.", "Type them like 08:00 (24 hours)."),
        ],
            "Leave a sheet empty to change nothing there. The sheets are saved in the order of the tabs: day parts, school years, terms, tracks, strands, courses, curriculum, sections, class schedule.",
            "A row that already exists (the same code, name or section) is changed to what the row says; nothing is deleted if a row is missing. The export of the system is this same workbook with everything in it: change it and import it again.",
            $"At most {MaxRows} rows in a sheet. A row with a problem is left out and says why.");
        return book.ToBytes();
    }

    // ======================================================================= reading

    private static XlsxSheetData? Find(IReadOnlyList<XlsxSheetData> sheets, params string[] names) =>
        sheets.FirstOrDefault(s => names.Any(n => string.Equals(s.Name.Trim(), n, StringComparison.OrdinalIgnoreCase)));

    private static string Key(string text) => new string(text.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    /// <summary>The rows of a sheet that are not empty, with the number of the row and the cells by the column titles (a title that is not found is read from its usual place).</summary>
    private sealed record SheetRows(string Sheet, IReadOnlyList<(int Row, string[] Cells)> Rows, string? FileError);

    private static SheetRows Rows(XlsxSheetData? sheet, string name, string[] headers, string[][] aliases)
    {
        if (sheet is null)
        {
            return new SheetRows(name, [], null);
        }

        var found = Enumerable.Range(1, headers.Length + 6).Select(c => Key(sheet.Cell(1, c))).ToList();
        var columns = new int[headers.Length];
        for (var i = 0; i < headers.Length; i++)
        {
            var at = new[] { headers[i] }.Concat(aliases[i]).Select(Key).Select(k => found.IndexOf(k)).FirstOrDefault(x => x >= 0, -1);
            columns[i] = at >= 0 ? at + 1 : i + 1;
        }

        var first = Key(headers[0]);
        if (!found.Contains(first) && !aliases[0].Select(Key).Any(found.Contains))
        {
            return new SheetRows(name, [], $"The sheet {sheet.Name} has no column {headers[0]} in its first row. Download the template again.");
        }

        var rows = new List<(int, string[])>();
        for (var r = 2; r <= sheet.Rows.Count; r++)
        {
            var cells = columns.Select(c => sheet.Cell(r, c).Trim()).ToArray();
            if (cells.All(c => c.Length == 0))
            {
                continue;
            }

            if (rows.Count >= MaxRows)
            {
                return new SheetRows(name, rows, $"The sheet {sheet.Name} has more than {MaxRows} rows. Import it in parts.");
            }

            rows.Add((r, cells));
        }

        return new SheetRows(name, rows, null);
    }

    private static bool? ReadYesNo(string text, bool whenEmpty, List<string> errors, string label)
    {
        if (text.Length == 0)
        {
            return whenEmpty;
        }

        switch (Key(text))
        {
            case "yes" or "y" or "true" or "1" or "on":
                return true;
            case "no" or "n" or "false" or "0" or "off":
                return false;
            default:
                errors.Add($"{label} must be Yes or No.");
                return null;
        }
    }

    private static DateOnly? ReadDay(string text, string label, List<string> errors)
    {
        if (text.Length == 0)
        {
            errors.Add($"{label}: write the day like 2027-06-07.");
            return null;
        }

        if (XlsxReader.ToDate(text) is { } day)
        {
            return DateOnly.FromDateTime(day);
        }

        errors.Add($"{label}: write the day like 2027-06-07 (year-month-day).");
        return null;
    }

    private static TimeOnly? ReadTime(string text, string label, List<string> errors)
    {
        if (text.Length == 0)
        {
            errors.Add($"{label}: write the hour like 08:00.");
            return null;
        }

        if (ClassTime.Parse(text) is { } plain)
        {
            return plain;
        }

        // 8:00 AM, and a time typed in Excel (a fraction of a day).
        if (TimeOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return new TimeOnly(parsed.Hour, parsed.Minute);
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var fraction) && fraction is >= 0 and < 1)
        {
            var minutes = (int)Math.Round(fraction * 24 * 60);
            return new TimeOnly(minutes / 60 % 24, minutes % 60);
        }

        errors.Add($"{label}: write the hour like 08:00 (24 hours).");
        return null;
    }

    private static int? ReadInt(string text, string label, int min, int max, List<string> errors)
    {
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) && number == decimal.Truncate(number) && number >= min && number <= max)
        {
            return (int)number;
        }

        errors.Add(min == max - 1 ? $"{label} must be {min} or {max}." : $"{label} must be a number from {min} to {max}.");
        return null;
    }

    private static SetupLine Line(string sheet, int row, string title, List<string> errors) => new(sheet, row, title, errors);

    /// <summary>
    /// Reads the workbook. Each row of a sheet becomes one thing to save, checked for what a sheet can check by itself (the needed cells, the numbers, the days and the hours, a code written twice).
    /// What needs the system (a school year that does not exist, a course that is not in the strand) is checked when the rows are saved.
    /// </summary>
    public static SetupBook Parse(byte[] data)
    {
        IReadOnlyList<XlsxSheetData> sheets;
        try
        {
            sheets = XlsxReader.Read(data);
        }
        catch (InvalidDataException problem)
        {
            return Empty(problem.Message);
        }

        var years = Rows(Find(sheets, YearsSheet, "School Year", "Years"), YearsSheet, YearHeaders, [[], ["Start", "Starts", "First"], ["End", "Ends", "Last"]]);
        var terms = Rows(Find(sheets, TermsSheet, "Term"), TermsSheet, TermHeaders, [["Year", "Schoolyear"], ["Term number", "No"], [], ["Start", "Starts", "First"], ["End", "Ends", "Last"], ["Current", "Is current"]]);
        var tracks = Rows(Find(sheets, TracksSheet, "Track"), TracksSheet, TrackHeaders, [[], [], ["On", "Is active"]]);
        var strands = Rows(Find(sheets, StrandsSheet, "Strand"), StrandsSheet, StrandHeaders, [[], [], ["Track code"], ["On", "Is active"]]);
        var courses = Rows(Find(sheets, CoursesSheet, "Course", "Subjects"), CoursesSheet, CourseHeaders, [[], [], ["Unit"], [], ["On", "Is active"]]);
        var curriculum = Rows(Find(sheets, CurriculumSheet), CurriculumSheet, CurriculumHeaders, [["Strand code"], ["Grade level"], ["Term number"], ["Course code"]]);
        var sections = Rows(Find(sheets, SectionsSheet, "Section"), SectionsSheet, SectionHeaders, [["Year"], ["Strand code"], ["Grade level"], ["Name", "Section name"], ["Adviser", "Adviser employee number"]]);
        var dayParts = Rows(Find(sheets, DayPartsSheet, "Day Part", "Schedules"), DayPartsSheet, DayPartHeaders, [["Code"], ["Start", "From"], ["End", "To"]]);
        var classes = Rows(Find(sheets, ClassesSheet, "Class Times", "Classes", "Class time"), ClassesSheet, ClassHeaders, [["Year"], ["Strand code"], ["Grade level"], ["Section name"], ["Course code", "Subject"], ["Day part", "Schedule"], ["Meeting days"], ["Start", "From"], ["End", "To"]]);

        var all = new[] { years, terms, tracks, strands, courses, curriculum, sections, dayParts, classes };
        if (all.FirstOrDefault(s => s.FileError is not null) is { } bad)
        {
            return Empty(bad.FileError);
        }

        if (all.All(s => s.Rows.Count == 0))
        {
            return Empty(sheets.Any(s => all.Any(a => string.Equals(a.Sheet, s.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
                ? "The file has nothing to import. Fill in the rows under the column titles."
                : $"This is not the template: it has none of the sheets {YearsSheet}, {TermsSheet}, {TracksSheet}, {StrandsSheet}, {CoursesSheet}, {CurriculumSheet}, {SectionsSheet}, {DayPartsSheet} or {ClassesSheet}. Download the template again.");
        }

        var seen = new HashSet<string>();
        bool Fresh(List<string> errors, string sheet, int row, string key, string what)
        {
            if (!seen.Add($"{sheet}|{key.ToLowerInvariant()}"))
            {
                errors.Add($"{what} is written twice in this sheet (see the row above it).");
                return false;
            }

            return true;
        }

        var yearRows = new List<SetupYear>();
        foreach (var (row, c) in years.Rows)
        {
            var errors = new List<string>();
            if (c[0].Length is 0 or > 20) errors.Add("Write the name of the school year, like 2027-2028 (up to 20 characters).");
            else Fresh(errors, YearsSheet, row, c[0], "The school year");
            var start = ReadDay(c[1], "First day", errors);
            var end = ReadDay(c[2], "Last day", errors);
            if (start is { } s0 && end is { } e0 && e0 <= s0) errors.Add("The school year must end after it starts.");
            yearRows.Add(new SetupYear(Line(YearsSheet, row, c[0], errors), c[0], start ?? default, end ?? default));
        }

        var termRows = new List<SetupTerm>();
        foreach (var (row, c) in terms.Rows)
        {
            var errors = new List<string>();
            if (c[0].Length == 0) errors.Add("Write the school year of the term (as in the sheet School Years).");
            var number = ReadInt(c[1], "The term", 1, 9, errors);
            if (c[2].Length is 0 or > 50) errors.Add("Write the name of the term, like 1st Term (up to 50 characters).");
            var start = ReadDay(c[3], "First day", errors);
            var end = ReadDay(c[4], "Last day", errors);
            if (start is { } s0 && end is { } e0 && e0 < s0) errors.Add("The term can't end before it starts.");
            var current = ReadYesNo(c[5], false, errors, "Current term");
            if (c[0].Length > 0 && number is { } n) Fresh(errors, TermsSheet, row, $"{c[0]}|{n}", $"Term {n} of {c[0]}");
            termRows.Add(new SetupTerm(Line(TermsSheet, row, c[2], errors), c[0], number ?? 0, c[2], start ?? default, end ?? default, current ?? false));
        }

        var trackRows = new List<SetupTrack>();
        foreach (var (row, c) in tracks.Rows)
        {
            var errors = new List<string>();
            if (c[0].Length is 0 or > 20) errors.Add("Write the code of the track, like ACAD (up to 20 characters).");
            else Fresh(errors, TracksSheet, row, c[0], "The code");
            if (c[1].Length is 0 or > 100) errors.Add("Write the name of the track (up to 100 characters).");
            var active = ReadYesNo(c[2], true, errors, "Active");
            trackRows.Add(new SetupTrack(Line(TracksSheet, row, c[0], errors), c[0], c[1], active ?? true));
        }

        var strandRows = new List<SetupStrand>();
        foreach (var (row, c) in strands.Rows)
        {
            var errors = new List<string>();
            if (c[0].Length is 0 or > 20) errors.Add("Write the code of the strand, like BUSSENT (up to 20 characters).");
            else Fresh(errors, StrandsSheet, row, c[0], "The code");
            if (c[1].Length is 0 or > 150) errors.Add("Write the name of the strand (up to 150 characters).");
            if (c[2].Length == 0) errors.Add("Write the code of the track of the strand.");
            var active = ReadYesNo(c[3], true, errors, "Active");
            strandRows.Add(new SetupStrand(Line(StrandsSheet, row, c[0], errors), c[0], c[1], c[2], active ?? true));
        }

        var courseRows = new List<SetupCourse>();
        foreach (var (row, c) in courses.Rows)
        {
            var errors = new List<string>();
            if (c[0].Length is 0 or > 30) errors.Add("Write the code of the course (up to 30 characters).");
            else Fresh(errors, CoursesSheet, row, c[0], "The code");
            if (c[1].Length is 0 or > 150) errors.Add("Write the name of the course (up to 150 characters).");
            var units = 3m;
            if (c[2].Length > 0)
            {
                if (!decimal.TryParse(c[2], NumberStyles.Number, CultureInfo.InvariantCulture, out units) || units < 0.5m || units > 12 || decimal.Round(units, 1) != units) errors.Add("The units are from 0.5 to 12 (one decimal at most, like 1.5 or 3).");
            }

            if (c[3].Length > 500) errors.Add("The description can have at most 500 characters.");
            var active = ReadYesNo(c[4], true, errors, "Active");
            courseRows.Add(new SetupCourse(Line(CoursesSheet, row, c[0], errors), c[0], c[1], units, c[3].Length == 0 ? null : c[3], active ?? true));
        }

        var curriculumRows = new List<SetupCurriculum>();
        foreach (var (row, c) in curriculum.Rows)
        {
            var errors = new List<string>();
            if (c[0].Length == 0) errors.Add("Write the code of the strand.");
            var grade = ReadInt(c[1], "The grade", 11, 12, errors);
            var term = ReadInt(c[2], "The term", 1, 9, errors);
            if (c[3].Length == 0) errors.Add("Write the code of the course.");
            if (errors.Count == 0) Fresh(errors, CurriculumSheet, row, $"{c[0]}|{grade}|{term}|{c[3]}", "This course of the strand");
            curriculumRows.Add(new SetupCurriculum(Line(CurriculumSheet, row, $"{c[0]} · {c[3]}", errors), c[0], grade ?? 0, term ?? 0, c[3]));
        }

        var sectionRows = new List<SetupSection>();
        foreach (var (row, c) in sections.Rows)
        {
            var errors = new List<string>();
            if (c[0].Length == 0) errors.Add("Write the school year of the section.");
            if (c[1].Length == 0) errors.Add("Write the code of the strand of the section.");
            var grade = ReadInt(c[2], "The grade", 11, 12, errors);
            if (c[3].Length is 0 or > 50) errors.Add("Write the name of the section, like A (up to 50 characters).");
            if (errors.Count == 0) Fresh(errors, SectionsSheet, row, $"{c[0]}|{c[1]}|{grade}|{c[3]}", "This section");
            sectionRows.Add(new SetupSection(Line(SectionsSheet, row, $"{c[1]} {c[3]}", errors), c[0], c[1], grade ?? 0, c[3], c[4].Length == 0 ? null : c[4]));
        }

        var partRows = new List<SetupDayPart>();
        foreach (var (row, c) in dayParts.Rows)
        {
            var errors = new List<string>();
            var code = c[0].ToUpperInvariant();
            if (code is not ("AM" or "MID" or "PM")) errors.Add("The part must be AM, MID or PM.");
            else Fresh(errors, DayPartsSheet, row, code, $"{code}");
            var start = ReadTime(c[1], "Starts", errors);
            var end = ReadTime(c[2], "Ends", errors);
            if (start is { } s0 && end is { } e0 && e0 <= s0) errors.Add("It must end after it starts.");
            partRows.Add(new SetupDayPart(Line(DayPartsSheet, row, code, errors), code, start ?? default, end ?? default));
        }

        var classRows = new List<SetupClass>();
        foreach (var (row, c) in classes.Rows)
        {
            var errors = new List<string>();
            if (c[0].Length == 0) errors.Add("Write the school year of the section.");
            if (c[1].Length == 0) errors.Add("Write the code of the strand.");
            var grade = ReadInt(c[2], "The grade", 11, 12, errors);
            if (c[3].Length == 0) errors.Add("Write the name of the section.");
            if (c[4].Length == 0) errors.Add("Write the code of the course.");
            var part = c[5].ToUpperInvariant();
            if (part is not ("AM" or "MID" or "PM")) errors.Add("The part must be AM, MID or PM.");
            var days = c[6].ToUpperInvariant();
            if (!MeetingDays.IsValid(days)) errors.Add("The days must be MWF or TTH.");
            var start = ReadTime(c[7], "Starts", errors);
            var end = ReadTime(c[8], "Ends", errors);
            if (start is { } s0 && end is { } e0 && e0 <= s0) errors.Add("The class must end after it starts.");
            if (errors.Count == 0) Fresh(errors, ClassesSheet, row, $"{c[0]}|{c[1]}|{grade}|{c[3]}|{c[4]}", "This course of the section");
            classRows.Add(new SetupClass(Line(ClassesSheet, row, $"{c[1]} {c[3]} · {c[4]}", errors), c[0], c[1], grade ?? 0, c[3], c[4], part, days, start ?? default, end ?? default));
        }

        return new SetupBook(partRows, yearRows, termRows, trackRows, strandRows, courseRows, curriculumRows, sectionRows, classRows, null);
    }

    private static SetupBook Empty(string? error) => new([], [], [], [], [], [], [], [], [], error);
}
