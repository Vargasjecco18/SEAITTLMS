using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace SEAITT_LMS.Services;

/// <summary>
/// The formatted text a student writes in the editor of their work (fonts, sizes, colors, lists, tables ...).
/// What the browser sends is never trusted: <see cref="Sanitize"/> keeps only a short list of tags, attributes and style
/// rules, so the text can be shown to the teacher without any risk of script or foreign content getting in.
/// </summary>
public static partial class RichText
{
    /// <summary>The most characters of formatted text kept for one answer (the formatting itself takes room too).</summary>
    public const int MaxHtmlLength = 200_000;

    private const int MaxDepth = 40;

    private static readonly HashSet<string> Tags = new(StringComparer.Ordinal)
    {
        "p", "div", "br", "hr", "span", "b", "strong", "i", "em", "u", "s", "strike", "del", "sub", "sup",
        "ul", "ol", "li", "h1", "h2", "h3", "h4", "blockquote", "pre", "code", "a",
        "table", "thead", "tbody", "tr", "td", "th",
    };

    private static readonly HashSet<string> VoidTags = new(StringComparer.Ordinal) { "br", "hr" };

    /// <summary>Tags dropped together with everything inside them.</summary>
    private static readonly HashSet<string> DroppedWithContent = new(StringComparer.Ordinal)
    {
        "script", "style", "iframe", "object", "embed", "template", "noscript", "head", "title", "textarea", "select", "svg", "math", "xml",
    };

    private static readonly HashSet<string> StyledTags = new(StringComparer.Ordinal)
    {
        "p", "div", "span", "li", "ul", "ol", "h1", "h2", "h3", "h4", "blockquote", "td", "th", "pre", "code", "b", "strong", "i", "em", "u", "s",
    };

    /// <summary>Block tags: they end a line when the formatted text is turned into plain text.</summary>
    private static readonly HashSet<string> BlockTags = new(StringComparer.Ordinal)
    {
        "p", "div", "li", "tr", "h1", "h2", "h3", "h4", "blockquote", "pre", "table", "ul", "ol", "hr",
    };

    // One tag: <name attr="value" attr='value' attr=value attr>  (a ">" inside quotes does not end it)
    [GeneratedRegex("""<(/?)([a-zA-Z][a-zA-Z0-9]*)((?:\s+[^\s"'<>/=]+(?:\s*=\s*(?:"[^"]*"|'[^']*'|[^\s"'<>=`]+))?)*)\s*/?>""", RegexOptions.CultureInvariant)]
    private static partial Regex TagPattern();

    // The same, but only where the search starts (so a stray "<" does not make the search run to the end of the text).
    [GeneratedRegex("""\G<(/?)([a-zA-Z][a-zA-Z0-9]*)((?:\s+[^\s"'<>/=]+(?:\s*=\s*(?:"[^"]*"|'[^']*'|[^\s"'<>=`]+))?)*)\s*/?>""", RegexOptions.CultureInvariant)]
    private static partial Regex TagHerePattern();

    [GeneratedRegex("""([^\s"'<>/=]+)(?:\s*=\s*(?:"([^"]*)"|'([^']*)'|([^\s"'<>=`]+)))?""", RegexOptions.CultureInvariant)]
    private static partial Regex AttributePattern();

    [GeneratedRegex(@"&(?:#[0-9]{1,7}|#[xX][0-9a-fA-F]{1,6}|[a-zA-Z][a-zA-Z0-9]{1,10});")]
    private static partial Regex EntityPattern();

