namespace AntiCheatScanner.Models;

public sealed class Detection
{
    public string FilePath { get; init; } = string.Empty;
    public FileKind FileKind { get; init; }
    public string Keyword { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public DetectionSeverity Severity { get; init; }
    public int Score { get; init; }
    public string Evidence { get; init; } = string.Empty;

    public string FileName => System.IO.Path.GetFileName(FilePath);
    public string DirectoryPath => System.IO.Path.GetDirectoryName(FilePath) ?? string.Empty;
    public string SeverityLabel => Severity.ToString().ToUpperInvariant();
    public string KindLabel => FileKind == FileKind.Jar ? "JAR" : "LOG";
}
