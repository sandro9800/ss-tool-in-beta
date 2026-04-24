using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AntiCheatScanner.Models;

namespace AntiCheatScanner.Core;

public static class ZipAnalyzer
{
    public static ZipAnalysisReport AnalyzeZip(string zipPath)
    {
        string extractedPath = null!;
        try
        {
            // Extract ZIP safely
            extractedPath = ZipProcessor.ExtractZip(zipPath);

            // Load and analyze files
            var files = ZipProcessor.LoadFiles(extractedPath);

            // Perform behavioral analysis
            var findings = BehavioralAnalyzer.AnalyzeFiles(files);

            // Generate summary
            var archiveSummary = GenerateArchiveSummary(files, findings);

            // Determine overall risk
            var overallRisk = DetermineOverallRisk(findings);

            return new ZipAnalysisReport
            {
                ArchiveSummary = archiveSummary,
                SuspiciousFindings = findings,
                OverallRisk = overallRisk
            };
        }
        finally
        {
            // Clean up extracted files
            if (!string.IsNullOrEmpty(extractedPath) && Directory.Exists(extractedPath))
            {
                try
                {
                    Directory.Delete(extractedPath, recursive: true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }

    public static string AnalyzeZipJson(string zipPath)
    {
        var report = AnalyzeZip(zipPath);
        var payload = new
        {
            archive_summary = report.ArchiveSummary,
            suspicious_findings = report.SuspiciousFindings.Select(finding => new
            {
                file = finding.File,
                behavior_type = ToBehaviorKey(finding.BehaviorType),
                severity = finding.Severity.ToString().ToUpperInvariant(),
                evidence = finding.Evidence,
                explanation = finding.Explanation
            }),
            overall_risk = ToRiskLabel(report.OverallRisk)
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    private static string GenerateArchiveSummary(List<ExtractedFile> files, List<SuspiciousFinding> findings)
    {
        var codeFiles = files.Count(f => f.Type == ExtractedFileType.Code);
        var logFiles = files.Count(f => f.Type == ExtractedFileType.Log);
        var configFiles = files.Count(f => f.Type == ExtractedFileType.Config);
        var behaviorGroups = findings.Select(f => f.BehaviorType).Distinct().Count();

        var summary = $"Archive contains {files.Count} analyzable files ({codeFiles} code, {logFiles} logs, {configFiles} configs). ";

        if (findings.Count == 0)
        {
            summary += "No suspicious patterns detected.";
        }
        else
        {
            var highSeverity = findings.Count(f => f.Severity == DetectionSeverity.High || f.Severity == DetectionSeverity.Critical);
            var critical = findings.Count(f => f.Severity == DetectionSeverity.Critical);
            var crossFile = findings.Count(f => f.File.StartsWith("Multiple Files", StringComparison.OrdinalIgnoreCase) || f.File.StartsWith("Module:", StringComparison.OrdinalIgnoreCase));
            summary += $"{findings.Count} suspicious findings across {behaviorGroups} behavior categories ({highSeverity} high/critical, {critical} critical, {crossFile} cross-file correlations).";
        }

        return summary;
    }

    private static RiskLevel DetermineOverallRisk(List<SuspiciousFinding> findings)
    {
        if (findings.Count == 0)
            return RiskLevel.Clean;

        var criticalCount = findings.Count(f => f.Severity == DetectionSeverity.Critical);
        var highCount = findings.Count(f => f.Severity == DetectionSeverity.High);
        var mediumCount = findings.Count(f => f.Severity == DetectionSeverity.Medium);
        var crossFileCount = findings.Count(f => f.File.StartsWith("Multiple Files", StringComparison.OrdinalIgnoreCase) || f.File.StartsWith("Module:", StringComparison.OrdinalIgnoreCase));

        if (criticalCount > 0 || (highCount >= 2 && crossFileCount >= 1))
            return RiskLevel.ConfirmedCheat;

        if (highCount >= 2 || (highCount >= 1 && mediumCount >= 2) || crossFileCount >= 2)
            return RiskLevel.LikelyCheat;

        if (highCount >= 1 || mediumCount >= 2)
            return RiskLevel.Suspicious;

        return RiskLevel.Clean;
    }

    private static string ToBehaviorKey(BehaviorType behaviorType)
    {
        return behaviorType switch
        {
            BehaviorType.MovementManipulation => "movement_modification",
            BehaviorType.CombatAutomation => "combat_automation",
            BehaviorType.RenderingModification => "rendering_modification",
            BehaviorType.NetworkManipulation => "network_manipulation",
            BehaviorType.InjectionModification => "injection_modification",
            BehaviorType.LogAnomaly => "log_anomaly",
            _ => "unknown"
        };
    }

    private static string ToRiskLabel(RiskLevel riskLevel)
    {
        return riskLevel switch
        {
            RiskLevel.Clean => "CLEAN",
            RiskLevel.Suspicious => "SUSPICIOUS",
            RiskLevel.LikelyCheat => "LIKELY CHEAT",
            RiskLevel.ConfirmedCheat => "CONFIRMED CHEAT",
            _ => "SUSPICIOUS"
        };
    }
}
