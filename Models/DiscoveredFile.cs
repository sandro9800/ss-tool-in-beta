using System;
using System.Globalization;

namespace AntiCheatScanner.Models;

public sealed class DiscoveredFile
{
    public string Path { get; init; } = string.Empty;
    public FileKind Kind { get; init; }
    public long Length { get; init; }
    public DateTimeOffset LastWriteTime { get; init; }

    public string Name => System.IO.Path.GetFileName(Path);
    public string DirectoryPath => System.IO.Path.GetDirectoryName(Path) ?? string.Empty;
    public string KindLabel => Kind == FileKind.Jar ? "JAR" : "LOG";
    public string SizeLabel => FormatSize(Length);
    public string ModifiedLabel => LastWriteTime.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    private static string FormatSize(long length)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double size = length;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{size:0} {units[unitIndex]}"
            : $"{size:0.0} {units[unitIndex]}";
    }
}
