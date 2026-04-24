using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AntiCheatScanner.Models;

namespace AntiCheatScanner.Core;

public sealed class DetectionEngine
{
    private readonly Regex keywordRegex;

    public DetectionEngine(IEnumerable<string> keywords)
    {
        var normalizedKeywords = keywords
            .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
            .Select(keyword => keyword.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(keyword => keyword.Length)
            .ToArray();

        if (normalizedKeywords.Length == 0)
        {
            throw new InvalidOperationException("At least one keyword is required to initialize the detection engine.");
        }

        keywordRegex = new Regex(
            string.Join("|", normalizedKeywords.Select(Regex.Escape)),
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(2));
    }

    public List<Detection> AnalyzeText(string filePath, FileKind fileKind, string location, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var matches = keywordRegex.Matches(text);

        if (matches.Count == 0)
        {
            return [];
        }

        var results = new List<Detection>(matches.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? normalizedText = null;

        foreach (Match match in matches)
        {
            if (!match.Success)
            {
                continue;
            }

            var keyword = match.Value.ToLowerInvariant();

            if (!seen.Add(keyword))
            {
                continue;
            }

            normalizedText ??= text.ToLowerInvariant();
            var score = Heuristics.Score(normalizedText, keyword, fileKind, location);

            results.Add(new Detection
            {
                FilePath = filePath,
                FileKind = fileKind,
                Keyword = keyword,
                Location = location,
                Score = score,
                Severity = Heuristics.Risk(score),
                Evidence = ExtractEvidence(text, match.Index, match.Length)
            });
        }

        return results;
    }

    private static string ExtractEvidence(string text, int index, int length)
    {
        var flattened = text.Replace('\r', ' ').Replace('\n', ' ').Trim();

        if (string.IsNullOrWhiteSpace(flattened))
        {
            return "Matched keyword in empty or unreadable text block.";
        }

        var safeIndex = Math.Max(0, Math.Min(index, flattened.Length - 1));
        var start = Math.Max(0, safeIndex - 45);
        var end = Math.Min(flattened.Length, safeIndex + length + 70);
        var snippet = flattened[start..end].Trim();

        if (start > 0)
        {
            snippet = $"...{snippet}";
        }

        if (end < flattened.Length)
        {
            snippet = $"{snippet}...";
        }

        return snippet;
    }
}
