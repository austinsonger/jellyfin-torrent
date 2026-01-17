# Jellyfin Torrent Downloader Plugin - Project Summary

## Overview

This is a Jellyfin plugin project that implements torrent downloading functionality directly within the Jellyfin media server. The plugin enables administrators to download content via magnet links or torrent files with automatic import into Jellyfin libraries.

## Project Structure

```
jellyfin-torrent/
├── .github/
│   └── workflows/
│       └── build.yml                    # CI/CD build configuration
├── .qoder/
│   └── quests/
│       └── jellyfin-torrent-download.md # Design document
├── Jellyfin.Plugin.TorrentDownloader/
│   ├── Configuration/
│   │   └── PluginConfiguration.cs       # Plugin settings model
│   ├── Controllers/                     # API controllers (TO IMPLEMENT)
│   ├── Models/
│   │   ├── DownloadEntry.cs            # Download data model
│   │   ├── DownloadStatus.cs           # Status enumeration
│   │   └── Dto/
│   │       └── ApiModels.cs            # API request/response models
│   ├── Services/                        # Core service implementations (TO IMPLEMENT)
│   │   ├── ITorrentEngine.cs           # Torrent engine interface ✓
│   │   └── IDownloadManager.cs         # Download manager interface ✓
│   ├── Web/                             # Web UI resources (TO IMPLEMENT)
│   ├── Plugin.cs                        # Main plugin class ✓
│   └── Jellyfin.Plugin.TorrentDownloader.csproj
├── Jellyfin.Plugin.TorrentDownloader.Tests/
│   └── Jellyfin.Plugin.TorrentDownloader.Tests.csproj
├── Jellyfin.Plugin.TorrentDownloader.sln
├── .gitignore
├── LICENSE                              # GPL-3.0 with legal disclaimer
├── README.md                            # User documentation
├── IMPLEMENTATION.md                    # Developer implementation guide
└── manifest.json                        # Plugin manifest for Jellyfin
```

## Completed Components ✅

### 1. Project Infrastructure
- ✅ Solution and project files configured
- ✅ NuGet package references added (Jellyfin SDK 10.8.*, MonoTorrent 3.0.2)
- ✅ Build workflow (GitHub Actions)
- ✅ Plugin manifest
- ✅ License with legal disclaimer
- ✅ .gitignore configuration

### 2. Core Models
- ✅ `DownloadStatus` enum with 7 states (Queued, Downloading, Paused, Completed, Failed, Importing, Imported)
- ✅ `DownloadEntry` model with all required fields (ID, source, status, progress, speeds, timestamps)
- ✅ `PluginConfiguration` with 11 configurable settings
- ✅ API DTO models (request/response objects)

### 3. Plugin Core
- ✅ Main `Plugin` class inheriting from `BasePlugin<PluginConfiguration>`
- ✅ Plugin metadata (ID, name, description)
- ✅ Web page registration (configuration and manager pages)

### 4. Service Interfaces
- ✅ `ITorrentEngine` interface with 8 methods
- ✅ `IDownloadManager` interface with 10 methods

### 5. Documentation
- ✅ Comprehensive README with installation, configuration, usage, API docs, troubleshooting
- ✅ IMPLEMENTATION.md with detailed implementation guidance and code samples
- ✅ Design document in `.qoder/quests/` directory

## Components Requiring Implementation 🚧

### 1. Service Implementations (High Priority)
- ⬜ `TorrentEngine.cs` - Wraps MonoTorrent's `ClientEngine`
- ⬜ `DownloadManager.cs` - Orchestrates download lifecycle and queue management
- ⬜ `ImportOrchestrator.cs` - Handles automatic library import after completion
- ⬜ `StorageManager.cs` - Manages staging directory and disk space
- ⬜ `MediaDetector.cs` - Detects media type from file extensions

### 2. API Controller (High Priority)
- ⬜ `TorrentsController.cs` with endpoints:
  - POST `/api/torrents/download` - Submit new download
  - GET `/api/torrents/list` - List all downloads
  - GET `/api/torrents/{id}` - Get download summary
  - GET `/api/torrents/{id}/details` - Get detailed info
  - POST `/api/torrents/{id}/control` - Control download (pause/resume/cancel)
  - DELETE `/api/torrents/{id}` - Delete download

