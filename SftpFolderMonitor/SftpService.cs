using Renci.SshNet;
using SftpFolderMonitor.Contracts;

namespace SftpFolderMonitor;

public class SftpService(SftpConfig config, ILogger<SftpService> logger) : ISftpService
{
    private readonly SftpConfig _config = config ?? throw new ArgumentNullException(nameof(config));

    public async Task UploadFileAsync(string localFilePath, string remoteFolder, CancellationToken cancellationToken)
    {
        try
        {
            var connection = CreateConnection();
            using var client = new SftpClient(connection);
            
            await Task.Run(() =>
            {
                client.Connect();
                using var fileStream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var remotePath = Path.Combine(remoteFolder, Path.GetFileName(localFilePath)).Replace("\\", "/");
                client.UploadFile(fileStream, remotePath);
                client.Disconnect();
            }, cancellationToken);

            logger.LogInformation("Successfully uploaded {FilePath} to {RemotePath}", localFilePath, remoteFolder);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to upload {FilePath} to {RemoteFolder}", localFilePath, remoteFolder);
            throw;
        }
    }

    private ConnectionInfo CreateConnection()
    {
        AuthenticationMethod authMethod = _config.AuthenticationMethod.ToLowerInvariant() switch
        {
            "privatekey" => new PrivateKeyAuthenticationMethod(_config.Username, new PrivateKeyFile(_config.SshKeyPath!)),
            "password" => new PasswordAuthenticationMethod(_config.Username, _config.Password!),
            _ => throw new InvalidOperationException($"Unsupported authentication method: {_config.AuthenticationMethod}")
        };
    
        return new ConnectionInfo(_config.Host, _config.Port, _config.Username, authMethod);
    }
}