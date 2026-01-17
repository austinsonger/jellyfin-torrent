# Implementation Guide

This document provides implementation guidance for completing the Jellyfin Torrent Downloader plugin based on the design document.

## Project Status

### ✅ Completed Components

1. **Project Structure**
   - Solution file and project files created
   - NuGet package references configured (Jellyfin SDK, MonoTorrent)
   - Directory structure established

2. **Core Models**
   - `DownloadStatus` enum
   - `DownloadEntry` model class
   - `PluginConfiguration` class

3. **Plugin Core**
   - Main `Plugin` class inheriting from BasePlugin
   - Configuration management integration

4. **Service Interfaces**
   - `ITorrentEngine` interface
   - `IDownloadManager` interface

5. **Documentation**
   - Comprehensive README.md
   - Build configuration (GitHub Actions)
   - Plugin manifest

### 🚧 Components Requiring Implementation

The following components have interfaces defined but need full implementations:

## 1. TorrentEngine Service Implementation

**File**: `Jellyfin.Plugin.TorrentDownloader/Services/TorrentEngine.cs`

**Key Requirements**:
- Wrap MonoTorrent's `ClientEngine` class
- Initialize with plugin configuration (DHT, PEX, encryption, listen port)
- Implement `TorrentManager` per download with state tracking
- Handle magnet link and .torrent file loading
- Track download progress with `PieceHashed` event handlers
- Implement pause/resume using `PauseAsync`/`StartAsync`
- Save/restore torrent state using `SaveFastResumeAsync`/`LoadFastResumeAsync`
- Validate torrents before starting

**MonoTorrent Integration Points**:
```csharp
using MonoTorrent;
using MonoTorrent.Client;

private ClientEngine _engine;
private Dictionary<Guid, TorrentManager> _managers;

// Initialize engine with settings
var engineSettings = new EngineSettings
{
    ListenPort = config.ListenPort,
    DhtPort = config.EnableDHT ? config.ListenPort : 0,
    MaximumDownloadSpeed = config.MaxDownloadSpeed,
    MaximumUploadSpeed = config.MaxUploadSpeed
};
```

## 2. DownloadManager Service Implementation

**File**: `Jellyfin.Plugin.TorrentDownloader/Services/DownloadManager.cs`

**Key Requirements**:
- Maintain in-memory collection of `DownloadEntry` objects
- Persist state to JSON file in plugin data directory
- Implement queue processing logic respecting `MaxConcurrentDownloads`
- Coordinate with `ITorrentEngine` for actual downloads
- Update download progress on timer (every 1-2 seconds)
- Handle download completion events
- Trigger `IImportOrchestrator` when downloads complete

**State Persistence**:
```csharp
private readonly string _stateFilePath;
private List<DownloadEntry> _downloads;
private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

// Save to: {PluginDataPath}/downloads.json
await File.WriteAllTextAsync(_stateFilePath, 
    JsonSerializer.Serialize(_downloads));
```

## 3. ImportOrchestrator Service Implementation

**File**: `Jellyfin.Plugin.TorrentDownloader/Services/ImportOrchestrator.cs`

**Key Requirements**:
- Monitor for completed downloads
- Analyze file extensions to detect media type (video, audio, etc.)
- Query Jellyfin's `ILibraryManager` for appropriate target library
- Move/copy files from staging to library directory
- Preserve directory structure for TV shows
- Trigger library refresh via `ILibraryManager.ValidateMediaLibrary`
- Handle import errors gracefully
- Optionally clean staging directory based on config

**Jellyfin Integration**:
```csharp
private readonly ILibraryManager _libraryManager;
private readonly IFileSystem _fileSystem;

// Detect media type from extensions
var videoExtensions = new[] { ".mp4", ".mkv", ".avi", ".mov" };
var audioExtensions = new[] { ".mp3", ".flac", ".m4a" };

// Find target library
var libraries = _libraryManager.GetVirtualFolders();
var targetLibrary = libraries.FirstOrDefault(l => 
    l.CollectionType == "movies" || l.CollectionType == "tvshows");
```

## 4. Storage Management Service

**File**: `Jellyfin.Plugin.TorrentDownloader/Services/StorageManager.cs`

**Key Requirements**:
- Create staging directory if not exists
- Monitor disk space using `DriveInfo`
- Warn when below threshold
- Provide cleanup methods for old/orphaned files
- Create download-specific subdirectories
- Sanitize paths to prevent traversal attacks

**Implementation**:
```csharp
public class StorageManager
{
    public async Task<bool> EnsureStagingDirectoryAsync(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        
        // Check permissions
        var testFile = Path.Combine(path, ".write_test");
        await File.WriteAllTextAsync(testFile, "test");
        File.Delete(testFile);
        return true;
    }
    
    public long GetAvailableSpace(string path)
    {
        var drive = new DriveInfo(Path.GetPathRoot(path));
        return drive.AvailableFreeSpace;
    }
}
```

## 5. API Controller Implementation

**File**: `Jellyfin.Plugin.TorrentDownloader/Controllers/TorrentsController.cs`

**Key Requirements**:
- Inherit from `BaseJellyfinApiController`
- Apply `[Authorize(Policy = "RequiresElevation")]` for admin-only
- Inject `IDownloadManager`, `IUserManager`, `ILogger`
- Implement all REST endpoints per design
- Return appropriate HTTP status codes
- Validate inputs and sanitize