### 3. Web UI (Medium Priority)
- ⬜ `Configuration/configPage.html` - Plugin settings page
- ⬜ `Web/torrentManager.html` - Main torrent management interface
- ⬜ `Web/torrentManager.js` - Client-side logic with API integration
- ⬜ `Web/torrentManager.css` - Styling

### 4. Security Components (High Priority)
- ⬜ Input validation utilities (magnet link, path sanitization)
- ⬜ Authorization attributes and middleware
- ⬜ Audit logging integration

### 5. Dependency Injection (High Priority)
- ⬜ Service registration in Plugin or startup class
- ⬜ Proper lifetime management (Singleton for engines/managers)

### 6. Testing (Medium Priority)
- ⬜ Unit tests for DownloadManager
- ⬜ Unit tests for TorrentEngine
- ⬜ Unit tests for ImportOrchestrator
- ⬜ Integration tests for API endpoints
- ⬜ Mock implementations for testing

## Key Technologies

- **Framework**: .NET 8.0
- **Jellyfin SDK**: 10.8.* (Controller, Model packages)
- **Torrent Library**: MonoTorrent 3.0.2
- **Testing**: xUnit, Moq, FluentAssertions
- **Build**: MSBuild, GitHub Actions

## Implementation Priorities

### Phase 1: Core Functionality (Highest Priority)
1. Implement `TorrentEngine` service with MonoTorrent integration
2. Implement `DownloadManager` with state persistence
3. Implement `TorrentsController` API endpoints
4. Implement basic input validation and security

### Phase 2: Integration (High Priority)
5. Implement `ImportOrchestrator` with Jellyfin library integration
6. Implement `StorageManager` for disk management
7. Set up dependency injection
8. Create API integration tests

### Phase 3: User Interface (Medium Priority)
9. Create configuration HTML page
10. Create torrent manager HTML page with JavaScript
11. Implement real-time progress updates
12. Add styling and UX improvements

### Phase 4: Quality & Testing (Medium Priority)
13. Write comprehensive unit tests
14. Add error handling and logging
15. Performance optimization
16. Security hardening

### Phase 5: Polish (Lower Priority)
17. Additional documentation
18. Example scripts
19. Advanced features from design document

## Quick Start for Development

### Prerequisites
- .NET 8.0 SDK installed
- Jellyfin server 10.8.x for testing
- IDE (Visual Studio, VS Code, Rider)

### Build Instructions
```bash
# Clone and navigate to project
cd /Users/olivebranch/dev/jellyfin-torrent

# Restore dependencies
dotnet restore

# Build solution
dotnet build --configuration Release

# Run tests (once implemented)
dotnet test

# Publish plugin
dotnet publish Jellyfin.Plugin.TorrentDownloader/Jellyfin.Plugin.TorrentDownloader.csproj \
  --configuration Release \
  --output ./bin/publish
```

### Installation for Testing
```bash
# Copy to Jellyfin plugins directory
cp -r ./bin/publish/* /var/lib/jellyfin/plugins/TorrentDownloader/

# Restart Jellyfin
sudo systemctl restart jellyfin
```

## Next Steps

1. **Immediate**: Implement `TorrentEngine` service
   - Review IMPLEMENTATION.md for MonoTorrent integration details
   - Start with basic download start/stop functionality
   - Add progress tracking

2. **Follow-up**: Implement `DownloadManager`
   - Add state persistence with JSON serialization
   - Implement queue processing logic
   - Connect to TorrentEngine

3. **Then**: Create API Controller
   - Implement all REST endpoints
   - Add authentication/authorization
   - Test with Postman or curl

## Design Document Reference

The complete design specification is available at:
`.qoder/quests/jellyfin-torrent-download.md`

Key sections:
- Architecture diagrams
- Functional requirements
- Data models
- Workflows
- Security considerations

## Support & Resources

- **Implementation Guide**: `IMPLEMENTATION.md` (detailed code examples)
- **User Documentation**: `README.md` (installation, usage, troubleshooting)
- **Design Document**: `.qoder/quests/jellyfin-torrent-download.md`
- **Jellyfin Docs**: https://jellyfin.org/docs/general/server/plugins/
- **MonoTorrent**: https://github.com/alanmcgovern/monotorrent

## License

GNU General Public License v3.0 with legal disclaimer regarding copyright compliance.

---

**Status**: Project scaffolding and architecture complete. Core service implementations ready to begin.
