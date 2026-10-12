using System.Text;

namespace SEAITT_LMS.Services;

/// <summary>
/// How a file is handed to the browser. A PDF, a picture, a text file, a sound or a video opens in a tab of the browser (the browser's own viewer,
/// with its download and print buttons); everything else is saved as a file. The type a file is served as is decided here from its name and its
/// first bytes, never from what the uploader said, and a file whose first bytes do not fit its name is only ever saved, not shown. Web pages
/// and SVG pictures are never shown (they could run as part of this site), and every answer says <c>nosniff</c>.
/// </summary>
public static class FilePreview
{
    private static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".txt"] = "text/plain; charset=utf-8",
        [".csv"] = "text/plain; charset=utf-8",
        [".md"] = "text/plain; charset=utf-8",
        [".log"] = "text/plain; charset=utf-8",
        [".mp3"] = "audio/mpeg",
        [".wav"] = "audio/wav",
        [".ogg"] = "audio/ogg",
        [".mp4"] = "video/mp4",
        [".webm"] = "video/webm",
    };

    /// <summary>Whether a file with this name is of a kind the browser can show.</summary>
    public static bool CanPreview(string fileName) => Types.ContainsKey(Path.GetExtension(fileName));

    /// <summary>The type to serve the file as, and whether the browser may show it (otherwise it is saved).</summary>
    public static (string ContentType, bool Inline) Decide(string fileName, byte[] data)
    {
        if (!Types.TryGetValue(Path.GetExtension(fileName), out var type) || !LooksLike(type, data))
        {
            return ("application/octet-stream", false);
        }

        return (type, true);
    }

    private static bool LooksLike(string type, byte[] d) => type switch
    {
        "application/pdf" => Starts(d, "%PDF-"),
        "image/png" => d.Length > 8 && d[0] == 0x89 && d[1] == 0x50 && d[2] == 0x4E && d[3] == 0x47,
        "image/jpeg" => d.Length > 3 && d[0] == 0xFF && d[1] == 0xD8 && d[2] == 0xFF,
        "image/gif" => Starts(d, "GIF8"),
        "image/webp" => Starts(d, "RIFF") && d.Length > 12 && Encoding.ASCII.GetString(d, 8, 4) == "WEBP",
        "audio/wav" => Starts(d, "RIFF") && d.Length > 12 && Encoding.ASCII.GetString(d, 8, 4) == "WAVE",
        "audio/ogg" => Starts(d, "OggS"),
        "audio/mpeg" => Starts(d, "ID3") || (d.Length > 2 && d[0] == 0xFF && (d[1] & 0xE0) == 0xE0),
        "video/mp4" => d.Length > 12 && Encoding.ASCII.GetString(d, 4, 4) == "ftyp",
        "video/webm" => d.Length > 4 && d[0] == 0x1A && d[1] == 0x45 && d[2] == 0xDF && d[3] == 0xA3,
        _ => LooksLikeText(d),
    };

    private static bool Starts(byte[] d, string text) => d.Length >= text.Length && Encoding.ASCII.GetString(d, 0, text.Length) == text;

    /// <summary>Text: no zero bytes in the first part, and it is not a web page.</summary>
    private static bool LooksLikeText(byte[] d)
    {
        var length = Math.Min(d.Length, 4096);
        for (var i = 0; i < length; i++)
        {
            if (d[i] == 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The <c>Content-Disposition</c> header: "inline" to show the file in the tab, "attachment" to save it, with the name of the file.</summary>
    public static string Disposition(bool inline, string fileName) =>
        new Microsoft.Net.Http.Headers.ContentDispositionHeaderValue(inline ? "inline" : "attachment") { FileNameStar = fileName, FileName = AsciiName(fileName) }.ToString();

    private static string AsciiName(string name)
    {
        var ascii = new string(name.Select(c => c is >= ' ' and <= '~' and not '"' and not '\\' ? c : '_').ToArray());
        return ascii.Length == 0 ? "file" : ascii;
    }
}
