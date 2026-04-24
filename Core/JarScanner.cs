using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using AntiCheatScanner.Models;

namespace AntiCheatScanner.Core;

public static class JarScanner
{
    private static readonly HashSet<string> KnownBinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".class", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".ico", ".dll", ".exe", ".so", ".dylib",
        ".bin", ".dat", ".mp3", ".ogg", ".wav", ".mp4", ".jar", ".zip", ".7z", ".rar"
    };

    private static readonly HashSet<string> KnownTextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".cfg", ".config", ".json", ".xml", ".yml", ".yaml", ".properties", ".toml",
        ".ini", ".csv", ".tsv", ".mf", ".md", ".html", ".js", ".ts", ".java", ".kt", ".scala",
        ".groovy", ".mcmeta", ".accesswidener"
    };

    public static List<Detection> Scan(DiscoveredFile file, DetectionEngine detectionEngine)
    {
        var results = new List<Detection>();

        using var archiveStream = new FileStream(
            file.Path,
            new FileStreamOptions
            {
                Access = FileAccess.Read,
                Mode = FileMode.Open,
                Share = FileShare.ReadWrite | FileShare.Delete,
                Options = FileOptions.SequentialScan,
                BufferSize = 65536
            });
        using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: false);

        foreach (var entry in archive.Entries)
        {
            if (!ShouldScanEntry(entry))
            {
                continue;
            }

            try
            {
                results.AddRange(detectionEngine.AnalyzeText(file.Path, FileKind.Jar, $"jar:{entry.FullName}", entry.FullName));
                results.AddRange(ScanEntryText(file.Path, entry, detectionEngine));
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (InvalidDataException)
            {
            }
        }

        return results;
    }

    private static IEnumerable<Detection> ScanEntryText(string filePath, ZipArchiveEntry entry, DetectionEngine detectionEngine)
    {
        if (LooksBinary(entry))
        {
            return [];
        }

        var results = new List<Detection>();

        using var textStream = entry.Open();
        using var reader = new StreamReader(textStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: false);

        string? line;
        var lineNumber = 0;

        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            results.AddRange(detectionEngine.AnalyzeText(filePath, FileKind.Jar, $"jar:{entry.FullName}:line {lineNumber}", line));
        }

        return results;
    }

    private static bool ShouldScanEntry(ZipArchiveEntry entry)
    {
        if (entry.Length == 0 || entry.FullName.EndsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        var extension = Path.GetExtension(entry.Name);

        if (KnownBinaryExtensions.Contains(extension))
        {
            return false;
        }

        if (KnownTextExtensions.Contains(extension))
        {
            return true;
        }

        return entry.Length <= 256 * 1024;
    }

    private static bool LooksBinary(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        Span<byte> sample = stackalloc byte[256];
        var read = stream.Read(sample);

        if (read == 0)
        {
            return false;
        }

        var controlBytes = 0;

        for (var index = 0; index < read; index++)
        {
            var current = sample[index];

            if (current == 0)
            {
                return true;
            }

            if (current < 8 || (current > 13 && current < 32))
            {
                controlBytes++;
            }
        }

        return controlBytes > read / 8;
    }
}
