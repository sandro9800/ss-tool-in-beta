namespace AntiCheatScanner.Models;

public sealed class GuaranteedFile
{
    public string FilePath { get; init; } = string.Empty;
    public string KindLabel { get; init; } = string.Empty;
    public int HighestScore { get; init; }
    public string KeywordSummary { get; init; } = string.Empty;

    public string FileName => System.IO.Path.GetFileName(FilePath);
    public string DirectoryPath => System.IO.Path.GetDirectoryName(FilePath) ?? string.Empty;
}
