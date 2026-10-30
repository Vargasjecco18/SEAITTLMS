using System.Globalization;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>What happened to one row of the file.</summary>
public sealed record ImportRowResult(
    int Row, string Lrn, string Name, string Section, bool Succeeded, IReadOnlyList<string> Errors, string? Email, string? Password, bool NewAccount);

public sealed record ImportResult(IReadOnlyList<ImportRowResult> Rows, string? FileError, string TermLabel = "")
{
    public int Enrolled => Rows.Count(r => r.Succeeded);

    public int Failed => Rows.Count(r => !r.Succeeded);
}

/// <summary>
/// Enrolls many students at once from an Excel file. The registrar downloads the template (<see cref="BuildTemplateAsync"/>), types or pastes the students
/// in it (LRN, name, grade level, strand, section), and uploads it for a term (<see cref="ImportAsync"/>). Every row goes through the same
/// <see cref="EnrollmentService.EnrollAsync"/> as the enrollment form, so all the rules hold (the section limit, the new account, a student in the archive),
/// and a row with a problem does not stop the others. The result can be saved as an Excel file with the sign-in details of the new accounts.
/// </summary>
public sealed class EnrollmentImportService(EnrollmentService enrollment)
{
    public const int MaxRows = 500;
    private const int TemplateRows = 60;

    private static readonly string[] Headers = ["LRN", "First name", "Middle name", "Last name", "Name extension", "Phone number", "Grade level", "Track", "Strand", "Section"];

    /// <summary>The empty template: the column titles, drop-down lists for the grade level, the strand and the section, and a sheet that says how to fill it.</summary>
    public async Task<byte[]> BuildTemplateAsync(CancellationToken ct = default)
    {
        var lookups = await enrollment.GetLookupsAsync(ct);
        var book = new XlsxBook();

        var sheet = book.AddSheet("Students");
        sheet.Row(1, Headers, XlsxStyle.Header);
        sheet.Width(1, 16).Width(2, 18).Width(3, 18).Width(4, 18).Width(5, 12).Width(6, 16).Width(7, 12).Width(8, 14).Width(9, 14).Width(10, 10);
        for (var row = 2; row <= TemplateRows + 1; row++)
        {
            for (var column = 1; column <= Headers.Length; column++)
            {
                sheet.Set(row, column, null, column is 1 or 6 ? XlsxStyle.InputText : XlsxStyle.Input);
            }
        }

        sheet.FrozenRows = 1;
        sheet.ListChoice($"G2:G{TemplateRows + 1}", "11", "12");
        sheet.ListChoice($"J2:J{TemplateRows + 1}", SectionNames.Choices.ToArray());
        if (lookups.Tracks.Count > 0)
        {
            sheet.ListFrom($"H2:H{TemplateRows + 1}", $"Lists!$A$2:$A${lookups.Tracks.Count + 1}");
        }

        if (lookups.Strands.Count > 0)
        {
            sheet.ListFrom($"I2:I{TemplateRows + 1}", $"Lists!$C$2:$C${lookups.Strands.Count + 1}");
        }

        sheet.Tip("A2:A" + (TemplateRows + 1), "Type the 12 digits of the LRN.").Tip("B2:B" + (TemplateRows + 1), "Type the first name (needed).").Tip("D2:D" + (TemplateRows + 1), "Type the last name (needed).");
        TemplateGuide.Add(book, "How to fill the Students sheet",
        [
            new("Students", "LRN", "Needed", "The 12 digits of the LRN.", TemplateGuide.TypeIt),
            new("Students", "First name, Last name", "Needed", "The names. Small letters are fine: the system writes them with capital letters.", TemplateGuide.TypeIt),
            new("Students", "Middle name, Name extension, Phone number", "Optional", "Only if the student has one (Jr., III ...).", TemplateGuide.TypeIt),
            new("Students", "Grade level", "Needed", "11 or 12.", "Choose it from the list."),
            new("Students", "Track", "Optional", "It is found from the strand, so you may leave it empty.", "Choose it from the list."),
            new("Students", "Strand", "Needed", "The code of the strand (the sheet Lists shows them).", "Choose it from the list."),
            new("Students", "Section", "Needed", "A, B or C.", "Choose it from the list."),
        ],
            "A student who already has an account keeps the name and the sign-in details the system has.",
            "A student who changes strand starts again in Grade 11.",
            $"A section can have {enrollment.SectionCapacity} students. A row for a full section is not enrolled and is listed with the reason.",
            $"At most {MaxRows} students in one file. On the Enrollments page, choose the term before you import.");

        var lists = book.AddSheet("Lists");
        lists.Row(1, ["Track", "Track name", "Strand", "Strand name", "Section"], XlsxStyle.Header);
        for (var i = 0; i < lookups.Tracks.Count; i++)
        {
            lists.Set(i + 2, 1, lookups.Tracks[i].Code).Set(i + 2, 2, lookups.Tracks[i].Name);
        }

        for (var i = 0; i < lookups.Strands.Count; i++)
        {
            lists.Set(i + 2, 3, lookups.Strands[i].Code).Set(i + 2, 4, lookups.Strands[i].Name);
        }

        for (var i = 0; i < SectionNames.Choices.Count; i++)
        {
            lists.Set(i + 2, 5, SectionNames.Choices[i]);
        }

        lists.Width(1, 14).Width(2, 36).Width(3, 14).Width(4, 44).Width(5, 10);
        return book.ToBytes();
    }

