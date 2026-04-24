using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace AntiCheatScanner.Utils;

public static class CheatLoader
{
    private static readonly object SyncRoot = new();
    private static readonly string[] DefaultCheats =
    [
        "aimbot",
        "autoclicker",
        "dll",
        "inject",
        "killaura",
        "reach",
        "velocity",
        "vape",
        "wurst"
    ];

    private static IReadOnlyList<string>? cachedKeywords;
    private static DateTime cachedWriteTimeUtc;

    public static IReadOnlyList<string> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "cheatnames.txt");

        lock (SyncRoot)
        {
            if (!File.Exists(path))
            {
                cachedKeywords ??= LoadEmbeddedKeywords();
                return cachedKeywords;
            }

            var lastWriteTimeUtc = File.GetLastWriteTimeUtc(path);

            if (cachedKeywords is not null && lastWriteTimeUtc == cachedWriteTimeUtc)
            {
                return cachedKeywords;
            }

            var loadedKeywords = File.ReadLines(path, Encoding.UTF8)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#", StringComparison.Ordinal))
                .Select(line => line.ToLowerInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            cachedKeywords = loadedKeywords.Length > 0 ? loadedKeywords : DefaultCheats;
            cachedWriteTimeUtc = lastWriteTimeUtc;

            return cachedKeywords;
        }
    }

    private static IReadOnlyList<string> LoadEmbeddedKeywords()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("AntiCheatScanner.cheatnames.txt");

        if (stream is null)
        {
            return DefaultCheats;
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);

        var loadedKeywords = reader
            .ReadToEnd()
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#", StringComparison.Ordinal))
            .Select(line => line.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return loadedKeywords.Length > 0 ? loadedKeywords : DefaultCheats;
    }
}
