using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AntiCheatScanner.Models;
using AntiCheatScanner.Utils;

namespace AntiCheatScanner.Core;

public static class Scanner
{
    public static ScanReport Run(string path, ScanTarget target = ScanTarget.All)
    {
        return Run(new ScanOptions
        {
            RootPath = path,
            Target = target
        });
    }

    public static ScanReport Run(IEnumerable<string> paths, ScanTarget target = ScanTarget.All)
    {
        return Run(new ScanOptions
        {
            RootPaths = NormalizeRootPaths(paths),
            Target = target
        });
    }

    public static ScanReport Run(ScanOptions options)
    {
        var rootPaths = ResolveRootPaths(options);
        var progress = options.Progress;
        var startedAt = DateTimeOffset.UtcNow;
        var files = DiscoverFiles(rootPaths, options.Target);
        var detectionEngine = new DetectionEngine(CheatLoader.Load());
        var detections = new ConcurrentBag<Detection>();
        var failures = new ConcurrentBag<ScanFailure>();
        var degreeOfParallelism = Math.Max(1, Math.Min(options.MaxDegreeOfParallelism, Environment.ProcessorCount));
        var totalFiles = files.Count;
        var processedFiles = 0;

        progress?.Report(new ScanProgressUpdate
        {
            Stage = "Preparing",
            CurrentItem = totalFiles == 0
                ? "No matching .jar or .log files were found."
                : $"Queued {totalFiles} file(s) for scanning.",
            ProcessedFiles = 0,
            TotalFiles = totalFiles
        });

        Parallel.ForEach(
            files,
            new ParallelOptions { MaxDegreeOfParallelism = degreeOfParallelism },
            file =>
            {
                try
                {
                    var fileDetections = file.Kind switch
                    {
                        FileKind.Log => LogScanner.Scan(file, detectionEngine),
                        FileKind.Jar => JarScanner.Scan(file, detectionEngine),
                        _ => []
                    };

                    foreach (var detection in fileDetections)
                    {
                        detections.Add(detection);
                    }
                }
                catch (IOException ex)
                {
                    failures.Add(CreateFailure(file.Path, "scan", ex));
                }
                catch (UnauthorizedAccessException ex)
                {
                    failures.Add(CreateFailure(file.Path, "scan", ex));
                }
                catch (InvalidDataException ex)
                {
                    failures.Add(CreateFailure(file.Path, "scan", ex));
                }
                finally
                {
                    var completed = Interlocked.Increment(ref processedFiles);
                    progress?.Report(new ScanProgressUpdate
                    {
                        Stage = "Scanning",
                        CurrentItem = file.Path,
                        ProcessedFiles = completed,
                        TotalFiles = totalFiles
                    });
                }
            });

        var finalizedDetections = Deduplicate(detections);
        var finalizedFailures = failures
            .GroupBy(failure => $"{failure.FilePath}|{failure.Message}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(failure => failure.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var completedAt = DateTimeOffset.UtcNow;

        return new ScanReport
        {
            RootPath = DescribeRootPaths(rootPaths),
            StartedAtUtc = startedAt,
            CompletedAtUtc = completedAt,
            Files = files,
            Detections = finalizedDetections,
            Failures = finalizedFailures,
            Summary = BuildSummary(files, finalizedDetections, finalizedFailures)
        };
    }

    public static List<DiscoveredFile> DiscoverFiles(string path, ScanTarget target = ScanTarget.All)
    {
        return DiscoverFiles([path], target);
    }

    public static List<DiscoveredFile> DiscoverFiles(IEnumerable<string> paths, ScanTarget target = ScanTarget.All)
    {
        var rootPaths = NormalizeRootPaths(paths);
        var files = new List<DiscoveredFile>();

        foreach (var rootPath in rootPaths)
        {
            if (!Directory.Exists(rootPath))
            {
                continue;
            }

            foreach (var filePath in EnumerateFilesSafe(rootPath))
            {
                if (!TryGetFileKind(filePath, out var fileKind) || !MatchesTarget(fileKind, target))
                {
                    continue;
                }

                try
                {
                    var fileInfo = new FileInfo(filePath);

                    files.Add(new DiscoveredFile
                    {
                        Path = filePath,
                        Kind = fileKind,
                        Length = fileInfo.Exists ? fileInfo.Length : 0,
                        LastWriteTime = fileInfo.Exists
                            ? new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero)
                            : DateTimeOffset.MinValue
                    });
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        return files
            .OrderBy(file => file.Kind)
            .ThenBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<string> GetReadyDriveRoots()
    {
        return DriveInfo.GetDrives()
            .Where(IsReadyDrive)
            .Select(drive => drive.RootDirectory.FullName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string DescribeRootPaths(IEnumerable<string> paths)
    {
        var rootPaths = NormalizeRootPaths(paths);

        return rootPaths.Count switch
        {
            0 => "all ready drives",
            1 => rootPaths[0],
            _ => string.Join(", ", rootPaths)
        };
    }

    private static IEnumerable<string> EnumerateFilesSafe(string rootPath)
    {
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(rootPath);

        while (pendingDirectories.Count > 0)
        {
            var current = pendingDirectories.Pop();

            IEnumerable<string> childDirectories = [];
            IEnumerable<string> files = [];

            try
            {
                childDirectories = Directory.EnumerateDirectories(current);
                files = Directory.EnumerateFiles(current);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            foreach (var childDirectory in childDirectories)
            {
                pendingDirectories.Push(childDirectory);
            }

            foreach (var file in files)
            {
                yield return file;
            }
        }
    }

    private static bool TryGetFileKind(string filePath, out FileKind fileKind)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        switch (extension)
        {
            case ".log":
                fileKind = FileKind.Log;
                return true;
            case ".jar":
                fileKind = FileKind.Jar;
                return true;
            default:
                fileKind = default;
                return false;
        }
    }

    private static bool MatchesTarget(FileKind fileKind, ScanTarget target)
    {
        return target switch
        {
            ScanTarget.Logs => fileKind == FileKind.Log,
            ScanTarget.Jars => fileKind == FileKind.Jar,
            _ => true
        };
    }

    private static List<Detection> Deduplicate(IEnumerable<Detection> detections)
    {
        return detections
            .GroupBy(detection => $"{detection.FilePath}|{detection.Location}|{detection.Keyword}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.Score).First())
            .OrderByDescending(detection => detection.Score)
            .ThenBy(detection => detection.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(detection => detection.Keyword, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static ScanFailure CreateFailure(string filePath, string operation, Exception exception)
    {
        return new ScanFailure
        {
            FilePath = filePath,
            Operation = operation,
            Message = exception.Message
        };
    }

    private static ScanSummary BuildSummary(
        IReadOnlyCollection<DiscoveredFile> files,
        IReadOnlyCollection<Detection> detections,
        IReadOnlyCollection<ScanFailure> failures)
    {
        return new ScanSummary
        {
            TotalFilesScanned = files.Count,
            TotalDetections = detections.Count,
            FailedFiles = failures.Count,
            LogFiles = files.Count(file => file.Kind == FileKind.Log),
            JarFiles = files.Count(file => file.Kind == FileKind.Jar),
            CriticalDetections = detections.Count(detection => detection.Severity == DetectionSeverity.Critical),
            HighDetections = detections.Count(detection => detection.Severity == DetectionSeverity.High)
        };
    }

    private static IReadOnlyList<string> ResolveRootPaths(ScanOptions options)
    {
        var rootPaths = NormalizeRootPaths(options.RootPaths);

        if (rootPaths.Count > 0)
        {
            return rootPaths;
        }

        var rootPath = NormalizeRootPath(options.RootPath);
        if (!string.IsNullOrWhiteSpace(rootPath))
        {
            return [rootPath];
        }

        return GetReadyDriveRoots();
    }

    private static List<string> NormalizeRootPaths(IEnumerable<string>? paths)
    {
        return paths is null
            ? []
            : paths
                .Select(NormalizeRootPath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
    }

    private static string NormalizeRootPath(string path)
    {
        return Environment.ExpandEnvironmentVariables((path ?? string.Empty).Trim().Trim('"'));
    }

    private static bool IsReadyDrive(DriveInfo drive)
    {
        try
        {
            return drive.IsReady;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
