using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using AntiCheatScanner.Models;

namespace AntiCheatScanner.Core;

public static class ZipProcessor
{
    private const int MaxNestedDepth = 3;
    private const int MaxEntriesPerArchive = 50000;
    private const long MaxTotalExtractedBytes = 2L * 1024 * 1024 * 1024; // 2 GB guardrail.
    private const long MaxTextFileReadBytes = 2L * 1024 * 1024; // 2 MB per file for analysis.

    public static string ExtractZip(string zipPath)
    {
        if (string.IsNullOrWhiteSpace(zipPath))
        {
            throw new ArgumentException("ZIP path cannot be empty.", nameof(zipPath));
        }

        if (!File.Exists(zipPath))
        {
            throw new FileNotFoundException("ZIP archive not found.", zipPath);
        }

        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            long extractedBytes = 0;
            ExtractArchiveRecursive(zipPath, tempDir, 0, ref extractedBytes);

            return tempDir;
        }
        catch
        {
            Directory.Delete(tempDir, recursive: true);
            throw;
        }
    }

    public static string extract_zip(string zipPath) => ExtractZip(zipPath);

    private static void ExtractArchiveRecursive(string archivePath, string extractionRoot, int depth, ref long extractedBytes)
    {
        if (depth > MaxNestedDepth)
        {
            return;
        }

        using var archive = ZipFile.OpenRead(archivePath);

        if (archive.Entries.Count > MaxEntriesPerArchive)
        {
            throw new InvalidOperationException($"Archive exceeds safe entry limit ({MaxEntriesPerArchive}).");
        }

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.FullName))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(Path.Combine(extractionRoot, entry.FullName));
            if (!fullPath.StartsWith(extractionRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Archive entry attempted path traversal outside extraction root.");
            }

            if (entry.FullName.EndsWith("/", StringComparison.Ordinal) || string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(fullPath);
                continue;
            }

            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            extractedBytes += entry.Length;
            if (extractedBytes > MaxTotalExtractedBytes)
            {
                throw new InvalidOperationException("Archive exceeds total extracted size guardrail.");
            }

            using var source = entry.Open();
            using var destination = new FileStream(
                fullPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
            source.CopyTo(destination);

            if (Path.GetExtension(fullPath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                var nestedTarget = Path.Combine(
                    Path.GetDirectoryName(fullPath) ?? extractionRoot,
                    $"{Path.GetFileNameWithoutExtension(fullPath)}_nested");
                Directory.CreateDirectory(nestedTarget);
                ExtractArchiveRecursive(fullPath, nestedTarget, depth + 1, ref extractedBytes);
            }
        }
    }

    public static List<ExtractedFile> LoadFiles(string extractedPath)
    {
        if (!Directory.Exists(extractedPath))
        {
            throw new DirectoryNotFoundException($"Extracted path not found: {extractedPath}");
        }

        var files = new List<ExtractedFile>();
        var allFiles = Directory.GetFiles(extractedPath, "*.*", SearchOption.AllDirectories);

        foreach (var file in allFiles)
        {
            var extension = Path.GetExtension(file).ToLowerInvariant();
            var type = GetFileType(extension);

            if (type == ExtractedFileType.Other)
            {
                continue;
            }

            var fileInfo = new FileInfo(file);
            if (!fileInfo.Exists || fileInfo.Length == 0 || fileInfo.Length > MaxTextFileReadBytes)
            {
                continue;
            }

            try
            {
                if (LooksBinary(file))
                {
                    continue;
                }

                var content = File.ReadAllText(file, Encoding.UTF8);
                files.Add(new ExtractedFile
                {
                    Path = Path.GetRelativePath(extractedPath, file),
                    Content = content,
                    Type = type,
                    Extension = extension
                });
            }
            catch
            {
                // Skip files that can't be read as text
                continue;
            }
        }

        return files;
    }

    public static List<ExtractedFile> load_files(string extractedPath) => LoadFiles(extractedPath);

    private static ExtractedFileType GetFileType(string extension)
    {
        return extension switch
        {
            ".java" or ".cs" or ".cpp" or ".c" or ".py" or ".js" or ".ts" or ".php" or ".rb" or ".go" or ".rs" => ExtractedFileType.Code,
            ".log" or ".txt" => ExtractedFileType.Log,
            ".json" or ".xml" or ".yml" or ".yaml" or ".ini" or ".cfg" or ".config" => ExtractedFileType.Config,
            _ => ExtractedFileType.Other
        };
    }

    private static bool LooksBinary(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        Span<byte> sample = stackalloc byte[256];
        var read = stream.Read(sample);

        if (read <= 0)
        {
            return false;
        }

        var controlBytes = 0;
        for (var i = 0; i < read; i++)
        {
            var current = sample[i];
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