    /// <summary>The value of one rule of a style="..." for each property that is allowed.</summary>
    private static readonly Dictionary<string, Regex> StyleRules = new(StringComparer.Ordinal)
    {
        ["font-family"] = new(@"^[A-Za-z0-9 ,'""\-]{1,100}$", RegexOptions.CultureInvariant),
        ["font-size"] = new(@"^\d{1,3}(\.\d{1,2})?(px|pt|em|rem|%)$", RegexOptions.CultureInvariant),
        ["font-weight"] = new(@"^(bold|normal|[1-9]00)$", RegexOptions.CultureInvariant),
        ["font-style"] = new(@"^(italic|normal)$", RegexOptions.CultureInvariant),
        ["text-decoration"] = new(@"^(none|((underline|line-through)( (underline|line-through))?))$", RegexOptions.CultureInvariant),
        ["text-decoration-line"] = new(@"^(none|((underline|line-through)( (underline|line-through))?))$", RegexOptions.CultureInvariant),
        ["color"] = new(@"^(#[0-9a-fA-F]{3,8}|rgba?\(\s*\d{1,3}\s*,\s*\d{1,3}\s*,\s*\d{1,3}\s*(,\s*[0-9.]+\s*)?\)|[a-zA-Z]{3,20})$", RegexOptions.CultureInvariant),
        ["background-color"] = new(@"^(#[0-9a-fA-F]{3,8}|rgba?\(\s*\d{1,3}\s*,\s*\d{1,3}\s*,\s*\d{1,3}\s*(,\s*[0-9.]+\s*)?\)|[a-zA-Z]{3,20})$", RegexOptions.CultureInvariant),
        ["text-align"] = new(@"^(left|right|center|justify)$", RegexOptions.CultureInvariant),
        ["line-height"] = new(@"^(\d(\.\d{1,2})?|\d{1,3}(px|pt|%))$", RegexOptions.CultureInvariant),
        ["margin-left"] = new(@"^\d{1,3}(px|pt|em)$", RegexOptions.CultureInvariant),
        ["padding-left"] = new(@"^\d{1,3}(px|pt|em)$", RegexOptions.CultureInvariant),
        ["text-indent"] = new(@"^\d{1,3}(px|pt|em)$", RegexOptions.CultureInvariant),
        ["vertical-align"] = new(@"^(sub|super|baseline)$", RegexOptions.CultureInvariant),
    };

