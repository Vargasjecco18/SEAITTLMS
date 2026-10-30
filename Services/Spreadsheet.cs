using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace SEAITT_LMS.Services;

/// <summary>How a cell looks in the Excel files the system makes (see <see cref="XlsxSheet"/>). The numbers are the positions of the styles in styles.xml below.</summary>
public enum XlsxStyle
{
    Normal = 0,

    /// <summary>The column titles: white bold letters on a blue band.</summary>
    Header = 1,

    /// <summary>The title at the top of a sheet.</summary>
    Title = 2,

    /// <summary>A number with two decimals (scores, grades).</summary>
    Decimal = 3,

    /// <summary>A fraction shown as a percent (0.85 shows 85.0%).</summary>
    Percent = 4,

    /// <summary>A date (yyyy-mm-dd).</summary>
    Date = 5,

    /// <summary>Text that wraps in its cell.</summary>
    Wrap = 6,

    /// <summary>A small grey note.</summary>
    Note = 7,

    /// <summary>A cell to type in (light yellow with a border), for the templates.</summary>
    Input = 8,

    Bold = 9,

    /// <summary>Bold number with two decimals (totals).</summary>
    BoldDecimal = 10,

    /// <summary>A whole number.</summary>
    Integer = 11,

    /// <summary>Text that stays text (a 12-digit LRN is not turned into a number).</summary>
    Text = 12,

    /// <summary>A cell to type in, kept as text.</summary>
    InputText = 13,
}

/// <summary>One sheet of an Excel file. Rows and columns start at 1, like in Excel.</summary>
public sealed class XlsxSheet(string name)
{
    private readonly SortedDictionary<int, SortedDictionary<int, (object? Value, bool IsFormula, XlsxStyle Style, double? Cached)>> cells = [];
    private readonly Dictionary<int, double> widths = [];
    private readonly List<Validation> lists = [];
    private readonly List<string> merges = [];

    public string Name { get; } = name;

    /// <summary>Rows kept in view while scrolling (the title rows), and columns.</summary>
    public int FrozenRows { get; set; }

    public int FrozenColumns { get; set; }

    /// <summary>Puts a filter button on the cells of this row (the header row), over this many columns.</summary>
    public (int Row, int Columns)? AutoFilter { get; set; }

    public int MaxRow => cells.Count == 0 ? 0 : cells.Keys.Max();

    /// <summary>A value: text, a number, a date or a yes/no. Null leaves the cell empty (only its style is kept).</summary>
    public XlsxSheet Set(int row, int column, object? value, XlsxStyle style = XlsxStyle.Normal)
    {
        Put(row, column, value, false, style);
        return this;
    }

    /// <summary>
    /// A formula, written without the equals sign: <c>SUM(B2:B9)</c>. Excel works it out when the file is opened. The answer we already know can be given
    /// as <paramref name="cached"/>, so a program that does not work out formulas (a viewer on a phone) still shows it.
    /// </summary>
    public XlsxSheet Formula(int row, int column, string formula, XlsxStyle style = XlsxStyle.Normal, double? cached = null)
    {
        Put(row, column, formula.TrimStart('='), true, style, cached);
        return this;
    }

    /// <summary>A whole row of values, from the first column on.</summary>
    public XlsxSheet Row(int row, IEnumerable<object?> values, XlsxStyle style = XlsxStyle.Normal)
    {
        var column = 1;
        foreach (var value in values)
        {
            Set(row, column++, value, style);
        }

        return this;
    }

    public XlsxSheet Width(int column, double characters)
    {
        widths[column] = characters;
        return this;
    }

    public XlsxSheet Merge(string range)
    {
        merges.Add(range);
        return this;
    }

    /// <summary>One rule of the cells of a range: a drop-down list, or a number between two limits, with the tip Excel shows when the cell is selected.</summary>
    private sealed record Validation(string Range, string Type, string[] Items, string? Formula, decimal? Min, decimal? Max, string? Tip);

    /// <summary>The cells of the range (like <c>G2:G500</c>) get a drop-down list of these choices. <paramref name="tip"/> is what Excel shows when a cell is selected.</summary>
    public XlsxSheet ListChoice(string range, string[] items, string? tip = null)
    {
        lists.Add(new Validation(range, "list", items, null, null, null, tip ?? "Choose it from the list (press the arrow at the right of the cell)."));
        return this;
    }

    public XlsxSheet ListChoice(string range, params string[] items) => ListChoice(range, items, null);

