using System;
using System.IO;
using System.Text;
using AntiCheatScanner.Models;

namespace AntiCheatScanner.Utils;

public static class Logger
{
    public static string SaveReport(ScanReport report)
    {
        var reportsDirectory = Path.Combine(AppContext.BaseDirectory, "Reports");
        Directory.CreateDirectory(reportsDirectory);

        var outputPath = Path.Combine(reportsDirectory, $"scan-report-{DateTime.Now:yyyyMMdd-HHmmss}.txt");

        using var writer = new StreamWriter(outputPath, append: false, Encoding.UTF8);

        writer.WriteLine("AntiCheat Scanner Report");
        writer.WriteLine(new string('=', 72));
        writer.WriteLine($"Root path: {report.RootPath}");
        writer.WriteLine($"Started:   {report.StartedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
        writer.WriteLine($"Finished:  {report.CompletedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
        writer.WriteLine($"Duration:  {report.Duration.TotalSeconds:0.00}s");
        writer.WriteLine();

        writer.WriteLine("Summary");
        writer.WriteLine(new string('-', 72));
        writer.WriteLine($"Files scanned: {report.Summary.TotalFilesScanned}");
        writer.WriteLine($"Log files:     {report.Summary.LogFiles}");
        writer.WriteLine($"JAR files:     {report.Summary.JarFiles}");
        writer.WriteLine($"Detections:    {report.Summary.TotalDetections}");
        writer.WriteLine($"Critical:      {report.Summary.CriticalDetections}");
        writer.WriteLine($"High:          {report.Summary.HighDetections}");
        writer.WriteLine($"Failures:      {report.Summary.FailedFiles}");
        writer.WriteLine();

        writer.WriteLine("Failures");
        writer.WriteLine(new string('-', 72));

        if (report.Failures.Count == 0)
        {
            writer.WriteLine("None");
        }
        else
        {
            foreach (var failure in report.Failures)
            {
                writer.WriteLine($"[{failure.Operation}] {failure.FilePath}");
                writer.WriteLine($"  {failure.Message}");
            }
        }

        writer.WriteLine();
        writer.WriteLine("Detections");
        writer.WriteLine(new string('-', 72));

        if (report.Detections.Count == 0)
        {
            writer.WriteLine("No detections found.");
        }
        else
        {
            foreach (var detection in report.Detections)
            {
                writer.WriteLine($"[{detection.SeverityLabel}] score={detection.Score} kind={detection.KindLabel} keyword={detection.Keyword}");
                writer.WriteLine($"  file: {detection.FilePath}");
                writer.WriteLine($"  location: {detection.Location}");
                writer.WriteLine($"  evidence: {detection.Evidence}");
            }
        }

        return outputPath;
    }

    public static string SaveCrash(Exception exception)
    {
        var logsDirectory = Path.Combine(AppContext.BaseDirectory, "Logs");
        Directory.CreateDirectory(logsDirectory);

        var outputPath = Path.Combine(logsDirectory, $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        File.WriteAllText(outputPath, exception.ToString(), Encoding.UTF8);

        return outputPath;
    }
}
