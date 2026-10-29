using Microsoft.JSInterop;

namespace SEAITT_LMS.Services;

/// <summary>
/// Gives a file made in memory to the browser, which saves it ("Export to Excel", the templates). A live page cannot answer with a file, so the bytes are
/// sent to a small script (<c>seaitt.downloadFile</c> in wwwroot/app.js) that makes the browser save them under the given name.
/// </summary>
public sealed class FileDownloads(IJSRuntime js)
{
    public const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public async Task SaveAsync(string fileName, byte[] data, string contentType = XlsxType)
    {
        using var stream = new MemoryStream(data);
        using var reference = new DotNetStreamReference(stream);
        await js.InvokeVoidAsync("seaitt.downloadFile", CleanName(fileName), contentType, reference);
    }

    /// <summary>A file name the computer accepts: no folders or odd characters, with the extension kept.</summary>
    public static string CleanName(string fileName)
    {
        var clean = new string(fileName.Select(ch => char.IsLetterOrDigit(ch) || ch is '.' or '-' or '_' or ' ' or '(' or ')' ? ch : '_').ToArray()).Trim();
        return clean.Length == 0 ? "file" : clean;
    }
}
