# Controllers Directory

## Purpose
This directory contains API controllers that expose REST endpoints for torrent download management.

## Contents
- `TorrentsController.cs`: Main API controller for torrent operations

## Key Responsibilities
- **API Endpoint Management**: Expose REST API for download operations
- **Request Validation**: Validate torrent sources, user authentication, and request parameters
- **Authorization**: Enforce administrator-only access via `RequiresElevation` policy
- **Service Orchestration**: Coordinate between DownloadManager, StorageManager, and LibraryManager
- **Response Mapping**: Convert internal models to API DTOs

## API Endpoints

### Download Management
- `POST /api/torrents/download`: Create new download from magnet link or .torrent file
- `GET /api/torrents/list`: List all downloads with optional status filter
- `GET /api/torrents/{id}`: Get download summary by ID
- `GET /api/torrents/{id}/details`: Get detailed download information

### Download Control
- `POST /api/torrents/{id}/control`: Control download (pause/resume/cancel)
- `DELETE /api/torrents/{id}`: Delete download with optional file removal

### Storage Management
- `GET /api/torrents/storage/status`: Get storage status across all volumes
- `POST /api/torrents/storage/cleanup`: Trigger manual cleanup of orphaned/old files

### Library Management
- `GET /api/torrents/libraries`: List available Jellyfin libraries

## Authentication & Authorization
- All endpoints require authentication via `[Authorize]`
- Administrator privileges required via `[Authorize(Policy = "RequiresElevation")]`
- Authentication token passed in `X-Emby-Token` header
- User context validated for download creation

## Request/Response Flow

### Creating a Download
1. Client sends POST to `/api/torrents/download` with torrent source
2. Controller validates source (magnet or .torrent file)
3. Validates user exists in Jellyfin user manager
4. Calls DownloadManager.CreateDownloadAsync
5. Returns DownloadSummaryResponse with 200 OK or 400/404 on error

### Controlling Downloads
1. Client sends POST to `/api/torrents/{id}/control` with action
2. Controller validates download exists
3. Calls appropriate DownloadManager method (Pause/Resume/Cancel)
4. Returns 204 No Content on success or 404 if not found

### Storage Status
1. Client requests GET `/api/torrents/storage/status`
2. Controller calls StorageManager.GetAllVolumesStatusAsync
3. Maps VolumeStatus to StorageStatusResponse
4. Returns aggregated storage information with critical flag

## Error Handling
- 400 Bad Request: Invalid input (missing source, invalid action)
- 404 Not Found: Download or user not found
- 500 Internal Server Error: Unhandled exceptions (logged)

## DTO Mapping
Controller converts internal models to API-friendly DTOs:
- `DownloadEntry` → `DownloadSummaryResponse`
- `DownloadEntry` → `DownloadDetailsResponse` (extended info)
- `VolumeStatus[]` → `StorageStatusResponse`
- `BaseItem` → `LibraryInfoResponse`

## Service Dependencies
- `IDownloadManager`: Core download orchestration
- `IUserManager`: User authentication and validation
- `IStorageManager`: Storage monitoring and cleanup
- `ILibraryManager`: Library enumeration and metadata
- `ILogger<TorrentsController>`: Structured logging

## Development Notes
- Route prefix: `/api/torrents`
- All responses use JSON serialization
- Async/await pattern throughout
- Proper HTTP status codes for all scenarios
- Comprehensive logging for debugging
- Null-safe operations with nullable reference types