    /// <summary>The cells of the range get a drop-down list made from other cells, like <c>Lists!$B$2:$B$30</c> (for choices too long to write in the rule).</summary>
    public XlsxSheet ListFrom(string range, string cells, string? tip = null)
    {
        lists.Add(new Validation(range, "list", [], cells, null, null, tip ?? "Choose it from the list (press the arrow at the right of the cell)."));
        return this;
    }

    /// <summary>The cells of the range only take a number from <paramref name="min"/> to <paramref name="max"/> (Excel refuses another value and shows the tip when a cell is selected).</summary>
    public XlsxSheet Number(string range, decimal min, decimal max, string? tip = null)
    {
        lists.Add(new Validation(range, "decimal", [], null, min, max, tip ?? $"Type a number from {min.ToString(CultureInfo.InvariantCulture)} to {max.ToString(CultureInfo.InvariantCulture)}."));
        return this;
    }

    /// <summary>A tip that Excel shows when a cell of the range is selected (for the cells that have no list: what to type).</summary>
    public XlsxSheet Tip(string range, string tip)
    {
        lists.Add(new Validation(range, "none", [], null, null, null, tip));
        return this;
    }

    public static string ColumnName(int column)
    {
        var name = "";
        for (var n = column; n > 0; n = (n - 1) / 26)
        {
            name = (char)('A' + (n - 1) % 26) + name;
        }

        return name;
    }

    public static string Ref(int row, int column) => ColumnName(column) + row.ToString(CultureInfo.InvariantCulture);

    private void Put(int row, int column, object? value, bool isFormula, XlsxStyle style, double? cached = null)
    {
        if (row < 1 || column < 1 || row > 1_048_576 || column > 16_384)
        {
            throw new ArgumentOutOfRangeException(nameof(row), "A cell must be inside the sheet.");
        }

        if (!cells.TryGetValue(row, out var line))
        {
            cells[row] = line = [];
        }

        line[column] = (value, isFormula, style, cached);
    }

    internal void Write(XmlWriter xml)
    {
        xml.WriteStartElement("worksheet", Xlsx.MainNs);

        xml.WriteStartElement("sheetViews");
        xml.WriteStartElement("sheetView");
        xml.WriteAttributeString("workbookViewId", "0");
        if (FrozenRows > 0 || FrozenColumns > 0)
        {
            xml.WriteStartElement("pane");
            if (FrozenColumns > 0) { xml.WriteAttributeString("xSplit", FrozenColumns.ToString(CultureInfo.InvariantCulture)); }
            if (FrozenRows > 0) { xml.WriteAttributeString("ySplit", FrozenRows.ToString(CultureInfo.InvariantCulture)); }
            xml.WriteAttributeString("topLeftCell", Ref(FrozenRows + 1, FrozenColumns + 1));
            xml.WriteAttributeString("activePane", FrozenRows > 0 && FrozenColumns > 0 ? "bottomRight" : FrozenRows > 0 ? "bottomLeft" : "topRight");
            xml.WriteAttributeString("state", "frozen");
            xml.WriteEndElement();
        }

        xml.WriteEndElement();
        xml.WriteEndElement();

        if (widths.Count > 0)
        {
            xml.WriteStartElement("cols");
            foreach (var (column, width) in widths.OrderBy(w => w.Key))
            {
                xml.WriteStartElement("col");
                xml.WriteAttributeString("min", column.ToString(CultureInfo.InvariantCulture));
                xml.WriteAttributeString("max", column.ToString(CultureInfo.InvariantCulture));
                xml.WriteAttributeString("width", width.ToString("0.##", CultureInfo.InvariantCulture));
                xml.WriteAttributeString("customWidth", "1");
                xml.WriteEndElement();
            }

            xml.WriteEndElement();
        }

        xml.WriteStartElement("sheetData");
        foreach (var (row, line) in cells)
        {
            xml.WriteStartElement("row");
            xml.WriteAttributeString("r", row.ToString(CultureInfo.InvariantCulture));
            foreach (var (column, (value, isFormula, style, cached)) in line)
            {
                WriteCell(xml, Ref(row, column), value, isFormula, style, cached);
            }

            xml.WriteEndElement();
        }

        xml.WriteEndElement();

        if (AutoFilter is { } filter)
        {
            xml.WriteStartElement("autoFilter");
            xml.WriteAttributeString("ref", $"{Ref(filter.Row, 1)}:{Ref(Math.Max(filter.Row, MaxRow), filter.Columns)}");
            xml.WriteEndElement();
        }

        if (merges.Count > 0)
        {
            xml.WriteStartElement("mergeCells");
            xml.WriteAttributeString("count", merges.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var range in merges)
            {
                xml.WriteStartElement("mergeCell");
                xml.WriteAttributeString("ref", range);
                xml.WriteEndElement();
            }

            xml.WriteEndElement();
        }

        if (lists.Count > 0)
        {
            xml.WriteStartElement("dataValidations");
            xml.WriteAttributeString("count", lists.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var rule in lists)
            {
                xml.WriteStartElement("dataValidation");
                if (rule.Type != "none")
                {
                    xml.WriteAttributeString("type", rule.Type);
                    if (rule.Type == "decimal")
                    {
                        xml.WriteAttributeString("operator", "between");
                    }

                    xml.WriteAttributeString("showErrorMessage", "1");
                    xml.WriteAttributeString("errorTitle", "Not allowed");
                    xml.WriteAttributeString("error", rule.Type == "list" ? "Choose one of the choices of the list." : rule.Tip ?? "This value is not allowed.");
                }

                xml.WriteAttributeString("allowBlank", "1");
                if (!string.IsNullOrEmpty(rule.Tip))
                {
                    xml.WriteAttributeString("showInputMessage", "1");
                    xml.WriteAttributeString("promptTitle", "What to put here");
                    xml.WriteAttributeString("prompt", rule.Tip.Length > 250 ? rule.Tip[..250] : rule.Tip);
                }

                xml.WriteAttributeString("sqref", rule.Range);
                if (rule.Type == "list")
                {
                    xml.WriteElementString("formula1", rule.Formula ?? "\"" + string.Join(",", rule.Items).Replace("\"", "\"\"") + "\"");
                }
                else if (rule.Type == "decimal")
                {
                    xml.WriteElementString("formula1", rule.Min!.Value.ToString(CultureInfo.InvariantCulture));
                    xml.WriteElementString("formula2", rule.Max!.Value.ToString(CultureInfo.InvariantCulture));
                }

                xml.WriteEndElement();
            }

            xml.WriteEndElement();
        }

        xml.WriteEndElement();
    }

