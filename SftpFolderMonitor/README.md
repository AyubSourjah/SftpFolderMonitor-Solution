# SFTP Folder Monitor

## Overview

The SFTP Folder Monitor is a .NET Core Windows Service application designed to monitor local folders for new files and automatically transfer them to a remote SFTP server. It uses `FileSystemWatcher` to detect file changes and the `Renci.SshNet` library for SFTP operations.

## Features

- Monitors multiple local folders for new files using `FileSystemWatcher`
- Automatically uploads new files to specified remote SFTP directories
- Supports both SSH private key and password authentication
- Configurable via `appsettings.json`
- Structured logging with Serilog to console and file
- Runs as a background service with proper lifecycle management
- Comprehensive error handling and validation

## Architecture

The application follows a clean architecture pattern with:
- **Worker**: Background service that orchestrates the monitoring
- **FileMonitorService**: Handles file system monitoring
- **SftpService**: Manages SFTP connections and file uploads
- **SftpConfig**: Configuration model with validation

## Configuration

The application is configured using the `appsettings.json` file. Below is an example configuration:

```json
{
  "Monitor": {
    "Folders": {
      "\\sftp\\uploads1": "uploads",
      "\\sftp\\uploads2": "uploads"
    },
    "Root": "D:"
  },
  "Sftp": {
    "Host": "20.2.8.54",
    "Port": 22,
    "Username": "sftpuser",
    "Password": "",
    "SshKeyPath": "D:\\sftp\\sftp_key",
    "AuthenticationMethod": "privateKey"
  },
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      {
        "Name": "Console"
      },
      {
        "Name": "File",
        "Args": {
          "path": "Logs\\service.log",
          "rollingInterval": "Day"
        }
      }
    ],
    "Enrich": [
      "FromLogContext"
    ]
  }
}
```
## Configuration Sections
Monitor
- **Root:** Base directory path (e.g., "D:")
- **Folders:** Dictionary mapping local folder paths to remote SFTP directories
- **Key:** Local folder path relative to Root
- **Value:** Remote SFTP directory path

Sftp
- **Host:** SFTP server hostname or IP address
- **Port:** SFTP server port (default: 22)
- **Username:** SFTP username (required)
- **Password:** Password for authentication (required for password auth)
- **SshKeyPath:** Path to SSH private key file (required for privateKey auth)
- **AuthenticationMethod:** Authentication method ("password" or "privateKey")

Serilog
Configures structured logging:
- **MinimumLevel:** Minimum log level to capture
- **WriteTo:** Array of log sinks (Console, File, etc.)
- **Enrich:** Log enrichers for additional context

## How It Works
- **Service Startup**
  - Application reads and validates configuration from appsettings.json
  - Registers services in dependency injection container
  - Creates FileSystemWatcher instances for each configured folder mapping
  - Starts monitoring specified directories


- **File Detection**
  - FileSystemWatcher detects new files (Created events)
  - Monitors FileName and Size changes with filter *.*
  - Triggers asynchronous file processing via event handler


- **File Upload Process**
  - Establishes SFTP connection using configured authentication method
  - Uploads file to corresponding remote directory
  - Maintains original filename in remote location
  - Handles connection lifecycle (connect/disconnect per upload)
  

- **Error Handling & Logging**
  - Comprehensive error logging with structured data
  - Validation of configuration on startup
  - Graceful handling of missing directories and connection failures

## Service Registration
The application uses dependency injection with the following service registrations:

```csharp
builder.ConfigureServices((hostContext, services) =>
{
    // Configuration validation and registration
    var sftpConfig = hostContext.Configuration.GetSection("Sftp").Get<SftpConfig>();
    if (sftpConfig != null)
    {
        sftpConfig.Validate();
        services.AddSingleton(sftpConfig);
    }

    // Service registrations
    services.AddScoped<IFileMonitorService, FileMonitorService>();
    services.AddScoped<ISftpService, SftpService>();
    services.AddHostedService<Worker>();
});
```

## Authentication Methods

Private Key Authentication

```json
{
  "AuthenticationMethod": "privateKey",
  "SshKeyPath": "D:\\sftp\\sftp_key",
  "Username": "sftpuser"
}
```

Password Authentication

```json
{
  "AuthenticationMethod": "password",
  "Username": "sftpuser",
  "Password": "your_password"
}
```

### Dependencies
  - .NET Core - Cross-platform runtime
  - Renci.SshNet - SSH/SFTP client library
  - Serilog - Structured logging framework
  - Microsoft.Extensions.Hosting - Background service hosting
  - Microsoft.Extensions.DependencyInjection - Dependency injection container


### File Monitoring Behavior
  - Trigger: File creation events in monitored directories
  - Filter: All file types (*.*)
  - Processing: Asynchronous upload to maintain responsiveness
  - Path Mapping: Local paths combined with Root, mapped to remote directories
  - Event Handling: Uses event-driven architecture with FileDetected event


### Error Scenarios
- The application handles various error conditions:
- Missing or invalid configuration
- Non-existent local directories (logged as warnings)
- SFTP connection failures
- File access issues during upload
- Invalid SSH keys or authentication failures
- Missing service registrations


## Extending the Application
### Adding New Features
- Implement additional interfaces in the Contracts namespace
- Extend Worker class for custom processing logic
- Add new configuration sections with corresponding models

### Custom File Processing
- Modify FileMonitorService.OnFileDetected for custom file handling
- Implement file filtering or transformation before upload
- Add retry mechanisms or queue-based processing

### Logging
The application uses Serilog for structured logging with:

- Information: Normal operations, file uploads, monitoring status
- Warning: Non-critical issues like missing directories
- Error: Upload failures, connection issues
- Fatal: Service startup failures

Log files are stored in the Logs directory with daily rolling.