    /// <summary>Reads the file and enrolls its students for the term, one row after the other.</summary>
    public async Task<ImportResult> ImportAsync(byte[] file, int termId, int actingUserId, CancellationToken ct = default)
    {
        var lookups = await enrollment.GetLookupsAsync(ct);
        var term = lookups.Terms.FirstOrDefault(t => t.Id == termId);
        if (term is null)
        {
            return new ImportResult([], "Choose the term the students are enrolled for.");
        }

        var termLabel = $"{term.SchoolYear} · {term.Name}";
        IReadOnlyList<XlsxSheetData> sheets;
        try
        {
            sheets = XlsxReader.Read(file);
        }
        catch (InvalidDataException ex)
        {
            return new ImportResult([], ex.Message, termLabel);
        }

        var data = sheets.FirstOrDefault(s => s.Name.Equals("Students", StringComparison.OrdinalIgnoreCase)) ?? sheets[0];

        // The titles are in the first row that has an LRN cell; the columns are found by their titles, so their order does not matter.
        var headerRow = Enumerable.Range(1, Math.Min(10, data.Rows.Count)).FirstOrDefault(r => data.Rows[r - 1].Any(c => Key(c) == "lrn"));
        if (headerRow == 0)
        {
            return new ImportResult([], "The column titles were not found. Use the template (the first row has LRN, First name, Last name ...).", termLabel);
        }

        var columns = new Dictionary<string, int>();
        for (var c = 1; c <= data.Rows[headerRow - 1].Count; c++)
        {
            if (Alias(Key(data.Cell(headerRow, c))) is { } name && !columns.ContainsKey(name))
            {
                columns[name] = c;
            }
        }

        var missing = new[] { "lrn", "first", "last", "grade", "strand", "section" }.Where(n => !columns.ContainsKey(n)).ToList();
        if (missing.Count > 0)
        {
            var words = new Dictionary<string, string> { ["lrn"] = "LRN", ["first"] = "First name", ["last"] = "Last name", ["grade"] = "Grade level", ["strand"] = "Strand", ["section"] = "Section" };
            return new ImportResult([], "The file does not have the column " + string.Join(", ", missing.Select(m => words[m])) + ". Use the template.", termLabel);
        }

        string Get(int row, string name) => columns.TryGetValue(name, out var column) ? data.Cell(row, column).Trim() : "";

        var rows = new List<int>();
        for (var r = headerRow + 1; r <= data.Rows.Count; r++)
        {
            if (data.Rows[r - 1].Any(c => !string.IsNullOrWhiteSpace(c)))
            {
                rows.Add(r);
            }
        }

        if (rows.Count == 0)
        {
            return new ImportResult([], "There is no student in the file.", termLabel);
        }

        if (rows.Count > MaxRows)
        {
            return new ImportResult([], $"The file has {rows.Count} students. Send at most {MaxRows} at a time.", termLabel);
        }

        var results = new List<ImportRowResult>();
        var seen = new HashSet<string>();
        foreach (var r in rows)
        {
            ct.ThrowIfCancellationRequested();
            var lrn = ReadLrn(Get(r, "lrn"));
            var name = string.Join(' ', new[] { Get(r, "first"), Get(r, "middle"), Get(r, "last"), Get(r, "extension") }.Where(p => p.Length > 0));
            var errors = new List<string>();

            if (!seen.Add(lrn) && lrn.Length > 0)
            {
                errors.Add("This LRN is in the file more than once.");
            }

            var grade = ReadGrade(Get(r, "grade"));
            if (grade is null)
            {
                errors.Add("Grade level must be 11 or 12.");
            }

            var strandText = Get(r, "strand");
            var strand = lookups.Strands.FirstOrDefault(s => s.Code.Equals(strandText, StringComparison.OrdinalIgnoreCase))
                         ?? lookups.Strands.FirstOrDefault(s => strandText.Length > 0 && s.Name.Equals(strandText, StringComparison.OrdinalIgnoreCase));
            if (strand is null)
            {
                errors.Add(strandText.Length == 0 ? "Strand is missing." : $"There is no strand {strandText}. Use a code from the list.");
            }
            else if (Get(r, "track") is { Length: > 0 } trackText
                     && !lookups.Tracks.Any(t => t.Id == strand.TrackId && (t.Code.Equals(trackText, StringComparison.OrdinalIgnoreCase) || t.Name.Equals(trackText, StringComparison.OrdinalIgnoreCase))))
            {
                errors.Add($"The strand {strand.Code} is not in the track {trackText}.");
            }

            var section = Get(r, "section").ToUpperInvariant();
            if (errors.Count > 0)
            {
                results.Add(new ImportRowResult(r, lrn, name, section, false, errors, null, null, false));
                continue;
            }

            var result = await enrollment.EnrollAsync(new EnrollmentRequest(
                lrn, Get(r, "first"), Get(r, "middle"), Get(r, "last"), grade!.Value, termId, strand!.TrackId, strand.Id, section,
                Get(r, "phone") is { Length: > 0 } phone ? phone : null, Get(r, "extension") is { Length: > 0 } extension ? extension : null), actingUserId, ct);

            results.Add(result.Receipt is { } receipt
                ? new ImportRowResult(r, lrn, receipt.FullName, receipt.Section, true, [], receipt.Email, receipt.NewAccount ? receipt.Password : null, receipt.NewAccount)
                : new ImportRowResult(r, lrn, name, section, false, result.Errors, null, null, false));
        }

        return new ImportResult(results, null, termLabel);
    }