    private static void WriteCell(XmlWriter xml, string reference, object? value, bool isFormula, XlsxStyle style, double? cached)
    {
        xml.WriteStartElement("c");
        xml.WriteAttributeString("r", reference);
        if (style != XlsxStyle.Normal)
        {
            xml.WriteAttributeString("s", ((int)style).ToString(CultureInfo.InvariantCulture));
        }

        if (isFormula)
        {
            xml.WriteElementString("f", (string)value!);
            if (cached is { } known && double.IsFinite(known))
            {
                xml.WriteElementString("v", known.ToString("0.##########", CultureInfo.InvariantCulture));
            }
        }
        else
        {
            switch (value)
            {
                case null:
                    break;
                case bool flag:
                    xml.WriteAttributeString("t", "b");
                    xml.WriteElementString("v", flag ? "1" : "0");
                    break;
                case DateTime date:
                    xml.WriteElementString("v", ToSerial(date).ToString("0.######", CultureInfo.InvariantCulture));
                    break;
                case DateOnly day:
                    xml.WriteElementString("v", ToSerial(day.ToDateTime(TimeOnly.MinValue)).ToString("0.######", CultureInfo.InvariantCulture));
                    break;
                case int or long or short or byte:
                    xml.WriteElementString("v", Convert.ToString(value, CultureInfo.InvariantCulture));
                    break;
                case decimal or double or float:
                    xml.WriteElementString("v", Convert.ToString(value, CultureInfo.InvariantCulture));
                    break;
                default:
                {
                    var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
                    // (An inline string is always text, even when it starts with an equals sign.)
                    xml.WriteAttributeString("t", "inlineStr");
                    xml.WriteStartElement("is");
                    xml.WriteStartElement("t");
                    xml.WriteAttributeString("xml", "space", null, "preserve");
                    xml.WriteString(Xlsx.CleanText(text));
                    xml.WriteEndElement();
                    xml.WriteEndElement();
                    break;
                }
            }
        }

        xml.WriteEndElement();
    }

    /// <summary>Excel counts days from 1900 (with its old mistake of a 29 February 1900), so a modern date is the days since 1899-12-30.</summary>
    internal static double ToSerial(DateTime date) => (date - new DateTime(1899, 12, 30)).TotalDays;
}

