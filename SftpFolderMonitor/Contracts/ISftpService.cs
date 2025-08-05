namespace SftpFolderMonitor.Contracts;

public interface ISftpService
{
    Task UploadFileAsync(string localFilePath, string remoteFolder, CancellationToken cancellationToken);
}