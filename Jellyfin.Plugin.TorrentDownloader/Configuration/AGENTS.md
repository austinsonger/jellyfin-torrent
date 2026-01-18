# Configuration Directory

## Purpose
This directory contains the plugin configuration system, including the configuration model and web-based configuration UI.

## Contents
- `PluginConfiguration.cs`: Plugin configuration class with all user-configurable settings
- `configPage.html`: Web-based configuration interface for editing settings

## Key Responsibilities
- **Configuration Model**: Define and manage all plugin settings with defaults
- **Storage Settings**: Staging directory, storage thresholds (warning/critical/recovery), cleanup retention
- **Performance Settings**: Max concurrent downloads, download/upload speed limits, listen port
- **Protocol Settings**: DHT, PEX, encryption preferences
- **Import Settings**: Auto-import toggle, retry attempts/delays, default library selection
- **Web UI**: Provide user-friendly interface for configuration editing with validation

## Configuration Settings Categories

### Storage Configuration
- `StagingDirectory`: Absolute path where torrents are downloaded before import
- `StorageWarningThreshold`: Warn when disk space falls below (default: 10 GB)
- `StorageCriticalThreshold`: Pause downloads when below (default: 2 GB)
- `StorageRecoveryThreshold`: Resume downloads when above (default: 15 GB)
- `StorageCheckIntervalActive`: Check interval when downloads active (default: 60s)
- `StorageCheckIntervalIdle`: Check interval when idle (default: 300s)
- `RemoveAfterImport`: Delete staging files after successful import
- `CleanupRetentionDays`: Days to retain completed downloads before cleanup (default: 30)
- `EnableAutomaticCleanup`: Automatically clean up old downloads

### Performance Configuration
- `MaxConcurrentDownloads`: Maximum simultaneous downloads (default: 3)
- `MaxDownloadSpeed`: Global download speed limit in bytes/s (0 = unlimited)
- `MaxUploadSpeed`: Global upload speed limit in bytes/s (0 = unlimited)
- `ListenPort`: Port for incoming peer connections (default: 6881)

### Protocol Configuration
- `EnableDHT`: Enable DHT for trackerless torrents (default: true)
- `EnablePEX`: Enable peer exchange (default: true)
- `EnableEncryption`: Prefer encrypted peer connections (default: true)

### Import Configuration
- `AutoImportEnabled`: Automatically import completed downloads (default: true)
- `ImportRetryAttempts`: Number of retry attempts for failed imports (default: 3)
- `ImportRetryDelaySeconds`: Initial delay between retries with exponential backoff (default: 5s)
- `DefaultLibraryId`: Default library when type detection fails

## Integration Points
- **PluginConfiguration** extends `BasePluginConfiguration` from Jellyfin's plugin framework
- **configPage.html** uses Jellyfin's web API (`ApiClient.updatePluginConfiguration`) for persistence
- **Runtime Services** consume configuration:
  - `TorrentEngine`: Performance and protocol settings
  - `ImportOrchestrator`: Import settings and retry logic
  - `StorageManager`: Storage thresholds and monitoring intervals
  - `DownloadManager`: Queue management and concurrency limits

## Data Flow
1. User edits settings via configPage.html
2. JavaScript validates input and converts units (GB ↔ bytes, KB/s ↔ bytes/s)
3. Configuration persisted via Jellyfin's plugin framework
4. Services read configuration on initialization and runtime
5. Configuration changes take effect on service restart or next check cycle

## Validation and Defaults
- HTML inputs enforce constraints (min/max values, required fields)
- JavaScript performs unit conversion before persistence
- Missing/invalid values fall back to safe defaults
- Services validate configuration at runtime

## Development Notes
- All numeric thresholds stored in bytes internally, displayed in GB to users
- Speed limits stored in bytes/s internally, displayed in KB/s to users
- Configuration changes require plugin restart for some settings
- Storage monitoring adapts check interval based on active downloads
