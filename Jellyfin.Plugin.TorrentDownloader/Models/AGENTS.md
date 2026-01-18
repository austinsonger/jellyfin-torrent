# Models Directory

## Purpose
This directory contains core data models, enumerations, and data transfer objects used throughout the plugin.

## Contents
- `DownloadEntry.cs`: Central entity representing a torrent download
- `DownloadStatus.cs`: Enumeration defining download lifecycle states
- `StorageStatus.cs`: Enumeration categorizing storage health levels
- `VolumeStatus.cs`: Model representing filesystem volume status
- `Dto/`: Subdirectory containing API DTO models

## Key Responsibilities
- **Data Structures**: Define core entities for download tracking and storage monitoring
- **State Management**: Represent download lifecycle through status enumerations
- **API Contracts**: Provide DTOs for request/response serialization
- **Validation**: Ensure data integrity through constraints and nullable reference types

## Core Models

### DownloadEntry
Represents a single torrent download with complete tracking information.

**Key Properties:**
- Identification: `DownloadId` (Guid), `TorrentSource`, `DisplayName`, `InfoHash`
- Status: `Status` (DownloadStatus enum)
- Progress: `TotalSize`, `DownloadedSize`, `ProgressPercent`
- Performance: `DownloadSpeed`, `UploadSpeed`, `PeerCount`, `EstimatedTimeRemaining`
- Paths: `StagingPath`, `TargetLibraryId`
- Timestamps: `CreatedAt`, `CompletedAt`, `ImportedAt`
- Metadata: `Trackers`, `ErrorMessage`, `CreatedByUserId`

**Usage Context:**
- Created by DownloadManager on new download
- Updated periodically with progress metrics
- Persisted to state file for recovery
- Mapped to DTOs for API responses

### DownloadStatus (Enumeration)
Defines the lifecycle states of a download.

**States:**
- `Queued`: Download queued but not started
- `Downloading`: Actively downloading
- `Paused`: Paused by user
- `Completed`: Download finished successfully
- `Failed`: Download failed with error
- `Importing`: Being imported into library
- `Imported`: Successfully imported

**State Transitions:**
- Queued → Downloading → Completed → Importing → Imported
- Any state → Paused (user action)
- Any state → Failed (on error)

### StorageStatus (Enumeration)
Categorizes storage health levels for disk space monitoring.

**Levels:**
- `Normal`: Storage at normal levels
- `Warning`: Below warning threshold
- `Critical`: Critically low, downloads paused

**Usage:**
- Set by StorageManager based on configuration thresholds
- Used to gate new downloads and trigger alerts

### VolumeStatus
Represents the status of a filesystem volume.

**Properties:**
- `VolumePath`: Volume root path
- `AvailableBytes`: Free space available
- `TotalBytes`: Total volume capacity
- `Status`: StorageStatus level
- `IsStagingVolume`: Flag indicating staging volume

**Usage:**
- Returned by StorageManager monitoring
- Aggregated in StorageStatusResponse
- Used for capacity planning and alerts

## Data Validation
- Non-negative numeric fields (sizes, speeds, counts)
- ProgressPercent bounded to 0–100
- Timestamps stored as UTC
- Nullable types for optional fields
- Guid validation for IDs

## Persistence Patterns
- DownloadEntry serialized to JSON for state persistence
- Configuration model persisted via Jellyfin framework
- VolumeStatus ephemeral, regenerated on each check

## Integration Points
- **DownloadManager**: Creates and updates DownloadEntry
- **TorrentEngine**: Provides progress metrics
- **StorageManager**: Monitors and updates VolumeStatus
- **ImportOrchestrator**: Updates import-related fields
- **TorrentsController**: Maps to API DTOs

## Development Notes
- All timestamps use UTC to avoid timezone issues
- Sizes stored in bytes, formatted for display
- Speeds in bytes/second, converted to KB/s or MB/s for UI
- Nullable reference types enabled for null safety
- Immutable after creation where appropriate