/// <summary>An Excel workbook (.xlsx) built in memory. The file is a zip of small XML files, written here without any package.</summary>
public sealed class XlsxBook
{
    private readonly List<XlsxSheet> sheets = [];

    public IReadOnlyList<XlsxSheet> Sheets => sheets;

    public XlsxSheet AddSheet(string name)
    {
        name = Xlsx.SheetName(name, sheets.Select(s => s.Name));
        var sheet = new XlsxSheet(name);
        sheets.Add(sheet);
        return sheet;
    }

    public byte[] ToBytes()
    {
        if (sheets.Count == 0)
        {
            AddSheet("Sheet1");
        }

        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Entry(zip, "[Content_Types].xml", xml =>
            {
                xml.WriteStartElement("Types", "http://schemas.openxmlformats.org/package/2006/content-types");
                Default(xml, "rels", "application/vnd.openxmlformats-package.relationships+xml");
                Default(xml, "xml", "application/xml");
                Override(xml, "/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
                Override(xml, "/xl/styles.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml");
                for (var i = 1; i <= sheets.Count; i++)
                {
                    Override(xml, $"/xl/worksheets/sheet{i}.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
                }

                xml.WriteEndElement();
            });

            Entry(zip, "_rels/.rels", xml =>
            {
                xml.WriteStartElement("Relationships", Xlsx.PackageRelNs);
                Relationship(xml, "rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument", "xl/workbook.xml");
                xml.WriteEndElement();
            });

            Entry(zip, "xl/workbook.xml", xml =>
            {
                xml.WriteStartElement("workbook", Xlsx.MainNs);
                xml.WriteAttributeString("xmlns", "r", null, Xlsx.RelNs);
                xml.WriteStartElement("sheets");
                for (var i = 0; i < sheets.Count; i++)
                {
                    xml.WriteStartElement("sheet");
                    xml.WriteAttributeString("name", sheets[i].Name);
                    xml.WriteAttributeString("sheetId", (i + 1).ToString(CultureInfo.InvariantCulture));
                    xml.WriteAttributeString("id", Xlsx.RelNs, $"rId{i + 1}");
                    xml.WriteEndElement();
                }

                xml.WriteEndElement();
                xml.WriteStartElement("calcPr");
                xml.WriteAttributeString("fullCalcOnLoad", "1");
                xml.WriteEndElement();
                xml.WriteEndElement();
            });

            Entry(zip, "xl/_rels/workbook.xml.rels", xml =>
            {
                xml.WriteStartElement("Relationships", Xlsx.PackageRelNs);
                for (var i = 1; i <= sheets.Count; i++)
                {
                    Relationship(xml, $"rId{i}", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet", $"worksheets/sheet{i}.xml");
                }

                Relationship(xml, $"rId{sheets.Count + 1}", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles", "styles.xml");
                xml.WriteEndElement();
            });

            Entry(zip, "xl/styles.xml", WriteStyles);

            for (var i = 0; i < sheets.Count; i++)
            {
                var sheet = sheets[i];
                Entry(zip, $"xl/worksheets/sheet{i + 1}.xml", sheet.Write);
            }
        }

        return stream.ToArray();
    }

    private static void Entry(ZipArchive zip, string path, Action<XmlWriter> write)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var output = entry.Open();
        using var xml = XmlWriter.Create(output, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false });
        xml.WriteStartDocument(true);
        write(xml);
        xml.WriteEndDocument();
    }

    private static void Default(XmlWriter xml, string extension, string type)
    {
        xml.WriteStartElement("Default");
        xml.WriteAttributeString("Extension", extension);
        xml.WriteAttributeString("ContentType", type);
        xml.WriteEndElement();
    }

    private static void Override(XmlWriter xml, string part, string type)
    {
        xml.WriteStartElement("Override");
        xml.WriteAttributeString("PartName", part);
        xml.WriteAttributeString("ContentType", type);
        xml.WriteEndElement();
    }

    private static void Relationship(XmlWriter xml, string id, string type, string target)
    {
        xml.WriteStartElement("Relationship");
        xml.WriteAttributeString("Id", id);
        xml.WriteAttributeString("Type", type);
        xml.WriteAttributeString("Target", target);
        xml.WriteEndElement();
    }

    /// <summary>The looks of the cells. The position of each cellXfs entry is the number of the <see cref="XlsxStyle"/>.</summary>
    private static void WriteStyles(XmlWriter xml)
    {
        xml.WriteStartElement("styleSheet", Xlsx.MainNs);

        // 164: 0.0%   165: yyyy-mm-dd
        xml.WriteStartElement("numFmts");
        xml.WriteAttributeString("count", "2");
        NumFmt(xml, 164, "0.0%");
        NumFmt(xml, 165, "yyyy\\-mm\\-dd");
        xml.WriteEndElement();

        xml.WriteStartElement("fonts");
        xml.WriteAttributeString("count", "5");
        Font(xml, bold: false, italic: false, size: 11, color: null);        // 0
        Font(xml, bold: true, italic: false, size: 11, color: null);         // 1
        Font(xml, bold: true, italic: false, size: 11, color: "FFFFFFFF");   // 2
        Font(xml, bold: true, italic: false, size: 14, color: null);         // 3
        Font(xml, bold: false, italic: true, size: 10, color: "FF6B7280");   // 4
        xml.WriteEndElement();

        xml.WriteStartElement("fills");
        xml.WriteAttributeString("count", "4");
        PatternFill(xml, "none", null);        // 0
        PatternFill(xml, "gray125", null);     // 1 (Excel needs the first two)
        PatternFill(xml, "solid", "FF1E40AF"); // 2 blue band
        PatternFill(xml, "solid", "FFFEF9C3"); // 3 light yellow
        xml.WriteEndElement();

        xml.WriteStartElement("borders");
        xml.WriteAttributeString("count", "2");
        xml.WriteStartElement("border");
        xml.WriteElementString("left", null);
        xml.WriteElementString("right", null);
        xml.WriteElementString("top", null);
        xml.WriteElementString("bottom", null);
        xml.WriteElementString("diagonal", null);
        xml.WriteEndElement();
        xml.WriteStartElement("border");
        foreach (var side in new[] { "left", "right", "top", "bottom" })
        {
            xml.WriteStartElement(side);
            xml.WriteAttributeString("style", "thin");
            xml.WriteStartElement("color");
            xml.WriteAttributeString("rgb", "FFB6BCC8");
            xml.WriteEndElement();
            xml.WriteEndElement();
        }

        xml.WriteElementString("diagonal", null);
        xml.WriteEndElement();
        xml.WriteEndElement();

        xml.WriteStartElement("cellStyleXfs");
        xml.WriteAttributeString("count", "1");
        xml.WriteStartElement("xf");
        xml.WriteAttributeString("numFmtId", "0");
        xml.WriteAttributeString("fontId", "0");
        xml.WriteAttributeString("fillId", "0");
        xml.WriteAttributeString("borderId", "0");
        xml.WriteEndElement();
        xml.WriteEndElement();

        xml.WriteStartElement("cellXfs");
        xml.WriteAttributeString("count", "14");
        Xf(xml, 0, 0, 0, 0, null);                    // 0 Normal
        Xf(xml, 0, 2, 2, 1, "center", wrap: true);    // 1 Header
        Xf(xml, 0, 3, 0, 0, null);                    // 2 Title
        Xf(xml, 2, 0, 0, 0, null);                    // 3 Decimal
        Xf(xml, 164, 0, 0, 0, null);                  // 4 Percent
        Xf(xml, 165, 0, 0, 0, null);                  // 5 Date
        Xf(xml, 0, 0, 0, 0, "left", wrap: true);      // 6 Wrap
        Xf(xml, 0, 4, 0, 0, null);                    // 7 Note
        Xf(xml, 0, 0, 3, 1, null);                    // 8 Input
        Xf(xml, 0, 1, 0, 0, null);                    // 9 Bold
        Xf(xml, 2, 1, 0, 0, null);                    // 10 BoldDecimal
        Xf(xml, 1, 0, 0, 0, null);                    // 11 Integer
        Xf(xml, 49, 0, 0, 0, null);                   // 12 Text ("@")
        Xf(xml, 49, 0, 3, 1, null);                   // 13 InputText
        xml.WriteEndElement();

        xml.WriteStartElement("cellStyles");
        xml.WriteAttributeString("count", "1");
        xml.WriteStartElement("cellStyle");
        xml.WriteAttributeString("name", "Normal");
        xml.WriteAttributeString("xfId", "0");
        xml.WriteAttributeString("builtinId", "0");
        xml.WriteEndElement();
        xml.WriteEndElement();

        xml.WriteEndElement();
    }

    private static void NumFmt(XmlWriter xml, int id, string code)
    {
        xml.WriteStartElement("numFmt");
        xml.WriteAttributeString("numFmtId", id.ToString(CultureInfo.InvariantCulture));
        xml.WriteAttributeString("formatCode", code);
        xml.WriteEndElement();
    }

    private static void Font(XmlWriter xml, bool bold, bool italic, int size, string? color)
    {
        xml.WriteStartElement("font");
        if (bold) { xml.WriteStartElement("b"); xml.WriteEndElement(); }
        if (italic) { xml.WriteStartElement("i"); xml.WriteEndElement(); }
        xml.WriteStartElement("sz");
        xml.WriteAttributeString("val", size.ToString(CultureInfo.InvariantCulture));
        xml.WriteEndElement();
        if (color is not null)
        {
            xml.WriteStartElement("color");
            xml.WriteAttributeString("rgb", color);
            xml.WriteEndElement();
        }

        xml.WriteStartElement("name");
        xml.WriteAttributeString("val", "Calibri");
        xml.WriteEndElement();
        xml.WriteEndElement();
    }

    private static void PatternFill(XmlWriter xml, string pattern, string? color)
    {
        xml.WriteStartElement("fill");
        xml.WriteStartElement("patternFill");
        xml.WriteAttributeString("patternType", pattern);
        if (color is not null)
        {
            xml.WriteStartElement("fgColor");
            xml.WriteAttributeString("rgb", color);
            xml.WriteEndElement();
            xml.WriteStartElement("bgColor");
            xml.WriteAttributeString("indexed", "64");
            xml.WriteEndElement();
        }

        xml.WriteEndElement();
        xml.WriteEndElement();
    }

    private static void Xf(XmlWriter xml, int numFmt, int font, int fill, int border, string? horizontal, bool wrap = false)
    {
        xml.WriteStartElement("xf");
        xml.WriteAttributeString("numFmtId", numFmt.ToString(CultureInfo.InvariantCulture));
        xml.WriteAttributeString("fontId", font.ToString(CultureInfo.InvariantCulture));
        xml.WriteAttributeString("fillId", fill.ToString(CultureInfo.InvariantCulture));
        xml.WriteAttributeString("borderId", border.ToString(CultureInfo.InvariantCulture));
        xml.WriteAttributeString("xfId", "0");
        if (numFmt != 0) { xml.WriteAttributeString("applyNumberFormat", "1"); }
        if (font != 0) { xml.WriteAttributeString("applyFont", "1"); }
        if (fill != 0) { xml.WriteAttributeString("applyFill", "1"); }
        if (border != 0) { xml.WriteAttributeString("applyBorder", "1"); }
        if (horizontal is not null || wrap)
        {
            xml.WriteAttributeString("applyAlignment", "1");
            xml.WriteStartElement("alignment");
            if (horizontal is not null) { xml.WriteAttributeString("horizontal", horizontal); }
            xml.WriteAttributeString("vertical", "center");
            if (wrap) { xml.WriteAttributeString("wrapText", "1"); }
            xml.WriteEndElement();
        }

        xml.WriteEndElement();
    }
}

/// <summary>The text of every cell of one sheet of an Excel file that was read. Numbers are given as text with a dot for decimals.</summary>
public sealed record XlsxSheetData(string Name, IReadOnlyList<IReadOnlyList<string>> Rows)
{
    /// <summary>The text of a cell (row and column start at 1); empty when there is nothing there.</summary>
    public string Cell(int row, int column) =>
        row >= 1 && row <= Rows.Count && column >= 1 && column <= Rows[row - 1].Count ? Rows[row - 1][column - 1] : "";
}

/// <summary>Reads the Excel files (.xlsx) that people send in: the text and numbers of the cells, nothing else (no formulas are worked out, no macros run).</summary>
public static class XlsxReader
{
    public const int MaxFileBytes = 5 * 1024 * 1024;
    private const int MaxRows = 20_000;
    private const int MaxColumns = 100;
    private const long MaxPartBytes = 40L * 1024 * 1024;

