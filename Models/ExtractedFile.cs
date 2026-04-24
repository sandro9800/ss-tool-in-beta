using System;

namespace AntiCheatScanner.Models;

public enum ExtractedFileType
{
    Code,
    Log,
    Config,
    Other
}

public class ExtractedFile
{
    public required string Path { get; init; }
    public required string Content { get; init; }
    public required ExtractedFileType Type { get; init; }
    public string? Extension { get; init; }
}