using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AntiCheatScanner.Core;
using AntiCheatScanner.Models;
using AntiCheatScanner.Utils;

namespace AntiCheatScanner;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<DiscoveredFile> discoveredFiles = [];
    private readonly ObservableCollection<Detection> detections = [];
    private readonly ObservableCollection<ScanFailure> failures = [];
    private readonly ObservableCollection<GuaranteedFile> guaranteedFiles = [];
    private readonly ObservableCollection<string> activityItems = [];
    private IReadOnlyList<string> readyRoots = [];
    private bool isRefreshing;
    private bool isScanning;

    public MainWindow()
    {
        InitializeComponent();

        DiscoveriesList.ItemsSource = discoveredFiles;
        ResultGrid.ItemsSource = detections;
        FailuresList.ItemsSource = failures;
        GuaranteedFilesList.ItemsSource = guaranteedFiles;
        ActivityList.ItemsSource = activityItems;

        AppendActivity("Application ready.");
        RefreshDriveState();

        StatusText.Text = readyRoots.Count == 0 ? "No drives found" : "Ready";
        TargetInfoText.Text = readyRoots.Count == 0
            ? "No ready drives are available to scan."
            : "Scan buttons are ready. Use Refresh only if you want to pre-list every .jar and .log file.";
        ResetScanProgress();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await RefreshDiscoveredFilesAsync();
    }

    private async void ScanAll_Click(object sender, RoutedEventArgs e)
    {
        await RunScanAsync(ScanTarget.All);
    }

    private async void ScanLogs_Click(object sender, RoutedEventArgs e)
    {
        await RunScanAsync(ScanTarget.Logs);
    }

    private async void ScanJars_Click(object sender, RoutedEventArgs e)
    {
        await RunScanAsync(ScanTarget.Jars);
    }

    private async Task RefreshDiscoveredFilesAsync()
    {
        if (isRefreshing || isScanning)
        {
            return;
        }

        RefreshDriveState();

        if (readyRoots.Count == 0)
        {
            discoveredFiles.Clear();
            UpdateMetrics(0, 0, 0, detections.Count, failures.Count);
            EmptyDiscoveriesText.Visibility = Visibility.Visible;
            StatusText.Text = "No drives found";
            TargetInfoText.Text = "No ready drives are available to scan.";
            UpdateCommandAvailability();
            return;
        }

        isRefreshing = true;
        UpdateCommandAvailability();
        StatusText.Text = "Refreshing";
        TargetInfoText.Text = "Finding .jar and .log files across all ready drives.";
        AppendActivity($"Refreshing targets across {Scanner.DescribeRootPaths(readyRoots)}");

        try
        {
            var files = await Task.Run(() => Scanner.DiscoverFiles(readyRoots));

            discoveredFiles.Clear();

            foreach (var file in files)
            {
                discoveredFiles.Add(file);
            }

            UpdateMetrics(
                files.Count,
                files.Count(file => file.Kind == FileKind.Log),
                files.Count(file => file.Kind == FileKind.Jar),
                detections.Count,
                failures.Count);

            EmptyDiscoveriesText.Visibility = files.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            StatusText.Text = files.Count == 0 ? "Ready" : "Targets ready";
            TargetInfoText.Text = files.Count == 0
                ? $"No .jar or .log files found across {Scanner.DescribeRootPaths(readyRoots)}."
                : $"Found {files.Count} scan target(s) across {Scanner.DescribeRootPaths(readyRoots)}.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Refresh failed";
            TargetInfoText.Text = "The drive scan could not finish.";
            AppendActivity($"Refresh failed: {ex.Message}");
            MessageBox.Show($"Drive discovery failed: {ex.Message}", "Discovery Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            isRefreshing = false;
            UpdateCommandAvailability();
        }
    }

    private async Task RunScanAsync(ScanTarget target)
    {
        if (isScanning)
        {
            return;
        }

        RefreshDriveState();

        if (readyRoots.Count == 0)
        {
            MessageBox.Show("No ready drives are available to scan.", "No Drives", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        isScanning = true;
        UpdateCommandAvailability();
        StatusText.Text = "Scanning";
        TargetInfoText.Text = GetModeDescription(target);
        AppendActivity($"{GetModeLabel(target)} started across {Scanner.DescribeRootPaths(readyRoots)}");
        ScanProgressText.Text = "Progress: preparing scan...";
        ScanProgressBar.Value = 0;

        var progress = new Progress<ScanProgressUpdate>(update =>
        {
            var currentItem = string.IsNullOrWhiteSpace(update.CurrentItem)
                ? update.Stage
                : update.CurrentItem;

            ScanProgressBar.Value = Math.Max(0, Math.Min(100, update.Percentage));
            ScanProgressText.Text = update.TotalFiles <= 0
                ? $"Progress: {update.Stage}"
                : $"Progress: {update.ProcessedFiles}/{update.TotalFiles} files, now on {currentItem}";
        });

        try
        {
            var report = await Task.Run(() => Scanner.Run(new ScanOptions
            {
                RootPaths = readyRoots,
                Target = target,
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1),
                Progress = progress
            }));

            var reportPath = Logger.SaveReport(report);
            ApplyReport(report, reportPath);
            AppendActivity($"{GetModeLabel(target)} completed in {report.Duration.TotalSeconds:0.00}s");
            ScanProgressBar.Value = 100;
            ScanProgressText.Text = $"Progress: finished {report.Summary.TotalFilesScanned} file(s)";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Scan failed";
            TargetInfoText.Text = "The scan stopped because of an unexpected error.";
            AppendActivity($"Scan failed: {ex.Message}");
            ScanProgressText.Text = "Progress: scan failed";
            MessageBox.Show($"Scan failed: {ex.Message}", "Scan Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            isScanning = false;
            UpdateCommandAvailability();
            if (StatusText.Text != "Scan failed")
            {
                ScanProgressBar.Value = Math.Max(ScanProgressBar.Value, 100);
            }
        }
    }

    private void ApplyReport(ScanReport report, string reportPath)
    {
        discoveredFiles.Clear();
        detections.Clear();
        failures.Clear();
        guaranteedFiles.Clear();

        foreach (var file in report.Files)
        {
            discoveredFiles.Add(file);
        }

        foreach (var detection in report.Detections)
        {
            detections.Add(detection);
        }

        foreach (var failure in report.Failures)
        {
            failures.Add(failure);
        }

        foreach (var guaranteedFile in BuildGuaranteedFiles(report.Detections))
        {
            guaranteedFiles.Add(guaranteedFile);
        }

        UpdateMetrics(
            report.Summary.TotalFilesScanned,
            report.Summary.LogFiles,
            report.Summary.JarFiles,
            report.Summary.TotalDetections,
            report.Summary.FailedFiles);

        EmptyDiscoveriesText.Visibility = discoveredFiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyResultsText.Visibility = detections.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyFailuresText.Visibility = failures.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyGuaranteedText.Visibility = guaranteedFiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        StatusText.Text = report.Detections.Count == 0 ? "Scan complete" : "Detections found";
        TargetInfoText.Text = $"{report.Summary.TotalFilesScanned} file(s) scanned across {report.RootPath} in {report.Duration.TotalSeconds:0.00}s. Critical: {report.Summary.CriticalDetections}.";
        LastRunText.Text = $"Last scan: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        ReportPathText.Text = $"Report: {reportPath}";
    }

    private void UpdateCommandAvailability()
    {
        var hasDrives = readyRoots.Count > 0;

        RefreshButton.IsEnabled = !isRefreshing && !isScanning && hasDrives;
        ScanAllButton.IsEnabled = !isRefreshing && !isScanning && hasDrives;
        ScanLogsButton.IsEnabled = !isRefreshing && !isScanning && hasDrives;
        ScanJarsButton.IsEnabled = !isRefreshing && !isScanning && hasDrives;
    }

    private void UpdateMetrics(int totalFiles, int logFiles, int jarFiles, int detectionCount, int failureCount)
    {
        TotalFilesText.Text = totalFiles.ToString();
        LogFilesText.Text = logFiles.ToString();
        JarFilesText.Text = jarFiles.ToString();
        DetectionCountText.Text = detectionCount.ToString();
        FailureCountText.Text = failureCount.ToString();

        EmptyResultsText.Visibility = detectionCount == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyFailuresText.Visibility = failureCount == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyActivityText.Visibility = activityItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AppendActivity(string message)
    {
        activityItems.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");

        while (activityItems.Count > 250)
        {
            activityItems.RemoveAt(activityItems.Count - 1);
        }

        EmptyActivityText.Visibility = activityItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshDriveState()
    {
        readyRoots = Scanner.GetReadyDriveRoots();
        DrivesText.Text = readyRoots.Count == 0
            ? "Drives: none ready"
            : $"Drives: {Scanner.DescribeRootPaths(readyRoots)}";
        UpdateCommandAvailability();
    }

    private void ResetScanProgress()
    {
        ScanProgressBar.Value = 0;
        ScanProgressText.Text = "Progress: idle";
    }

    private static string GetModeDescription(ScanTarget target)
    {
        return target switch
        {
            ScanTarget.Logs => "Scanning .log files across every ready drive.",
            ScanTarget.Jars => "Scanning .jar files across every ready drive.",
            _ => "Scanning every discovered .jar and .log file across every ready drive."
        };
    }

    private static string GetModeLabel(ScanTarget target)
    {
        return target switch
        {
            ScanTarget.Logs => "Log scan",
            ScanTarget.Jars => "JAR scan",
            _ => "Full scan"
        };
    }

    private static IReadOnlyList<GuaranteedFile> BuildGuaranteedFiles(IEnumerable<Detection> detections)
    {
        return detections
            .Where(detection => detection.Severity == DetectionSeverity.Critical)
            .GroupBy(detection => detection.FilePath, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var orderedGroup = group
                    .OrderByDescending(item => item.Score)
                    .ThenBy(item => item.Keyword, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new GuaranteedFile
                {
                    FilePath = group.Key,
                    KindLabel = orderedGroup[0].KindLabel,
                    HighestScore = orderedGroup[0].Score,
                    KeywordSummary = string.Join(", ", orderedGroup.Select(item => item.Keyword).Distinct(StringComparer.OrdinalIgnoreCase).Take(4))
                };
            })
            .OrderByDescending(file => file.HighestScore)
            .ThenBy(file => file.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // private async void AnalyzeZip_Click(object sender, RoutedEventArgs e)
    // {
    //     var openFileDialog = new Microsoft.Win32.OpenFileDialog
    //     {
    //         Title = "Select ZIP Archive to Analyze",
    //         Filter = "ZIP files (*.zip)|*.zip|All files (*.*)|*.*",
    //         CheckFileExists = true
    //     };

    //     if (openFileDialog.ShowDialog() == true)
    //     {
    //         var zipPath = openFileDialog.FileName;
    //         ZipAnalysisText.Text = $"Analyzing {System.IO.Path.GetFileName(zipPath)}...";

    //         try
    //         {
    //             var report = await Task.Run(() => AntiCheatScanner.Core.ZipAnalyzer.AnalyzeZip(zipPath));

    //             // Display results
    //             var resultMessage = $"Analysis Complete\n\nArchive Summary: {report.ArchiveSummary}\n\nOverall Risk: {report.OverallRisk}\n\nFindings: {report.SuspiciousFindings.Count}";

    //             if (report.SuspiciousFindings.Count > 0)
    //             {
    //                 resultMessage += "\n\nTop Findings:\n" + string.Join("\n", report.SuspiciousFindings.Take(5).Select(f =>
    //                     $"- {f.File}: {f.BehaviorType} ({f.Severity}) - {f.Explanation}"));
    //             }

    //             MessageBox.Show(resultMessage, "ZIP Analysis Results", MessageBoxButton.OK,
    //                 report.OverallRisk >= RiskLevel.LikelyCheat ? MessageBoxImage.Warning : MessageBoxImage.Information);

    //             ZipAnalysisText.Text = $"Analysis complete. Risk level: {report.OverallRisk}";
    //         }
    //         catch (Exception ex)
    //         {
    //             MessageBox.Show($"Analysis failed: {ex.Message}", "Analysis Error", MessageBoxButton.OK, MessageBoxImage.Error);
    //             ZipAnalysisText.Text = "Analysis failed. Select a .zip file to analyze for cheat patterns.";
    //         }
    //     }
    // }
}
