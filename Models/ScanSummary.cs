namespace AntiCheatScanner.Models;

public sealed class ScanSummary
{
    public int TotalFilesScanned { get; init; }
    public int TotalDetections { get; init; }
    public int FailedFiles { get; init; }
    public int LogFiles { get; init; }
    public int JarFiles { get; init; }
    public int CriticalDetections { get; init; }
    public int HighDetections { get; init; }
}
