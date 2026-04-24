using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AntiCheatScanner.Models;

namespace AntiCheatScanner.Core;

public static class BehavioralAnalyzer
{
    private const int MaxEvidencePerFinding = 6;

    private static readonly PatternGroup[] Groups =
    [
        new(
            BehaviorType.MovementManipulation,
            DetectionSeverity.High,
            "Movement manipulation patterns detected (velocity/airborne/ground bypass logic).",
            [
                new PatternRule("velocity_change", @"(?:setVelocity|motion[XYZ]?|deltaMovement|setSpeed|playerVelocity)\s*(?:[\+\-\*\/]?=|\()"),
                new PatternRule("forced_airborne", @"(?:setFlying|allowFlying|isFlying|onGround\s*=\s*false|jumpTicks|airTicks)"),
                new PatternRule("ground_bypass", @"(?:no.?fall|bypass.*ground|ignore.*ground|spoof.*ground|onGround\s*=\s*true)")
            ]),
        new(
            BehaviorType.CombatAutomation,
            DetectionSeverity.High,
            "Combat automation patterns detected (timed attack loops or input simulation).",
            [
                new PatternRule("attack_loop", @"(?:while|for|tick|loop|schedule).{0,40}(?:attack|hit|swing)|(?:attack|hit|swing).{0,40}(?:delay|cooldown|tick|timer)"),
                new PatternRule("input_simulation", @"(?:simulate|inject|synthesize|fake).{0,25}(?:input|click|mouse|key)|Robot\(|SendInput"),
                new PatternRule("timing_logic", @"(?:cps|clicksPerSecond|randomDelay|nextClickAt|attackInterval|jitterClick)")
            ]),
        new(
            BehaviorType.RenderingModification,
            DetectionSeverity.Medium,
            "Rendering override patterns detected (visibility/occlusion/world-to-screen logic).",
            [
                new PatternRule("visibility_override", @"(?:setInvisible|isInvisible|entityVisible|glDisable|depthTest|alpha|opacity)\s*(?:[\+\-\*\/]?=|\()"),
                new PatternRule("occlusion_disable", @"(?:disableOcclusion|occlusion\s*=\s*false|frustum|cull|xray|throughWall)"),
                new PatternRule("world_to_screen", @"(?:worldToScreen|screenToWorld|project\(|unproject\(|viewMatrix|projectionMatrix)")
            ]),
        new(
            BehaviorType.NetworkManipulation,
            DetectionSeverity.High,
            "Network manipulation patterns detected (packet resend/timing/desync/spoof logic).",
            [
                new PatternRule("packet_replay", @"(?:resend|replay|queue|buffer).{0,25}(?:packet|transaction)|(?:packet).{0,25}(?:replay|resend|duplicate)"),
                new PatternRule("packet_timing", @"(?:packet).{0,30}(?:delay|flush|throttle|timer|burst)|(?:sleep|timer|tick).{0,30}(?:packet)"),
                new PatternRule("desync_spoof", @"(?:desync|spoof|fakePacket|blink|lagSwitch|positionSpoof|rotationSpoof)")
            ]),
        new(
            BehaviorType.InjectionModification,
            DetectionSeverity.High,
            "Runtime injection/modification patterns detected (reflection/mixins/hooks/bytecode).",
            [
                new PatternRule("reflection", @"(?:Class\.forName|getDeclaredField|getDeclaredMethod|setAccessible|MethodHandle|Unsafe)"),
                new PatternRule("runtime_patch", @"(?:bytecode|ASM|ClassNode|MethodNode|transform|instrument|redefine|classLoader)"),
                new PatternRule("hook_intercept", @"(?:mixin|injectAt|hook|intercept|eventBus|callback|overwrite)")
            ]),
        new(
            BehaviorType.LogAnomaly,
            DetectionSeverity.Medium,
            "Log anomalies detected (anti-cheat flags, corrections, packet flood indicators).",
            [
                new PatternRule("anti_cheat_flags", @"(?:anti.?cheat|violation|flagged|detected\s+cheat|banwave)"),
                new PatternRule("movement_corrections", @"(?:teleport\s+correct|position\s+correct|moved\s+too\s+quickly|rubber.?band|rollback)"),
                new PatternRule("packet_flood", @"(?:packet\s+flood|too\s+many\s+packets|packet\s+spam|disconnect.*packet)")
            ])
    ];

