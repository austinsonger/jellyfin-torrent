# Dto (Data Transfer Objects) Directory

## Purpose
This directory contains API-specific data transfer objects used for request/response serialization.

## Contents
- `ApiModels.cs`: All API DTO definitions for the plugin

## Key Responsibilities
- **API Contracts**: Define request and response schemas
- **Serialization**: Enable JSON serialization for API endpoints
- **Decoupling**: Separate internal models from external API surface
- **Validation**: Enforce required fields and data types

## Request DTOs

### CreateDownloadRequest
Request model for creating a new torrent download.

**Properties:**
- `Source` (string, required): Magnet link or .torrent file path
- `UserId` (Guid?, optional): User ID initiating download
- `TargetLibraryId` (Guid?, optional): Target library for import

**Usage:**
- Sent by client to `POST /api/torrents/download`
- Validated by TorrentsController
- Converted to internal DownloadEntry

### ControlDownloadRequest
Request model for controlling a download.

**Properties:**
- `Action` (string, required): Action to perform (pause/resume/cancel)

**Usage:**
- Sent by client to `POST /api/torrents/{id}/control`
- Action string mapped to DownloadManager method

## Response DTOs

### DownloadSummaryResponse
Summary information for a download.

**Properties:**
- `DownloadId`: Unique identifier
- `DisplayName`: Human-readable name
- `Status`: Current status string
- `ProgressPercent`: Completion percentage
- `DownloadSpeed`, `UploadSpeed`: Transfer speeds
- `PeerCount`: Connected peers
- `TotalSize`, `DownloadedSize`: Size metrics
- `EstimatedTimeRemaining`: ETA in seconds
- `CreatedAt`, `CompletedAt`: Timestamps

**Usage:**
- List endpoint returns array of summaries
- Provides overview without excessive detail

### DownloadDetailsResponse
Detailed information for a download (extends DownloadSummaryResponse).

**Additional Properties:**
- `TorrentSource`: Original magnet/file
- `InfoHash`: Torrent info hash
- `Trackers`: List of tracker URLs
- `StagingPath`: Download location
- `TargetLibraryId`: Import destination
- `ErrorMessage`: Error details if failed
- `ImportedAt`: Import timestamp
- `CreatedByUserId`: User who created download

**Usage:**
- Details endpoint returns full information
- Includes sensitive paths (admin-only)

### StorageStatusResponse
Aggregated storage status across volumes.

**Properties:**
- `Volumes`: Array of VolumeStatusResponse
- `IsStorageCritical`: Overall critical flag
- `LastCheckTime`: Last monitoring check

**Usage:**
- Storage status endpoint
- Provides system-wide storage health

### VolumeStatusResponse
Individual volume status.

**Properties:**
- `VolumePath`: Volume root path
- `AvailableBytes`: Free space
- `TotalBytes`: Total capacity
- `Status`: Health level (Normal/Warning/Critical)
- `IsStagingVolume`: Staging volume flag

**Usage:**
- Nested in StorageStatusResponse
- Per-volume capacity information

### CleanupResultResponse
Result of cleanup operation.

**Properties:**
- `FilesDeleted`: Number of files/directories removed
- `BytesFreed`: Space reclaimed in bytes

**Usage:**
- Cleanup endpoint response
- Provides feedback on cleanup effectiveness

### LibraryInfoResponse
Jellyfin library information.

**Properties:**
- `Id`: Library ID (Guid)
- `Name`: Display name
- `CollectionType`: Type (movies/tvshows/music)
- `Paths`: Array of library paths

**Usage:**
- Libraries endpoint returns array
- Used for target library selection in UI

## Mapping Patterns

### Internal Model → DTO
Controller methods map internal models to DTOs:
- `DownloadEntry` → `DownloadSummaryResponse`
- `DownloadEntry` → `DownloadDetailsResponse`
- `List<VolumeStatus>` → `StorageStatusResponse`
- `BaseItem` → `LibraryInfoResponse`

### DTO → Internal Model
Request DTOs validated and converted:
- `CreateDownloadRequest` → `DownloadEntry` creation
- `ControlDownloadRequest.Action` → Method selection

## Serialization
- All DTOs use JSON serialization via System.Text.Json
- Property names use PascalCase (C# convention)
- Nullable types for optional fields
- DateTime serialized as ISO 8601

## Validation Rules
- Required fields enforced by controller
- Numeric fields validated for non-negative values
- Guid fields validated for format
- Enum strings validated against allowed values

## Development Notes
- DTOs are intentionally simple with minimal logic
- All DTOs are public for serialization
- Avoid circular references in nested DTOs
- Keep DTOs focused on data structure, not behavior
- Update DTOs when API contract changes