    /// <summary>Reads the first sheet (or every sheet). Throws <see cref="InvalidDataException"/> with a sentence a person can understand when the file is not an Excel file.</summary>
    public static IReadOnlyList<XlsxSheetData> Read(byte[] data)
    {
        if (data.Length == 0 || data.Length > MaxFileBytes)
        {
            throw new InvalidDataException(data.Length == 0 ? "The file is empty." : $"The file is bigger than {MaxFileBytes / (1024 * 1024)} MB.");
        }

        try
        {
            using var zip = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read);
            if (zip.Entries.Sum(e => e.Length) > MaxPartBytes * 2)
            {
                throw new InvalidDataException("The file is too big when it is opened.");
            }

            var workbook = Part(zip, "xl/workbook.xml") ?? throw new InvalidDataException("This is not an Excel (.xlsx) file.");
            var sheetNames = new List<(string Name, string RelId)>();
            using (var xml = Reader(workbook))
            {
                while (xml.Read())
                {
                    if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "sheet")
                    {
                        sheetNames.Add((xml.GetAttribute("name") ?? "Sheet", xml.GetAttribute("id", Xlsx.RelNs) ?? ""));
                    }
                }
            }

            var targets = new Dictionary<string, string>();
            if (Part(zip, "xl/_rels/workbook.xml.rels") is { } rels)
            {
                using var xml = Reader(rels);
                while (xml.Read())
                {
                    if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Relationship")
                    {
                        targets[xml.GetAttribute("Id") ?? ""] = xml.GetAttribute("Target") ?? "";
                    }
                }
            }