**Controller Structure**:
```csharp
[ApiController]
[Route("api/torrents")]
[Authorize(Policy = "RequiresElevation")]
public class TorrentsController : BaseJellyfinApiController
{
    private readonly IDownloadManager _downloadManager;
    private readonly IUserManager _userManager;
    private readonly ILogger<TorrentsController> _logger;
    
    [HttpPost("download")]
    public async Task<ActionResult<DownloadEntry>> CreateDownload(
        [FromBody] CreateDownloadRequest request)
    {
        var userId = User.GetUserId();
        var download = await _downloadManager.CreateDownloadAsync(
            request.TorrentSource, userId);
        return Ok(download);
    }
}
```

## 6. Web UI Implementation

### Configuration Page
**File**: `Jellyfin.Plugin.TorrentDownloader/Configuration/configPage.html`

Use Jellyfin's standard plugin configuration template:
```html
<!DOCTYPE html>
<html>
<head>
    <title>Torrent Downloader Settings</title>
</head>
<body>
    <div data-role="page" class="page type-interior pluginConfigurationPage">
        <div data-role="content">
            <form class="torrentConfigForm">
                <div class="inputContainer">
                    <label for="stagingDirectory">Staging Directory:</label>
                    <input type="text" id="stagingDirectory" name="StagingDirectory" />
                </div>
                <!-- Add all configuration fields -->
                <button type="submit">Save</button>
            </form>
        </div>
    </div>
    <script type="text/javascript">
        // Load and save configuration using ApiClient
    </script>
</body>
</html>
```

### Torrent Manager Page
**File**: `Jellyfin.Plugin.TorrentDownloader/Web/torrentManager.html`

Implement full UI with:
- Add torrent form (magnet input, file upload)
- Downloads grid with real-time updates
- Progress bars using HTML5 `<progress>` element
- Control buttons for each download
- Polling mechanism to refresh every 2 seconds

## 7. Security Implementations

### Input Validation
**File**: `Jellyfin.Plugin.TorrentDownloader/Security/InputValidator.cs`

```csharp
public static class InputValidator
{
    public static bool IsValidMagnetLink(string magnetLink)
    {
        return magnetLink.StartsWith("magnet:?xt=urn:btih:", 
            StringComparison.OrdinalIgnoreCase);
    }
    
    public static string SanitizePath(string path)
    {
        path = path.Replace("..", string.Empty);
        path = Path.GetFullPath(path);
        return path;
    }
}
```

### Audit Logging
Wrap all operations with audit log entries:
```csharp
_logger.LogInformation(
    "User {UserId} initiated torrent download: {TorrentSource}",
    userId, torrentSource);
```

## 8. Dependency Injection Setup

**File**: `Jellyfin.Plugin.TorrentDownloader/ServiceRegistration.cs`

```csharp
public static class ServiceRegistration
{
    public static void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<ITorrentEngine, TorrentEngine>();
        services.AddSingleton<IDownloadManager, DownloadManager>();
        services.AddSingleton<IImportOrchestrator, ImportOrchestrator>();
        services.AddSingleton<IStorageManager, StorageManager>();
    }
}
```

Register in plugin initialization or use Jellyfin's DI discovery.

## 9. Testing Implementation

### Unit Tests Structure

**DownloadManager Tests**:
- Test queue management
- Test concurrent limit enforcement
- Test state persistence
- Mock ITorrentEngine

**TorrentEngine Tests**:
- Test magnet link validation
- Test start/stop/pause operations
- Mock MonoTorrent components

**ImportOrchestrator Tests**:
- Test media type detection
- Test file operations
- Mock ILibraryManager

## 10. Build and Deployment

### Local Development
```bash
# Restore and build
dotnet restore
dotnet build

# Run tests
dotnet test

# Publish for deployment
dotnet publish --configuration Release --output ./bin/Release/net8.0/publish
```

### Installation
Copy publish output to Jellyfin plugins directory:
```bash
cp -r ./bin/Release/net8.0/publish/* \
    /var/lib/jellyfin/plugins/TorrentDownloader/
```

## Next Steps

1. Implement TorrentEngine with MonoTorrent integration
2. Implement DownloadManager with state management
3. Implement ImportOrchestrator with library integration
4. Create API Controller with all endpoints
5. Build Web UI pages
6. Write comprehensive unit tests
7. Test end-to-end with real Jellyfin instance
8. Create installer/package

## Key Jellyfin SDK References

- **ILibraryManager**: Library operations and scanning
- **IUserManager**: User authentication and authorization
- **IFileSystem**: Cross-platform file operations
- **ILogger**: Logging infrastructure
- **BaseJellyfinApiController**: API controller base class

## MonoTorrent Key Classes

- **ClientEngine**: Main torrent engine
- **TorrentManager**: Per-torrent management
- **TorrentSettings**: Per-torrent configuration
- **EngineSettings**: Global engine configuration
- **MagnetLink**: Magnet link parsing

## Additional Resources

- [Jellyfin Plugin Development Guide](https://jellyfin.org/docs/general/server/plugins/)
- [MonoTorrent Documentation](https://github.com/alanmcgovern/monotorrent)
- [Design Document](./.qoder/quests/jellyfin-torrent-download.md)
