# Services Directory

## Purpose
This directory contains the core business logic services that implement download orchestration, storage management, and library integration.

## Contents
- `IDownloadManager.cs`: Interface for download orchestration
- `DownloadManager.cs`: Download queue management and coordination
- `ITorrentEngine.cs`: Interface for torrent download operations
- `TorrentEngine.cs`: MonoTorrent integration for actual downloads
- `IImportOrchestrator.cs`: Interface for library import operations
- `ImportOrchestrator.cs`: Automated import into Jellyfin libraries
- `IStorageManager.cs`: Interface for storage monitoring
- `StorageManager.cs`: Disk space monitoring and cleanup

## Key Responsibilities
- **Download Orchestration**: Queue management, concurrency control, state persistence
- **Torrent Operations**: Integration with MonoTorrent for BitTorrent protocol
- **Import Automation**: Detect media type, select library, move files, trigger scan
- **Storage Monitoring**: Track disk space, enforce thresholds, cleanup operations

## Service Descriptions

### DownloadManager (IDownloadManager)
Central orchestrator for download lifecycle management.

**Responsibilities:**
- Queue management with concurrency limits
- State persistence to JSON file
- Progress tracking with periodic updates
- Coordination between TorrentEngine and ImportOrchestrator
- Download status transitions

**Key Methods:**
- `CreateDownloadAsync`: Add new download to queue
- `GetAllDownloadsAsync`: List downloads with optional filtering
- `GetDownloadAsync`: Retrieve single download by ID
- `PauseDownloadAsync`, `ResumeDownloadAsync`, `CancelDownloadAsync`: Control operations
- `CleanupDownloadAsync`: Remove download and optionally delete files

**Integration Points:**
- Uses ITorrentEngine for actual downloads
- Uses IImportOrchestrator for completed downloads
- Uses IStorageManager for space checks
- Persists state for recovery after restart

### TorrentEngine (ITorrentEngine)
MonoTorrent integration layer for BitTorrent protocol operations.

**Responsibilities:**
- Initialize MonoTorrent ClientEngine with configuration
- Start/stop/pause torrent downloads
- Retrieve real-time progress metrics
- Manage peer connections and tracker communication
- Configure DHT, PEX, encryption per settings

**Key Methods:**
- `StartDownloadAsync`: Begin torrent download
- `PauseDownloadAsync`, `ResumeDownloadAsync`, `StopDownloadAsync`: Control torrent
- `GetProgressAsync`: Retrieve current download metrics
- `Dispose`: Clean shutdown of torrent engine

**Configuration Integration:**
- Max concurrent downloads
- Download/upload speed limits
- DHT, PEX, encryption settings
- Listen port for incoming connections

### ImportOrchestrator (IImportOrchestrator)
Automated import of completed downloads into Jellyfin libraries.

**Responsibilities:**
- Media type detection (video/audio)
- Target library selection with fallback logic
- File movement from staging to library
- Library scan triggering
- Retry logic with exponential backoff
- Cleanup of staging files

**Key Methods:**
- `ImportDownloadAsync`: Import completed download to library

**Import Flow:**
1. Detect media type by file extensions
2. Select target library (explicit → type match → default → first available)
3. Check storage space on target volume
4. Move files from staging to library path
5. Trigger library scan for new content
6. Optionally cleanup staging directory

**Retry Logic:**
- Configurable retry attempts (default: 3)
- Exponential backoff delay (default: 5s initial)
- Logs retry attempts and failures

### StorageManager (IStorageManager)
Disk space monitoring and cleanup operations.

**Responsibilities:**
- Periodic volume status checks
- Threshold enforcement (warning/critical/recovery)
- Orphaned file cleanup
- Old download cleanup
- Multi-volume monitoring (staging + library volumes)

**Key Methods:**
- `Start`: Begin monitoring timer
- `StopMonitoring`: Stop monitoring
- `GetAllVolumesStatusAsync`: Get status of all volumes
- `HasSufficientSpaceAsync`: Check if space available for download
- `CleanupOrphanedFilesAsync`: Remove files without corresponding downloads
- `CleanupOldDownloadsAsync`: Remove downloads older than retention period

**Monitoring Behavior:**
- Check staging volume and all library volumes
- Adaptive check intervals (active vs idle)
- Log status changes (normal → warning → critical)
- Set critical flag to gate new downloads

**Storage Properties:**
- `IsStorageCritical`: Overall critical state
- `StagingVolumeAvailableBytes`: Free space on staging
- `StagingVolumeStatus`: Staging health level
- `LastCheckTime`: Last check timestamp

## Service Registration
Services registered in Plugin.cs via IPluginServiceRegistrator:
- `AddSingleton<IDownloadManager, DownloadManager>`
- `AddSingleton<ITorrentEngine, TorrentEngine>`
- `AddSingleton<IImportOrchestrator, ImportOrchestrator>`
- `AddSingleton<IStorageManager, StorageManager>`

## Dependency Injection
Services consume Jellyfin's built-in services:
- `ILibraryManager`: Library operations
- `IFileSystem`: File system abstraction
- `IUserManager`: User validation
- `ILogger<T>`: Structured logging

## Lifecycle Management
- Services initialized on plugin load
- TorrentEngine and DownloadManager start background tasks
- StorageManager starts monitoring timer
- Proper disposal via IDisposable
- State persistence for recovery

## Development Notes
- All services are async-first
- Comprehensive logging at all levels
- Thread-safe with SemaphoreSlim for concurrency
- Configuration read from TorrentDownloaderPlugin.Instance
- Error handling with graceful degradation