            var shared = new List<string>();
            if (Part(zip, "xl/sharedStrings.xml") is { } strings)
            {
                shared = ReadSharedStrings(strings);
            }

            var sheets = new List<XlsxSheetData>();
            for (var i = 0; i < sheetNames.Count; i++)
            {
                var target = targets.GetValueOrDefault(sheetNames[i].RelId) ?? $"worksheets/sheet{i + 1}.xml";
                var path = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
                if (Part(zip, path) is { } sheetStream)
                {
                    sheets.Add(new XlsxSheetData(sheetNames[i].Name, ReadSheet(sheetStream, shared)));
                }
            }

            if (sheets.Count == 0)
            {
                throw new InvalidDataException("There is no sheet in this file.");
            }

            return sheets;
        }
        catch (Exception ex) when (ex is InvalidDataException or XmlException or IOException or NotSupportedException or ArgumentException)
        {
            throw ex is InvalidDataException ? ex : new InvalidDataException("This is not an Excel (.xlsx) file, or it is damaged. Save it as an Excel Workbook (.xlsx) and try again.");
        }
    }

    private static MemoryStream? Part(ZipArchive zip, string path)
    {
        var entry = zip.GetEntry(path);
        if (entry is null)
        {
            return null;
        }

        if (entry.Length > MaxPartBytes)
        {
            throw new InvalidDataException("The file is too big when it is opened.");
        }

        var copy = new MemoryStream();
        using (var input = entry.Open())
        {
            input.CopyTo(copy);
        }

        copy.Position = 0;
        return copy;
    }

    private static XmlReader Reader(Stream stream) =>
        XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreWhitespace = false });

    private static List<string> ReadSharedStrings(Stream stream)
    {
        var list = new List<string>();
        using var xml = Reader(stream);
        var builder = new StringBuilder();
        var inItem = false;
        var inText = false;
        var skip = 0; // phonetic runs (rPh) are not part of the text
        while (xml.Read())
        {
            switch (xml.NodeType)
            {
                case XmlNodeType.Element when xml.LocalName == "si":
                    inItem = true;
                    builder.Clear();
                    break;
                case XmlNodeType.Element when xml.LocalName == "rPh" && !xml.IsEmptyElement:
                    skip++;
                    break;
                case XmlNodeType.Element when xml.LocalName == "t" && inItem && skip == 0 && !xml.IsEmptyElement:
                    inText = true;
                    break;
                case XmlNodeType.Text or XmlNodeType.SignificantWhitespace or XmlNodeType.Whitespace when inText:
                    builder.Append(xml.Value);
                    break;
                case XmlNodeType.EndElement when xml.LocalName == "t":
                    inText = false;
                    break;
                case XmlNodeType.EndElement when xml.LocalName == "rPh":
                    skip--;
                    break;
                case XmlNodeType.EndElement when xml.LocalName == "si":
                    list.Add(builder.ToString());
                    inItem = false;
                    break;
            }
        }

        return list;
    }

    private static List<IReadOnlyList<string>> ReadSheet(Stream stream, List<string> shared)
    {
        var rows = new SortedDictionary<int, SortedDictionary<int, string>>();
        using var xml = Reader(stream);

        int row = 0, column = 0;
        string type = "";
        var value = new StringBuilder();
        var inValue = false;
        var inCell = false;

        while (xml.Read())
        {
            switch (xml.NodeType)
            {
                case XmlNodeType.Element when xml.LocalName == "row":
                    row = int.TryParse(xml.GetAttribute("r"), NumberStyles.None, CultureInfo.InvariantCulture, out var r) ? r : row + 1;
                    break;
                case XmlNodeType.Element when xml.LocalName == "c":
                    inCell = true;
                    type = xml.GetAttribute("t") ?? "";
                    value.Clear();
                    column = ColumnIndex(xml.GetAttribute("r")) is { } c ? c : column + 1;
                    if (xml.IsEmptyElement)
                    {
                        inCell = false;
                    }

                    break;
                case XmlNodeType.Element when inCell && (xml.LocalName is "v" or "t") && !xml.IsEmptyElement:
                    inValue = true;
                    break;
                case XmlNodeType.Text or XmlNodeType.SignificantWhitespace or XmlNodeType.Whitespace when inValue:
                    value.Append(xml.Value);
                    break;
                case XmlNodeType.EndElement when xml.LocalName is "v" or "t":
                    inValue = false;
                    break;
                case XmlNodeType.EndElement when xml.LocalName == "c":
                    if (inCell && row is >= 1 and <= MaxRows && column is >= 1 and <= MaxColumns)
                    {
                        var text = type switch
                        {
                            "s" => int.TryParse(value.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out var index) && index < shared.Count ? shared[index] : "",
                            "b" => value.ToString() == "1" ? "TRUE" : "FALSE",
                            _ => value.ToString(),
                        };
                        if (text.Length > 0)
                        {
                            if (!rows.TryGetValue(row, out var line))
                            {
                                rows[row] = line = [];
                            }

                            line[column] = text;
                        }
                    }

                    inCell = false;
                    break;
            }
        }

        var result = new List<IReadOnlyList<string>>();
        var last = rows.Count == 0 ? 0 : rows.Keys.Max();
        for (var i = 1; i <= last; i++)
        {
            if (!rows.TryGetValue(i, out var line))
            {
                result.Add([]);
                continue;
            }

            var width = line.Keys.Max();
            var cells = new string[width];
            for (var j = 1; j <= width; j++)
            {
                cells[j - 1] = line.GetValueOrDefault(j, "");
            }

            result.Add(cells);
        }

        return result;
    }

    /// <summary>"C12" gives 3.</summary>
    private static int? ColumnIndex(string? reference)
    {
        if (string.IsNullOrEmpty(reference))
        {
            return null;
        }

        var number = 0;
        foreach (var ch in reference)
        {
            if (ch is < 'A' or > 'Z')
            {
                break;
            }

            number = number * 26 + (ch - 'A' + 1);
        }

        return number == 0 ? null : number;
    }

    /// <summary>A date typed in Excel is a number (the days since 1899-12-30). Gives the date, or null when the text is neither a date nor such a number.</summary>
    public static DateTime? ToDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial is > 0 and < 2_958_466)
        {
            return new DateTime(1899, 12, 30).AddDays(serial);
        }

        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ? parsed : null;
    }
}

internal static class Xlsx
{
    public const string MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    public const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    public const string PackageRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";

    /// <summary>A sheet name: at most 31 characters, none of \ / ? * [ ] :, and not the same as another sheet.</summary>
    public static string SheetName(string name, IEnumerable<string> taken)
    {
        var clean = new string((name ?? "").Where(ch => ch is not ('\\' or '/' or '?' or '*' or '[' or ']' or ':') && !char.IsControl(ch)).ToArray()).Trim().Trim('\'');
        if (clean.Length == 0)
        {
            clean = "Sheet";
        }

        if (clean.Length > 31)
        {
            clean = clean[..31];
        }

        var used = taken.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = clean;
        for (var i = 2; used.Contains(result); i++)
        {
            var suffix = " (" + i + ")";
            result = (clean.Length + suffix.Length > 31 ? clean[..(31 - suffix.Length)] : clean) + suffix;
        }

        return result;
    }

    /// <summary>Characters that XML cannot hold are left out.</summary>
    public static string CleanText(string text)
    {
        var builder = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (char.IsHighSurrogate(ch) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                builder.Append(ch).Append(text[++i]);
            }
            else if (XmlConvert.IsXmlChar(ch))
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }
}
