using SftpFolderMonitor;
using Serilog;
using SftpFolderMonitor.Contracts;

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureAppConfiguration((context, config) =>
{
    config.SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
});

builder.UseSerilog((context, services, configuration) =>
{
    configuration.ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext();
});

builder.ConfigureServices((hostContext, services) =>
{
    // Configuration validation
    var sftpConfig = hostContext.Configuration.GetSection("Sftp").Get<SftpConfig>();
    if (sftpConfig != null)
    {
        sftpConfig.Validate();
        services.AddSingleton(sftpConfig);
    }
    
    services.AddSingleton<IFileMonitorService, FileMonitorService>();
    services.AddSingleton<ISftpService, SftpService>();
    services.AddHostedService<Worker>();
});

try
{
    Log.Information("Starting service");
    var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Service terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}