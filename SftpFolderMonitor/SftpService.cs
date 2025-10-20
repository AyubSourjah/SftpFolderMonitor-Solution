using Renci.SshNet;
using SftpFolderMonitor.Contracts;

namespace SftpFolderMonitor;

public class SftpService(SftpConfig config, ILogger<SftpService> logger) : ISftpService, IDisposable
{
    private readonly SftpConfig _config = config ?? throw new ArgumentNullException(nameof(config));
    private SftpClient? _client;
    private readonly SemaphoreSlim _connectionSemaphore = new(1, 1);
    private bool _disposed;

    public async Task UploadFileAsync(string localFilePath, string remoteFolder, CancellationToken cancellationToken)
    {
        try
        {
            var client = await EnsureConnectedAsync(cancellationToken);

            await Task.Run(() =>
            {
                using var fileStream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var remotePath = Path.Combine(remoteFolder, Path.GetFileName(localFilePath)).Replace("\\", "/");
                client.UploadFile(fileStream, remotePath, true);
            }, cancellationToken);

            logger.LogInformation("Successfully uploaded {FilePath} to {RemotePath}", localFilePath, remoteFolder);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to upload {FilePath} to {RemoteFolder}", localFilePath, remoteFolder);
            throw;
        }
    }

    private async Task<SftpClient> EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        await _connectionSemaphore.WaitAsync(cancellationToken);
        
        try
        {
            if (_client is { IsConnected: true }) return _client;
            _client?.Dispose();
            
            var connection = CreateConnection();
            _client = new SftpClient(connection);
            await Task.Run(() => _client.Connect(), cancellationToken);
            logger.LogInformation("SFTP connection established to {Host}:{Port}", _config.Host, _config.Port);

            return _client;
        }
        finally
        {
            _connectionSemaphore.Release();
        }
    }

    private ConnectionInfo CreateConnection()
    {
        AuthenticationMethod authMethod = _config.AuthenticationMethod.ToLowerInvariant() switch
        {
            "privatekey" => new PrivateKeyAuthenticationMethod(_config.Username,
                new PrivateKeyFile(_config.SshKeyPath!)),
            "password" => new PasswordAuthenticationMethod(_config.Username, _config.Password!),
            _ => throw new InvalidOperationException(
                $"Unsupported authentication method: {_config.AuthenticationMethod}")
        };

        return new ConnectionInfo(_config.Host, _config.Port, _config.Username, authMethod);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _client?.Dispose();
        _connectionSemaphore.Dispose();
        _disposed = true;
    }
}