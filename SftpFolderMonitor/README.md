
# SFTP Folder Monitor

## Overview

The SFTP Folder Monitor is a .NET Core application designed to monitor local folders for new files and automatically transfer them to a remote SFTP server. It uses `FileSystemWatcher` to detect file changes and the `Renci.SshNet` library for SFTP operations.

## Features

- Monitors multiple local folders for new files.
- Automatically uploads new files to specified remote SFTP directories.
- Configurable via `appsettings.json`.
- Logs operations and errors to the console.

## Configuration

The application is configured using the `appsettings.json` file. Below is an example configuration:

```json
{
  "Monitor": {
    "Folders": {
      "C:\\WatchFolder1": "/remote/path1",
      "C:\\WatchFolder2": "/remote/path2",
      "D:\\AnotherFolder": "/archive"
    }
  },
  "Sftp": {
    "Host": "sftp.example.com",
    "Port": 22,
    "Username": "sftpuser",
    "SshKeyPath": "C:\\Keys\\id_rsa"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

### Key Sections

1. **Monitor:Folders**  
   Maps local folders to remote SFTP directories. Each key is a local folder path, and its value is the corresponding remote folder path.

2. **Sftp**  
   Contains SFTP server connection details:
   - `Host`: SFTP server hostname.
   - `Port`: SFTP server port (default is 22).
   - `Username`: SFTP username.
   - `SshKeyPath`: Path to the private SSH key for authentication.

3. **Logging**  
   Configures the logging level for the application.

## How It Works

1. **Startup**  
   - The application reads the configuration from `appsettings.json`.
   - It initializes `FileSystemWatcher` instances for each folder specified in the `Monitor:Folders` section.

2. **File Monitoring**  
   - When a new file is created in a monitored folder, the application triggers the `ProcessFile` method.

3. **File Transfer**  
   - The `ProcessFile` method connects to the SFTP server using the provided credentials and uploads the file to the corresponding remote directory.

4. **Logging**  
   - Logs are written to the console, including information about file transfers and errors.

## Running the Application

1. Ensure the `appsettings.json` file is correctly configured.
2. Build and run the application using the following command:
   ```bash
   dotnet run
   ```

## Dependencies

- [.NET Core](https://dotnet.microsoft.com/)
- [Renci.SshNet](https://github.com/sshnet/SSH.NET)

## Error Handling

- If no folder mappings are found in the configuration, the application logs an error and exits.
- Errors during file transfer are logged with detailed exception messages.

## Extending the Application

To add new features or modify the behavior:
- Update the `Worker` class for custom file processing logic.
- Modify the `appsettings.json` structure as needed and update the corresponding code in `Worker`.

## License

This project is licensed under the MIT License.