    /// <summary>Returns the formatted text with everything outside the allowed list taken out. Never throws, never returns null.</summary>
    public static string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return "";
        }

        if (html.Length > MaxHtmlLength * 2)
        {
            html = html[..(MaxHtmlLength * 2)];
        }

        var output = new StringBuilder(html.Length);
        var open = new Stack<string>();
        var position = 0;

        while (position < html.Length)
        {
            var lt = html.IndexOf('<', position);
            if (lt < 0)
            {
                AppendText(output, html.AsSpan(position));
                break;
            }

            AppendText(output, html.AsSpan(position, lt - position));

            // A comment or a declaration (<!-- ... -->, <!doctype>, <?xml ?>) is skipped.
            if (lt + 1 < html.Length && (html[lt + 1] == '!' || html[lt + 1] == '?'))
            {
                var isComment = html.AsSpan(lt).StartsWith("<!--", StringComparison.Ordinal);
                var end = isComment ? html.IndexOf("-->", lt + 4, StringComparison.Ordinal) : html.IndexOf('>', lt + 1);
                position = end < 0 ? html.Length : end + (isComment ? 3 : 1);
                continue;
            }

            var match = TagHerePattern().Match(html, lt);
            if (!match.Success)
            {
                // A "<" that does not start a tag is only text.
                output.Append("&lt;");
                position = lt + 1;
                continue;
            }

            position = lt + match.Length;
            var closing = match.Groups[1].Value.Length > 0;
            var name = match.Groups[2].Value.ToLowerInvariant();

            if (DroppedWithContent.Contains(name))
            {
                if (!closing && !match.Value.EndsWith("/>", StringComparison.Ordinal))
                {
                    var close = html.IndexOf("</" + name, position, StringComparison.OrdinalIgnoreCase);
                    if (close < 0)
                    {
                        position = html.Length;
                    }
                    else
                    {
                        var gt = html.IndexOf('>', close);
                        position = gt < 0 ? html.Length : gt + 1;
                    }
                }

                continue;
            }

            if (!Tags.Contains(name))
            {
                continue;
            }

            if (closing)
            {
                if (VoidTags.Contains(name) || !open.Contains(name))
                {
                    continue;
                }

                while (open.Count > 0)
                {
                    var top = open.Pop();
                    output.Append("</").Append(top).Append('>');
                    if (top == name)
                    {
                        break;
                    }
                }

                continue;
            }

            if (open.Count >= MaxDepth && !VoidTags.Contains(name))
            {
                continue;
            }

            output.Append('<').Append(name);
            AppendAttributes(output, name, match.Groups[3].Value);
            output.Append('>');
            if (!VoidTags.Contains(name))
            {
                open.Push(name);
            }

            if (output.Length > MaxHtmlLength * 2)
            {
                break;
            }
        }

        while (open.Count > 0)
        {
            output.Append("</").Append(open.Pop()).Append('>');
        }

        return output.ToString();
    }

    /// <summary>The text of formatted text without the formatting, one line for each paragraph or list item (for counting, searching and for old screens).</summary>
    public static string ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return "";
        }

        var clean = Sanitize(html);
        var text = new StringBuilder(clean.Length);
        var position = 0;
        foreach (Match match in TagPattern().Matches(clean))
        {
            text.Append(clean, position, match.Index - position);
            position = match.Index + match.Length;
            var name = match.Groups[2].Value.ToLowerInvariant();
            if (name == "br")
            {
                text.Append('\n');
            }
            else if (BlockTags.Contains(name))
            {
                EndLine(text);
            }
            else if ((name is "td" or "th") && match.Groups[1].Value.Length > 0)
            {
                text.Append('\t');
            }
        }

        text.Append(clean, position, clean.Length - position);

        var plain = WebUtility.HtmlDecode(text.ToString()).Replace(' ', ' ').Replace("\r", "");
        plain = Regex.Replace(plain, @"[ \t]+\n", "\n");
        plain = Regex.Replace(plain, @"\n{3,}", "\n\n");
        return plain.Trim();
    }

    private static void EndLine(StringBuilder text)
    {
        if (text.Length > 0 && text[^1] != '\n')
        {
            text.Append('\n');
        }
    }

    /// <summary>Plain text made safe to show as formatted text (used when old work, written before the editor existed, is opened in it).</summary>
    public static string FromPlainText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        var builder = new StringBuilder();
        foreach (var line in text.Replace("\r", "").Split('\n'))
        {
            builder.Append("<p>").Append(string.IsNullOrEmpty(line) ? "<br>" : WebUtility.HtmlEncode(line)).Append("</p>");
        }

        return builder.ToString();
    }

    private static void AppendText(StringBuilder output, ReadOnlySpan<char> text)
    {
        // Keep the character references the browser writes (&amp; &nbsp; &#160;), turn every other "&", "<" and ">" into one.
        var value = text.ToString();
        var position = 0;
        foreach (Match entity in EntityPattern().Matches(value))
        {
            EncodeRaw(output, value.AsSpan(position, entity.Index - position));
            output.Append(entity.Value);
            position = entity.Index + entity.Length;
        }

        EncodeRaw(output, value.AsSpan(position));
    }

    private static void EncodeRaw(StringBuilder output, ReadOnlySpan<char> text)
    {
        foreach (var c in text)
        {
            switch (c)
            {
                case '&': output.Append("&amp;"); break;
                case '<': output.Append("&lt;"); break;
                case '>': output.Append("&gt;"); break;
                case '"': output.Append("&quot;"); break;
                case < ' ' when c is not ('\n' or '\t' or '\r'):
                    break;
                default: output.Append(c); break;
            }
        }
    }

    private static void AppendAttributes(StringBuilder output, string tag, string attributes)
    {
        string? href = null;
        string? style = null;
        string? colspan = null;
        string? rowspan = null;

        foreach (Match attribute in AttributePattern().Matches(attributes))
        {
            var key = attribute.Groups[1].Value.ToLowerInvariant();
            var value = WebUtility.HtmlDecode(attribute.Groups[2].Success ? attribute.Groups[2].Value : attribute.Groups[3].Success ? attribute.Groups[3].Value : attribute.Groups[4].Value);

            switch (key)
            {
                case "style" when StyledTags.Contains(tag):
                    style = CleanStyle(value);
                    break;
                case "href" when tag == "a":
                    href = CleanLink(value);
                    break;
                case "colspan" when tag is "td" or "th":
                    colspan = CleanSpan(value);
                    break;
                case "rowspan" when tag is "td" or "th":
                    rowspan = CleanSpan(value);
                    break;
            }
        }

        if (!string.IsNullOrEmpty(style))
        {
            output.Append(" style=\"").Append(style).Append('"');
        }

        if (tag == "a" && href is not null)
        {
            output.Append(" href=\"").Append(WebUtility.HtmlEncode(href)).Append("\" target=\"_blank\" rel=\"noopener noreferrer\"");
        }

        if (colspan is not null)
        {
            output.Append(" colspan=\"").Append(colspan).Append('"');
        }

        if (rowspan is not null)
        {
            output.Append(" rowspan=\"").Append(rowspan).Append('"');
        }
    }

    private static string CleanStyle(string style)
    {
        var kept = new List<string>();
        foreach (var rule in style.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var colon = rule.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            var property = rule[..colon].Trim().ToLowerInvariant();
            var value = rule[(colon + 1)..].Trim().Replace("\"", "'");
            if (StyleRules.TryGetValue(property, out var allowed) && allowed.IsMatch(value) && !value.Contains("javascript", StringComparison.OrdinalIgnoreCase))
            {
                kept.Add($"{property}: {value}");
            }
        }

        return string.Join("; ", kept);
    }

    private static string? CleanLink(string value)
    {
        value = value.Trim();
        return value.Length <= 500 && (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            && Uri.TryCreate(value, UriKind.Absolute, out _)
            ? value
            : null;
    }

    private static string? CleanSpan(string value) =>
        int.TryParse(value, out var number) && number is >= 1 and <= 20 ? number.ToString() : null;
}
