using Renci.SshNet;
using Serilog;

namespace SftpFolderMonitor;

public class Worker : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly SftpConfig? _sftpConfig;

    public Worker(IConfiguration configuration)
    {
        _configuration = configuration;
        _sftpConfig = _configuration.GetSection("Sftp").Get<SftpConfig>();
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        var mappings = _configuration.GetSection("Monitor:Folders").Get<Dictionary<string, string>>();
        if (mappings == null || mappings.Count == 0)
        {
            Log.Error("No folder mappings found in configuration.");
            return Task.CompletedTask;
        }

        foreach (var (localFolder, remoteFolder) in mappings)
        {
            var fullPath = string.Concat(_configuration["Monitor:Root"], localFolder);
            
            if (string.IsNullOrWhiteSpace(fullPath) || string.IsNullOrWhiteSpace(remoteFolder))
            {
                Log.Warning("Invalid folder mapping: LocalFolder or RemoteFolder is null or empty.");
                continue;
            }
    
            if (!Directory.Exists(fullPath))
            {
                Log.Warning("Local folder does not exist: {LocalFolder}", fullPath);
                continue;
            }
    
            var watcher = new FileSystemWatcher(fullPath)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size,
                Filter = "*.*",
                EnableRaisingEvents = true
            };
    
            watcher.Created += (s, e) =>
                Task.Run(() => ProcessFile(e.FullPath, remoteFolder), cancellationToken);
    
            _watchers.Add(watcher);
            
            Log.Information("Monitoring {LocalFolder} -> {RemoteFolder}", 
                fullPath, remoteFolder);
        }
    
        return base.StartAsync(cancellationToken);
    }

    private void ProcessFile(string filePath, string remoteFolder)
    {
        try
        {
            if (_sftpConfig != null)
            {
                Log.Information("Transferring {FilePath} to {SftpConfigHost}:{RemoteFolder}",
                    filePath, _sftpConfig.Host, remoteFolder);

                // Create an SFTP connection configuration using the provided host, port, and username.
                // Determine the authentication method based on the configuration:
                // - If "privateKey", use a private key file for authentication.
                // - If "key", use a password for authentication.
                // - Otherwise, throw an exception for unsupported authentication methods.
                var connection =
                    new ConnectionInfo(_sftpConfig.Host, _sftpConfig.Port, _sftpConfig.Username,
                        _sftpConfig.AuthenticationMethod.Equals("privateKey", StringComparison.OrdinalIgnoreCase)
                            ? new PrivateKeyAuthenticationMethod(_sftpConfig.Username,
                                new PrivateKeyFile(_sftpConfig.SshKeyPath))
                            : _sftpConfig.AuthenticationMethod.Equals("key", StringComparison.OrdinalIgnoreCase)
                                ? new PasswordAuthenticationMethod(_sftpConfig.Username, _sftpConfig.Password)
                                : throw new InvalidOperationException(
                                    $"Unsupported authentication method: {_sftpConfig.AuthenticationMethod}"));

                using var client = new SftpClient(connection);
                client.Connect();

                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var remotePath = Path.Combine(remoteFolder, Path.GetFileName(filePath)).Replace("\\", "/");
                client.UploadFile(fileStream, remotePath);
                client.Disconnect();

                Log.Information("Transfer complete: {FilePath}", filePath);
            }
            else Log.Information("SFTP was not initialized. File {FilePath} was not transferred.", 
                filePath);
        }
        catch (Exception ex)
        {
            Log.Information(ex, "Error transferring {FilePath}", filePath);
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var watcher in _watchers)
            watcher.Dispose();

        return base.StopAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}

public class SftpConfig
{
    public required string Host { get; init; }
    public required int Port { get; init; }
    public required string Username { get; init; }
    public required string Password { get; init; }
    public required string SshKeyPath { get; init; }
    public required string AuthenticationMethod { get; init; }
}