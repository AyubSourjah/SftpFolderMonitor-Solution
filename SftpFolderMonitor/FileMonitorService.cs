using SftpFolderMonitor.Contracts;

namespace SftpFolderMonitor;

public class FileMonitorService(ILogger<FileMonitorService> logger) : IFileMonitorService, IDisposable
{
    private readonly List<FileSystemWatcher> _watchers = [];

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
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size,
                Filter = "*.*",
                EnableRaisingEvents = true
            };

            watcher.Created += (_, e) =>
                Task.Run(() => OnFileDetected(e.FullPath, remoteFolder, cancellationToken), cancellationToken);

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
        return Task.CompletedTask;
    }

    private async Task OnFileDetected(string filePath, string remoteFolder, CancellationToken cancellationToken)
    {
        if (FileDetected != null)
        {
            await FileDetected(filePath, remoteFolder, cancellationToken);
        }
    }

    public void Dispose()
    {
        foreach (var watcher in _watchers)
        {
            watcher.Dispose();
        }
        _watchers.Clear();
    }
}