using System;
using System.Collections.Generic;

namespace AntiCheatScanner.Models;

public sealed class ScanReport
{
    public string RootPath { get; init; } = string.Empty;
    public DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset CompletedAtUtc { get; init; }
    public List<DiscoveredFile> Files { get; init; } = [];
    public List<Detection> Detections { get; init; } = [];
    public List<ScanFailure> Failures { get; init; } = [];
    public ScanSummary Summary { get; init; } = new();

    public TimeSpan Duration => CompletedAtUtc - StartedAtUtc;
}
