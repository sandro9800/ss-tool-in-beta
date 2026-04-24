using System.Collections.Generic;

namespace AntiCheatScanner.Models;

public enum BehaviorType
{
    MovementManipulation,
    CombatAutomation,
    RenderingModification,
    NetworkManipulation,
    InjectionModification,
    LogAnomaly
}

public enum RiskLevel
{
    Clean,
    Suspicious,
    LikelyCheat,
    ConfirmedCheat
}

public class SuspiciousFinding
{
    public required string File { get; init; }
    public required BehaviorType BehaviorType { get; init; }
    public required DetectionSeverity Severity { get; init; }
    public required List<string> Evidence { get; init; }
    public required string Explanation { get; init; }
}