    public static List<SuspiciousFinding> AnalyzeFiles(List<ExtractedFile> files)
    {
        if (files.Count == 0)
        {
            return [];
        }

        var findings = new List<SuspiciousFinding>();
        var hitsByFile = new Dictionary<string, List<PatternHit>>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var fileHits = AnalyzeSingleFile(file);
            if (fileHits.Count == 0)
            {
                continue;
            }

            hitsByFile[file.Path] = fileHits;
            findings.AddRange(BuildSingleFileFindings(file.Path, fileHits));
        }

        findings.AddRange(AnalyzeCrossFilePatterns(hitsByFile));
        return findings;
    }

    private static List<PatternHit> AnalyzeSingleFile(ExtractedFile file)
    {
        var hits = new List<PatternHit>();
        var lines = file.Content.Split('\n');

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var rawLine = lines[lineIndex];
            var line = rawLine.Trim();

            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var group in Groups)
            {
                foreach (var rule in group.Rules)
                {
                    if (!rule.Regex.IsMatch(line))
                    {
                        continue;
                    }

                    hits.Add(new PatternHit(
                        group.BehaviorType,
                        group.Severity,
                        group.Explanation,
                        rule.Name,
                        $"line {lineIndex + 1}: {TrimEvidence(line)}"));
                }
            }
        }

        return hits;
    }

    private static IEnumerable<SuspiciousFinding> BuildSingleFileFindings(string filePath, IEnumerable<PatternHit> hits)
    {
        return hits
            .GroupBy(hit => hit.BehaviorType)
            .Select(group =>
            {
                var evidence = group
                    .Select(hit => $"{hit.RuleName}: {hit.Evidence}")
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(MaxEvidencePerFinding)
                    .ToList();

                return new SuspiciousFinding
                {
                    File = filePath,
                    BehaviorType = group.Key,
                    Severity = group.Max(hit => hit.Severity),
                    Evidence = evidence,
                    Explanation = group.First().Explanation
                };
            });
    }

    private static IEnumerable<SuspiciousFinding> AnalyzeCrossFilePatterns(Dictionary<string, List<PatternHit>> hitsByFile)
    {
        if (hitsByFile.Count < 2)
        {
            return [];
        }

        var findings = new List<SuspiciousFinding>();
        var filesByBehavior = hitsByFile
            .SelectMany(item => item.Value.Select(hit => (item.Key, hit.BehaviorType)))
            .Distinct()
            .GroupBy(item => item.BehaviorType)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.Key).Distinct(StringComparer.OrdinalIgnoreCase).ToList());

        var activeBehaviors = filesByBehavior.Where(x => x.Value.Count > 0).ToList();

        if (activeBehaviors.Count >= 3)
        {
            var strongest = RankCrossFileSeverity(activeBehaviors.Select(x => x.Key).ToHashSet());
            findings.Add(new SuspiciousFinding
            {
                File = "Multiple Files",
                BehaviorType = BehaviorType.InjectionModification,
                Severity = strongest,
                Evidence = activeBehaviors
                    .Select(item => $"{item.Key}: {item.Value.Count} file(s)")
                    .Take(MaxEvidencePerFinding)
                    .ToList(),
                Explanation = "Cross-file behavior correlation detected: suspicious features are distributed across modules."
            });
        }

        // Hidden feature spread: behavior split across related module paths.
        var moduleBehaviorSpread = hitsByFile
            .Select(item => new
            {
                Module = GetModuleKey(item.Key),
                Behaviors = item.Value.Select(hit => hit.BehaviorType).Distinct().ToList()
            })
            .GroupBy(item => item.Module, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                Module = group.Key,
                Behaviors = group.SelectMany(item => item.Behaviors).Distinct().ToList(),
                FileCount = group.Count()
            })
            .Where(item => item.FileCount >= 2 && item.Behaviors.Count >= 2)
            .OrderByDescending(item => item.Behaviors.Count)
            .ThenByDescending(item => item.FileCount)
            .FirstOrDefault();

        if (moduleBehaviorSpread is not null)
        {
            findings.Add(new SuspiciousFinding
            {
                File = $"Module: {moduleBehaviorSpread.Module}",
                BehaviorType = BehaviorType.NetworkManipulation,
                Severity = moduleBehaviorSpread.Behaviors.Count >= 3 ? DetectionSeverity.Critical : DetectionSeverity.High,
                Evidence =
                [
                    $"Behavior categories in module: {string.Join(", ", moduleBehaviorSpread.Behaviors)}",
                    $"Module files involved: {moduleBehaviorSpread.FileCount}"
                ],
                Explanation = "Hidden feature spread detected: suspicious capabilities are split across related files/modules."
            });
        }

        // Logs + implementation pairing is often a strong indicator.
        var hasLogAnomalies = filesByBehavior.ContainsKey(BehaviorType.LogAnomaly);
        var hasMovement = filesByBehavior.ContainsKey(BehaviorType.MovementManipulation);
        var hasNetwork = filesByBehavior.ContainsKey(BehaviorType.NetworkManipulation);

        if (hasLogAnomalies && (hasMovement || hasNetwork))
        {
            findings.Add(new SuspiciousFinding
            {
                File = "Multiple Files",
                BehaviorType = BehaviorType.LogAnomaly,
                Severity = DetectionSeverity.High,
                Evidence =
                [
                    $"Log anomaly files: {filesByBehavior[BehaviorType.LogAnomaly].Count}",
                    hasMovement ? $"Movement manipulation files: {filesByBehavior[BehaviorType.MovementManipulation].Count}" : "Movement manipulation files: 0",
                    hasNetwork ? $"Network manipulation files: {filesByBehavior[BehaviorType.NetworkManipulation].Count}" : "Network manipulation files: 0"
                ],
                Explanation = "Runtime anomalies in logs correlate with suspicious implementation code across files."
            });
        }

        return findings;
    }

    private static DetectionSeverity RankCrossFileSeverity(ISet<BehaviorType> behaviorTypes)
    {
        var hasMovement = behaviorTypes.Contains(BehaviorType.MovementManipulation);
        var hasCombat = behaviorTypes.Contains(BehaviorType.CombatAutomation);
        var hasNetwork = behaviorTypes.Contains(BehaviorType.NetworkManipulation);
        var hasInjection = behaviorTypes.Contains(BehaviorType.InjectionModification);

        if ((hasMovement || hasCombat) && hasNetwork && hasInjection)
        {
            return DetectionSeverity.Critical;
        }

        if ((hasMovement && hasNetwork) || (hasCombat && hasNetwork) || (hasInjection && hasNetwork))
        {
            return DetectionSeverity.High;
        }

        return DetectionSeverity.Medium;
    }

    private static string GetModuleKey(string filePath)
    {
        var normalized = filePath.Replace('\\', '/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length <= 1)
        {
            return segments.FirstOrDefault() ?? "root";
        }

        return $"{segments[0]}/{segments[1]}";
    }

    private static string TrimEvidence(string value)
    {
        const int maxLength = 140;
        return value.Length <= maxLength ? value : $"{value[..maxLength]}...";
    }

    private sealed record PatternRule(string Name, string Pattern)
    {
        public Regex Regex { get; } = new(Pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    private sealed record PatternGroup(
        BehaviorType BehaviorType,
        DetectionSeverity Severity,
        string Explanation,
        PatternRule[] Rules);

    private sealed record PatternHit(
        BehaviorType BehaviorType,
        DetectionSeverity Severity,
        string Explanation,
        string RuleName,
        string Evidence);
}
