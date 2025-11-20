using SftpFolderMonitor.Contracts;
using System.Collections.Concurrent;

namespace SftpFolderMonitor;

public class FileMonitorService(ILogger<FileMonitorService> logger) : IFileMonitorService, IDisposable
{
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _debounceTokens = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan _debounceDelay = TimeSpan.FromMilliseconds(750);

    public event Func<string, string, CancellationToken, Task>? FileDetected;

    public Task StartMonitoringAsync(Dictionary<string, string> folderMappings, string rootPath, CancellationToken cancellationToken)
    {
        foreach (var (localFolder, remoteFolder) in folderMappings)
        {
            var fullPath = Path.Combine(rootPath, localFolder);

            if (string.IsNullOrWhiteSpace(fullPath) || string.IsNullOrWhiteSpace(remoteFolder))
            {
                logger.LogWarning("Invalid folder mapping: LocalFolder or RemoteFolder is null or empty");
                continue;
            }

            if (!Directory.Exists(fullPath))
            {
                logger.LogWarning("Local folder does not exist: {LocalFolder}", fullPath);
                continue;
            }

            var watcher = new FileSystemWatcher(fullPath)
            {
                // Reduce noisy filters to avoid multiple Changed events during writes
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                Filter = "*.*",
                EnableRaisingEvents = true
            };

            watcher.Created += (_, e) => ScheduleDebounced(e.FullPath, remoteFolder, cancellationToken);

            // Debounce Changed events so we process a file once after it settles
            watcher.Changed += (_, e) => ScheduleDebounced(e.FullPath, remoteFolder, cancellationToken);

            // Many apps write to a temp file and then rename into place
            watcher.Renamed += (_, e) => ScheduleDebounced(e.FullPath, remoteFolder, cancellationToken);

            _watchers.Add(watcher);

            logger.LogInformation("Monitoring {LocalFolder} -> {RemoteFolder}", fullPath, remoteFolder);
        }

        return Task.CompletedTask;
    }

    public Task StopMonitoringAsync()
    {
        foreach (var watcher in _watchers)
        {
            watcher.Dispose();
        }
        _watchers.Clear();

        CancelAllDebounces();
        return Task.CompletedTask;
    }

    private async Task OnFileDetected(string filePath, string remoteFolder, CancellationToken cancellationToken)
    {
        if (FileDetected != null)
        {
            await FileDetected(filePath, remoteFolder, cancellationToken);
        }
    }

    private void ScheduleDebounced(string filePath, string remoteFolder, CancellationToken appCancellation)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        // Cancel any pending debounce for this file (do not dispose yet; let its task finish)
        if (_debounceTokens.TryGetValue(filePath, out var existingCts))
        {
            try { existingCts.Cancel(); } catch { /* ignore */ }
        }

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(appCancellation);
        _debounceTokens[filePath] = linkedCts; // replace current debounce token
        var token = linkedCts.Token; // capture before potential disposal

        _ = Task.Run(async () =>
        {
            try
            {
                // Wait for a quiet period
                await Task.Delay(_debounceDelay, token).ConfigureAwait(false);

                // Ensure the file is ready (not locked and fully written)
                var ready = await WaitForFileReadyAsync(filePath, token).ConfigureAwait(false);
                if (!ready)
                {
                    logger.LogWarning("File not ready after debounce window: {File}", filePath);
                    return;
                }

                await OnFileDetected(filePath, remoteFolder, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // expected when another event arrives or service stops
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error handling debounced file event for {File}", filePath);
            }
            finally
            {
                // Only remove if this CTS is still the current one for the path
                if (_debounceTokens.TryGetValue(filePath, out var current) && ReferenceEquals(current, linkedCts))
                {
                    _debounceTokens.TryRemove(filePath, out _);
                    linkedCts.Dispose();
                }
                else
                {
                    // It was replaced; dispose safely
                    linkedCts.Dispose();
                }
            }
        }, token);
    }

    private static async Task<bool> WaitForFileReadyAsync(string filePath, CancellationToken ct)
    {
        const int maxAttempts = 10;
        const int delayMs = 200;

        for (int attempt = 0; attempt < maxAttempts && !ct.IsCancellationRequested; attempt++)
        {
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                // If we can open with FileShare.None, it's not locked by writer
                return true;
            }
            catch (IOException)
            {
                // likely still being written or locked; retry
            }
            catch (UnauthorizedAccessException)
            {
                // may occur during write/lock; retry
            }

            try
            {
                await Task.Delay(delayMs, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        return false;
    }

    private void CancelAllDebounces()
    {
        foreach (var kvp in _debounceTokens)
        {
            try { kvp.Value.Cancel(); } catch { /* ignore */ }
        }
        // Do not dispose/remove here; tasks will clean up appropriately.
    }

    public void Dispose()
    {
        foreach (var watcher in _watchers)
        {
            watcher.Dispose();
        }
        _watchers.Clear();

        CancelAllDebounces();
    }
}