    /// <summary>The result as an Excel file: one line for each student, who was enrolled, what went wrong, and the sign-in details of the new accounts.</summary>
    public static byte[] BuildResultFile(ImportResult result)
    {
        var book = new XlsxBook();
        var sheet = book.AddSheet("Result");
        sheet.Set(1, 1, "Enrollment import · " + result.TermLabel, XlsxStyle.Title);
        sheet.Set(2, 1, $"{result.Enrolled} enrolled, {result.Failed} not enrolled.", XlsxStyle.Note);
        sheet.Row(4, ["Row in the file", "LRN", "Student", "Section", "Result", "Problem", "Email", "Password (new accounts)"], XlsxStyle.Header);
        var line = 5;
        foreach (var row in result.Rows)
        {
            sheet.Set(line, 1, row.Row, XlsxStyle.Integer).Set(line, 2, row.Lrn, XlsxStyle.Text).Set(line, 3, row.Name).Set(line, 4, row.Section)
                .Set(line, 5, row.Succeeded ? "Enrolled" : "Not enrolled", row.Succeeded ? XlsxStyle.Normal : XlsxStyle.Bold)
                .Set(line, 6, string.Join(" ", row.Errors), XlsxStyle.Wrap).Set(line, 7, row.Email).Set(line, 8, row.Password, XlsxStyle.Text);
            line++;
        }

        sheet.Width(1, 14).Width(2, 16).Width(3, 30).Width(4, 14).Width(5, 14).Width(6, 60).Width(7, 40).Width(8, 24);
        sheet.FrozenRows = 4;
        if (result.Rows.Any(r => r.Password is not null))
        {
            sheet.Set(line + 1, 1, "The passwords are given only once, here. Hand them to the students and keep this file private.", XlsxStyle.Note);
        }

        return book.ToBytes();
    }

    private static string Key(string text) => new string((text ?? "").Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static string? Alias(string key) => key switch
    {
        "lrn" or "learnerreferencenumber" => "lrn",
        "firstname" or "first" or "givenname" => "first",
        "middlename" or "middle" or "middleinitial" => "middle",
        "lastname" or "last" or "surname" or "familyname" => "last",
        "nameextension" or "extension" or "suffix" => "extension",
        "phonenumber" or "phone" or "mobile" or "mobilenumber" or "contactnumber" => "phone",
        "gradelevel" or "grade" or "year" => "grade",
        "track" => "track",
        "strand" => "strand",
        "section" => "section",
        _ => null,
    };

    /// <summary>The LRN as 12 digits. A number typed in Excel can come as 1.00000000001E+11: it is turned back into digits.</summary>
    private static string ReadLrn(string text)
    {
        text = text.Trim();
        if (text.Any(ch => ch is 'E' or 'e' or '.') && decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            text = number.ToString("0", CultureInfo.InvariantCulture);
        }

        return new string(text.Where(char.IsAsciiDigit).ToArray());
    }

    private static byte? ReadGrade(string text)
    {
        var digits = new string(text.Where(char.IsAsciiDigit).ToArray());
        if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            digits = Math.Truncate(number).ToString(CultureInfo.InvariantCulture);
        }

        return digits is "11" or "12" ? byte.Parse(digits, CultureInfo.InvariantCulture) : null;
    }
}
