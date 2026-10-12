namespace SEAITT_LMS.Components.Shared;

/// <summary>A size in bytes as people read it: 12 KB, 3.4 MB.</summary>
public static class FileSize
{
    public static string Text(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        var kilobytes = bytes / 1024.0;
        if (kilobytes < 1024)
        {
            return $"{kilobytes:0.#} KB";
        }

        var megabytes = kilobytes / 1024;
        return megabytes < 1024 ? $"{megabytes:0.#} MB" : $"{megabytes / 1024:0.##} GB";
    }
}
