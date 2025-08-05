namespace SftpFolderMonitor.Contracts;

public interface IFileMonitorService
{
    event Func<string, string, CancellationToken, Task>? FileDetected;

    Task StartMonitoringAsync(Dictionary<string, string> folderMappings, string rootPath,
        CancellationToken cancellationToken);

    Task StopMonitoringAsync();
}