using System.Collections.Generic;

namespace AntiCheatScanner.Models;

public class ZipAnalysisReport
{
    public required string ArchiveSummary { get; init; }
    public required List<SuspiciousFinding> SuspiciousFindings { get; init; }
    public required RiskLevel OverallRisk { get; init; }
}