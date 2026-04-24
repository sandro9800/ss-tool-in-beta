using System;
using AntiCheatScanner.Models;

namespace AntiCheatScanner.Core;

public static class Heuristics
{
    private static readonly string[] CriticalKeywordSignals =
    [
        "inject", "dll", "manualmap", "loadlibrary", "spoofer", "hwid", "bypass", "ghost", "exploit", "packet"
    ];

    private static readonly string[] KnownClientSignals =
    [
        "vape", "wurst", "liquidbounce", "meteor", "rise", "sigma", "novoline", "rusher", "future", "tenacity"
    ];

    private static readonly string[] CombatSignals =
    [
        "killaura", "aura", "aim", "trigger", "reach", "velocity", "antikb", "autoclick", "forcefield"
    ];

    private static readonly string[] MovementSignals =
    [
        "fly", "speed", "bhop", "scaffold", "noslow", "phase", "noclip", "freecam"
    ];

    private static readonly string[] VisualSignals =
    [
        "esp", "wallhack", "xray", "chams", "fullbright", "nametag", "tracer"
    ];

    private static readonly string[] InjectionContextSignals =
    [
        "manual map", "manualmap", "loadlibrary", "createremotethread", "openprocess", "writeprocessmemory", "jni", "hook"
    ];

    private static readonly string[] FeatureContextSignals =
    [
        "module", "feature", "combat", "rotation", "autoclick", "velocity", "reach", "client"
    ];

    private static readonly string[] EvasionContextSignals =
    [
        "bypass", "screenshare", "destruct", "ghost client", "undetected", "spoof"
    ];

    public static int Score(string normalizedText, string keyword, FileKind fileKind, string location)
    {
        var score = BaseKeywordScore(keyword);

        if (ContainsAny(normalizedText, InjectionContextSignals))
        {
            score += 18;
        }

        if (ContainsAny(normalizedText, FeatureContextSignals))
        {
            score += 12;
        }

        if (ContainsAny(normalizedText, EvasionContextSignals))
        {
            score += 14;
        }

        if (fileKind == FileKind.Jar)
        {
            score += 4;
        }

        if (location.Contains("manifest", StringComparison.OrdinalIgnoreCase) ||
            location.Contains("mixin", StringComparison.OrdinalIgnoreCase) ||
            location.Contains("config", StringComparison.OrdinalIgnoreCase))
        {
            score += 6;
        }

        if (normalizedText.Length > 4000)
        {
            score += 4;
        }

        return Math.Clamp(score, 1, 100);
    }

    public static DetectionSeverity Risk(int score)
    {
        if (score >= 75)
        {
            return DetectionSeverity.Critical;
        }

        if (score >= 55)
        {
            return DetectionSeverity.High;
        }

        if (score >= 30)
        {
            return DetectionSeverity.Medium;
        }

        return DetectionSeverity.Low;
    }

    private static int BaseKeywordScore(string keyword)
    {
        if (ContainsAny(keyword, CriticalKeywordSignals))
        {
            return 56;
        }

        if (ContainsAny(keyword, KnownClientSignals))
        {
            return 44;
        }

        if (ContainsAny(keyword, CombatSignals))
        {
            return 38;
        }

        if (ContainsAny(keyword, MovementSignals))
        {
            return 30;
        }

        if (ContainsAny(keyword, VisualSignals))
        {
            return 25;
        }

        return 18;
    }

    private static bool ContainsAny(string text, string[] values)
    {
        foreach (var value in values)
        {
            if (text.Contains(value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
