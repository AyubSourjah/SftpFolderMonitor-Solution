using SftpFolderMonitor.Contracts;

namespace SftpFolderMonitor;

public class Worker : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly IFileMonitorService _fileMonitor;
    private readonly ISftpService _sftpService;
    private readonly ILogger<Worker> _logger;

    public Worker(
        IConfiguration configuration,
        IFileMonitorService fileMonitor,
        ISftpService sftpService,
        ILogger<Worker> logger)
    {
        _configuration = configuration;
        _fileMonitor = fileMonitor;
        _sftpService = sftpService;
        _logger = logger;

        _fileMonitor.FileDetected += HandleFileDetected;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        var mappings = _configuration.GetSection("Monitor:Folders").Get<Dictionary<string, string>>();
        if (mappings == null || mappings.Count == 0)
        {
            _logger.LogError("No folder mappings found in configuration");
            return;
        }

        var rootPath = _configuration["Monitor:Root"];
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            _logger.LogError("Monitor:Root configuration is missing");
            return;
        }

        await _fileMonitor.StartMonitoringAsync(mappings, rootPath, cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _fileMonitor.StopMonitoringAsync();
        await base.StopAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;

    private async Task HandleFileDetected(string filePath, string remoteFolder, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Processing file {FilePath}", filePath);
            await _sftpService.UploadFileAsync(filePath, remoteFolder, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process file {FilePath}", filePath);
        }
    }
}