namespace AntiCheatScanner.Models;

public sealed class ScanFailure
{
    public string FilePath { get; init; } = string.Empty;
    public string Operation { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;

    public string FileName => System.IO.Path.GetFileName(FilePath);
}
