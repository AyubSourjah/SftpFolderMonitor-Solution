# Copilot Instructions for SftpFolderMonitor

Use these repo-specific rules when proposing code. Keep changes aligned with the existing patterns and DI setup.

## Big picture
- This is a .NET Generic Host worker (not web). Entry: `SftpFolderMonitor/Program.cs` creates a host, reads `appsettings.json` from `AppContext.BaseDirectory`, configures Serilog, registers services, and runs a single `BackgroundService` (`Worker`).
- Components:
  - `Worker` orchestrates: reads config, starts/stops monitoring, handles `FileDetected` to trigger SFTP upload.
  - `FileMonitorService` wraps `FileSystemWatcher` with debounce and file-readiness checks.
  - `SftpService` wraps `Renci.SshNet` for SFTP uploads with connection reuse, concurrency control, and retry.
  - `SftpConfig` holds SFTP settings and validates them at startup.

## Configuration and conventions
- Configuration lives in `appsettings.json` next to the executable. Sections:
  - `Monitor:Root` (base local path), `Monitor:Folders` (map of local subfolder -> remote folder).
  - `Sftp` (host/port/user/auth). `AuthenticationMethod` is `password` or `privateKey` (lower/upper case tolerated). `UploadRetryDelaySeconds` configures retry backoff.
- `Program.cs` binds `SftpConfig` from `Sftp` and calls `Validate()` before registering it as a singleton. Fail fast on invalid config.
- Logging is Serilog-driven via configuration; always use `ILogger<T>` for logs inside services.
- DI lifetimes: `IFileMonitorService` and `ISftpService` are registered as singletons. Keep that when adding/replacing implementations unless there is a strong reason otherwise.

## Data flows
- File events: `FileMonitorService` raises `FileDetected(string filePath, string remoteFolder, CancellationToken)` after debounce and readiness checks → `Worker.HandleFileDetected` awaits `ISftpService.UploadFileAsync`.
- SFTP upload: `SftpService.UploadFileAsync` ensures a connected `SftpClient` (guarded by a `SemaphoreSlim`), uploads, logs, and retries up to 3 times with delay; on retry it disposes and recreates the client.

## Patterns to follow
- Respect cancellation tokens; never swallow `OperationCanceledException` unless rethrowing. See `SftpService` and `FileMonitorService` for examples.
- Avoid duplicate work on noisy file events; use debounce like `FileMonitorService` (`_debounceTokens` + `Task.Delay`).
- When accessing files that might still be written, use a readiness loop like `WaitForFileReadyAsync` (open with `FileShare.None`).
- For SFTP, prefer creating/disposing `SftpClient` via `EnsureConnectedAsync` and do not share it without the semaphore.
- Use forward slashes for remote paths and keep the original filename (`Path.GetFileName`).

## Build/run workflows
- Build: `dotnet build SftpFolderMonitor/SftpFolderMonitor.csproj -c Release`
- Run (console): `dotnet run --project SftpFolderMonitor` (reads `appsettings.json` from the build output dir)
- Logs: Serilog writes to console and `Logs/service.log` per config.

## Extending the app (examples)
- Add a new monitored mapping: update `Monitor:Folders` in `appsettings.json`; no code change needed.
- Add a new operation (e.g., SFTP download):
  - Extend `ISftpService` and implement in `SftpService` using the same connection/locking pattern and logging.
  - Register via DI in `Program.cs` (keep singleton) and call from `Worker` or a new background task.
- Add custom filtering (skip temp files): implement checks in `ScheduleDebounced` before scheduling, or in `OnFileDetected` prior to raising the event.

## Gotchas
- `Program.cs` sets base path to `AppContext.BaseDirectory`; ensure `appsettings.json` is deployed alongside the executable, not only in source.
- Windows-style local paths are used in the sample config; normalize or document platform expectations before changing.
- Do not block the thread in `BackgroundService` handlers; prefer `async`/`await` and offload synchronous SFTP calls via `Task.Run` as shown.

## Key files
- `Program.cs`, `Worker.cs`, `FileMonitorService.cs`, `SftpService.cs`, `SftpConfig.cs`, `Contracts/*`, `appsettings.json`

When in doubt, mirror the logging, cancellation, and retry patterns used in `SftpService` and `FileMonitorService`.