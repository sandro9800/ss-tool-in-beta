using System.Collections.Generic;
using System.IO;
using System.Text;
using AntiCheatScanner.Models;

namespace AntiCheatScanner.Core;

public static class LogScanner
{
    public static List<Detection> Scan(DiscoveredFile file, DetectionEngine detectionEngine)
    {
        var results = new List<Detection>();

        using var stream = new FileStream(
            file.Path,
            new FileStreamOptions
            {
                Access = FileAccess.Read,
                Mode = FileMode.Open,
                Share = FileShare.ReadWrite | FileShare.Delete,
                Options = FileOptions.SequentialScan,
                BufferSize = 65536
            });
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: false);

        string? line;
        var lineNumber = 0;

        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            results.AddRange(detectionEngine.AnalyzeText(file.Path, FileKind.Log, $"log:line {lineNumber}", line));
        }

        return results;
    }
}
