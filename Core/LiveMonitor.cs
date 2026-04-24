using System;
using System.IO;

namespace AntiCheatScanner.Core;

public sealed class LiveMonitor : IDisposable
{
    private readonly FileSystemWatcher watcher;
    private readonly Action<string> callback;

    public LiveMonitor(string path, Action<string> callback)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"The folder '{path}' does not exist.");
        }

        this.callback = callback ?? throw new ArgumentNullException(nameof(callback));

        watcher = new FileSystemWatcher(path)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
            Filter = "*.*"
        };

        watcher.Created += OnCreated;
        watcher.Changed += OnChanged;
        watcher.Renamed += OnRenamed;
        watcher.Deleted += OnDeleted;
        watcher.Error += OnError;
    }

    public void Start()
    {
        watcher.EnableRaisingEvents = true;
    }

    public void Stop()
    {
        watcher.EnableRaisingEvents = false;
    }

    public void Dispose()
    {
        watcher.Dispose();
    }

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        Publish("New", e.FullPath);
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        Publish("Updated", e.FullPath);
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        if (!IsRelevantFile(e.FullPath))
        {
            return;
        }

        callback($"[LIVE] Renamed {e.OldFullPath} -> {e.FullPath}");
    }

    private void OnDeleted(object sender, FileSystemEventArgs e)
    {
        Publish("Deleted", e.FullPath);
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        callback($"[LIVE] Monitor error: {e.GetException().Message}");
    }

    private void Publish(string action, string path)
    {
        if (!IsRelevantFile(path))
        {
            return;
        }

        callback($"[LIVE] {action} {Path.GetExtension(path).TrimStart('.').ToUpperInvariant()} file: {path}");
    }

    private static bool IsRelevantFile(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension is ".jar" or ".log";
    }
}
