namespace AntiCheatScanner.Models;

public sealed class ScanProgressUpdate
{
    public string Stage { get; init; } = string.Empty;
    public string CurrentItem { get; init; } = string.Empty;
    public int ProcessedFiles { get; init; }
    public int TotalFiles { get; init; }

    public double Percentage => TotalFiles <= 0
        ? 0
        : (double)ProcessedFiles / TotalFiles * 100.0;
}
