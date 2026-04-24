using System;
using System.Collections.Generic;
using AntiCheatScanner.Core;

namespace AntiCheatScanner.Models;

public sealed class ScanOptions
{
    public string RootPath { get; init; } = string.Empty;
    public IReadOnlyList<string> RootPaths { get; init; } = [];
    public ScanTarget Target { get; init; } = ScanTarget.All;
    public int MaxDegreeOfParallelism { get; init; } = Math.Max(1, Environment.ProcessorCount - 1);
    public IProgress<ScanProgressUpdate>? Progress { get; init; }
}
