# Jellyfin Plugin: Torrent Downloader

A Jellyfin plugin that enables torrent downloading functionality directly within the Jellyfin application. Download content via magnet links or torrent files with automatic import into your Jellyfin media library.

## ⚠️ Legal Disclaimer

**IMPORTANT**: This plugin is intended for downloading legally obtained content only. Users are solely responsible for ensuring that any content downloaded through this plugin complies with applicable copyright laws and regulations in their jurisdiction. The developers of this plugin assume no liability for any misuse or illegal activity conducted with this software.

## Features

- **Embedded Torrent Client**: Self-contained torrent downloading without external dependencies
- **Administrator-Only Access**: Secure access restricted to Jellyfin administrators
- **Automatic Library Import**: Completed downloads are automatically imported into appropriate Jellyfin libraries
- **Staging Directory**: Safe staging area for downloads before library integration
- **Download Management**: Full control with pause, resume, and cancel operations
- **Progress Monitoring**: Real-time download progress, speeds, and peer information
- **Configurable Settings**: Bandwidth limits, concurrent downloads, DHT/PEX support, and more

## Requirements

- Jellyfin Server 10.8.x or later
- .NET 8.0 Runtime
- Sufficient disk space for staging directory
- Network firewall configured to allow torrent traffic (default port: 6881)

## Installation

### Method 1: From Repository (Recommended)

1. Open Jellyfin Dashboard
2. Navigate to **Plugins** → **Repositories**
3. Add custom repository: `https://github.com/austinsonger/jellyfin-torrent/blob/main/manifest.json` 4. Go to **Catalog** and install **Torrent Downloader**
5. Restart Jellyfin

### Method 2: Manual Installation

1. Download the latest release from the [Releases page](https://github.com/austinsonger/jellyfin-torrent/releases)
2. Extract the ZIP file
3. Copy `Jellyfin.Plugin.TorrentDownloader.dll` to your Jellyfin plugins directory:
   - **Linux**: `/var/lib/jellyfin/plugins/TorrentDownloader/`
   - **Windows**: `%ProgramData%\Jellyfin\Server\plugins\TorrentDownloader\`
   - **macOS**: `/usr/local/var/jellyfin/plugins/TorrentDownloader/`
4. Restart Jellyfin server

## Configuration

Access plugin settings through: **Dashboard** → **Plugins** → **Torrent Downloader** → **Settings**

### Storage Settings

- **Staging Directory**: Path where torrents download before import (default: `/var/lib/jellyfin/torrents/staging`)
- **Storage Warning Threshold**: Minimum free disk space before warnings (default: 10 GB)
- **Remove After Import**: Automatically delete staging files after successful import (default: false)

### Performance Settings

- **Max Concurrent Downloads**: Maximum simultaneous downloads (default: 3)
- **Max Download Speed**: Global download speed limit in bytes/sec, 0=unlimited (default: 0)
- **Max Upload Speed**: Global upload speed limit in bytes/sec, 0=unlimited (default: 0)
- **Listen Port**: Port for incoming peer connections (default: 6881)

### Protocol Settings

- **Enable DHT**: Enable Distributed Hash Table for trackerless torrents (default: true)
- **Enable PEX**: Enable Peer Exchange (default: true)
- **Enable Encryption**: Prefer encrypted peer connections (default: true)

### Import Settings

- **Auto Import Enabled**: Automatically import completed downloads (default: true)

## Usage

### Accessing Torrent Manager

1. Log in to Jellyfin as an administrator
2. Navigate to **Dashboard** → **Torrent Manager**

### Adding a Torrent

1. In the Torrent Manager, locate the "Add Torrent" section
2. **Option A**: Paste a magnet link into the input field
3. **Option B**: Click "Upload File" and select a .torrent file
4. Click "Submit" to add the download

### Monitoring Downloads

The Torrent Manager displays:
- Download name and total size
- Progress bar with percentage complete
- Current download/upload speeds
- Number of connected peers
- Estimated time remaining
- Status (Queued, Downloading, Paused, Completed, Failed, Importing, Imported)

### Controlling Downloads

Each download has control buttons:
- **Pause**: Temporarily pause the download
- **Resume**: Resume a paused download
- **Cancel**: Stop and remove the download

### Viewing Details

Click any download to expand detailed information:
- Individual file list with progress
- Torrent metadata (info hash, trackers)
- Peer connection details
- Download path information
- Import status and target library

## API Documentation

The plugin exposes REST API endpoints for automation and advanced usage.

### Authentication

All endpoints require Jellyfin administrator authentication via API key or session token.

### Endpoints

#### POST `/api/torrents/download`
Submit a new torrent download.

**Request Body:**
```json
{
  "torrentSource": "magnet:?xt=urn:btih:..." or "file path",
  "userId": "user-guid"
}
```

**Response:**
```json
{
  "downloadId": "guid",
  "displayName": "torrent name",
  "status": "Queued"
}
```

#### GET `/api/torrents/list`
Get all downloads with optional status filter.

**Query Parameters:**
- `status` (optional): Filter by DownloadStatus enum value

**Response:**
```json
[
  {
    "downloadId": "guid",
    "displayName": "name",
    "status": "Downloading",
    "progressPercent": 45.67,
    "downloadSpeed": 1048576,
    "uploadSpeed": 524288,
    "peerCount": 12
  }
]
```

#### GET `/api/torrents/{id}`
Get download summary by ID.

#### GET `/api/torrents/{id}/details`
Get detailed download information including file list and peer details.

#### POST `/api/torrents/{id}/control`
Control a download (pause, resume, cancel).

**Request Body:**
```json
{
  "action": "pause" | "resume" | "cancel"
}
```

#### DELETE `/api/torrents/{id}`
Delete a download and optionally remove files.

**Query Parameters:**
- `deleteFiles` (optional, default: true): Whether to delete downloaded files

## Troubleshooting

### Downloads Not Starting

**Issue**: Torrents remain in "Queued" status.

**Solutions**:
- Check concurrent download limit in settings
- Verify staging directory is writable
- Check Jellyfin logs for errors
- Ensure sufficient disk space available

### No Peers Connecting

**Issue**: Download stuck at 0% with no peers.

**Solutions**:
- Enable DHT and PEX in plugin settings
- Check firewall allows traffic on listen port (default: 6881)
- Verify the torrent has active seeders
- Try a different torrent to rule out dead torrents

### Import Failures

**Issue**: Downloads complete but don't import to library.

**Solutions**:
- Verify auto-import is enabled in settings
- Check file permissions on library directories
- Review Jellyfin logs for import errors
- Manually trigger library scan if needed
- Ensure target library exists and is accessible

### Disk Space Issues

**Issue**: Downloads fail with disk space errors.

**Solutions**:
- Free up space on staging directory drive
- Adjust storage warning threshold
- Enable "Remove After Import" to auto-cleanup
- Manually clean staging directory via plugin UI

### Performance Problems

**Issue**: Jellyfin becomes slow during downloads.

**Solutions**:
- Reduce max concurrent downloads
- Set download/upload speed limits
- Reduce peer connection limits (requires code modification)
- Move staging directory to different drive

## Development

### Building from Source

```bash
# Clone repository
git clone https://github.com/austinsonger/jellyfin-torrent.git
cd jellyfin-torrent

# Restore dependencies
dotnet restore

# Build plugin
dotnet build --configuration Release

# Run tests
dotnet test
```

### Project Structure

```
Jellyfin.Plugin.TorrentDownloader/
├── Configuration/          # Plugin configuration
├── Controllers/            # API controllers
├── Models/                 # Data models
├── Services/              # Core services
│   ├── TorrentEngine.cs   # Torrent client wrapper
│   ├── DownloadManager.cs # Download orchestration
│   └── ImportOrchestrator.cs # Library import
├── Web/                   # Web UI resources
└── Plugin.cs              # Main plugin class
```

### Contributing

Contributions are welcome! Please:
1. Fork the repository
2. Create a feature branch
3. Make your changes with tests
4. Submit a pull request

## Security Considerations

- **Administrator Access Only**: The plugin enforces administrator-only access at multiple layers
- **Input Validation**: All torrent sources are validated before processing
- **Path Sanitization**: File paths are sanitized to prevent directory traversal attacks
- **Audit Logging**: All torrent operations are logged with user identity
- **Network Isolation**: Torrent traffic is isolated from Jellyfin admin interfaces

## Known Limitations

- Search functionality not yet implemented (manual torrent submission only)
- Library mapping is automatic based on file type detection
- No support for selective file downloading within torrents
- Seeding management features not yet implemented
- RSS automation not yet available

## Roadmap

- [ ] Torrent search and indexer integration (Jackett, Prowlarr)
- [ ] Advanced user permission system
- [ ] Smart content organization and renaming
- [ ] Seeding management and ratio controls
- [ ] RSS feed automation
- [ ] Mobile app integration
- [ ] Scheduled downloads

## Support

- **Issues**: [GitHub Issues](https://github.com/yourusername/jellyfin-torrent/issues)
- **Discussions**: [GitHub Discussions](https://github.com/yourusername/jellyfin-torrent/discussions)
- **Documentation**: [Wiki](https://github.com/yourusername/jellyfin-torrent/wiki)

## License

This project is licensed under the GNU General Public License v3.0 - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Jellyfin team for the excellent media server platform
- MonoTorrent project for the torrent client library
- Community contributors and testers

---

**Remember**: Only download content you have the legal right to obtain. Respect copyright laws in your jurisdiction.
