# Web Directory

## Purpose
This directory contains embedded web assets for the plugin's user interface.

## Contents
- `torrentManager.html`: Torrent manager web interface

## Key Responsibilities
- **User Interface**: Provide web-based UI for managing torrents
- **Real-time Updates**: Poll API for download progress updates
- **Download Control**: Add, pause, resume, cancel, delete downloads
- **Storage Monitoring**: Display storage status and capacity
- **Library Selection**: Allow users to select target libraries

## Torrent Manager Interface

### Features
- **Download List**: Display all downloads with status, progress, speeds
- **Filtering**: Filter by status (all/active/completed/failed)
- **Sorting**: Sort by creation time, name, progress, size
- **Add Download**: Dialog for adding magnet links or .torrent files
- **Download Control**: Pause, resume, cancel, delete actions
- **Details Panel**: Detailed information for selected download
- **Storage Indicator**: Visual storage status with capacity bar

### UI Components

#### Download List
- Display name, status badge, progress bar
- Download/upload speeds, peer count
- Size, ETA, timestamps
- Action buttons (pause/resume/cancel/delete)
- Click row to view details

#### Storage Indicator
- Color-coded status (green/orange/red)
- Progress bar showing used capacity
- Available/total space in GB
- Real-time updates

#### Add Download Dialog
- Textarea for magnet link or file path
- Library selector (optional, auto-detect if not specified)
- Validation before submission

#### Details Panel
- Download ID, torrent source
- Staging path, info hash
- Creation, completion, import timestamps
- Error messages (if failed)
- Tracker list

### API Integration
All interactions go through REST API:
- `GET /api/torrents/list`: Fetch downloads
- `POST /api/torrents/download`: Create download
- `GET /api/torrents/{id}/details`: Get details
- `POST /api/torrents/{id}/control`: Control download
- `DELETE /api/torrents/{id}`: Delete download
- `GET /api/torrents/storage/status`: Storage status
- `GET /api/torrents/libraries`: List libraries

### Real-time Updates
- Polling interval: 2 seconds (configurable)
- Automatic refresh when tab visible
- Pause polling when tab hidden (performance optimization)
- Manual refresh button

### Status Visualization
Color-coded status badges:
- **Queued**: Blue
- **Downloading**: Green (animated)
- **Paused**: Orange
- **Completed**: Green
- **Importing**: Purple
- **Imported**: Green
- **Failed**: Red

### User Experience
- Responsive layout for different screen sizes
- Hover effects for interactive elements
- Confirmation dialogs for destructive actions
- Progress bars with percentage display
- Human-readable formatting (sizes, speeds, ETA)

### Authentication
- Uses Jellyfin's ApiClient for authentication
- Token passed in X-Emby-Token header
- Integrated with Jellyfin's dashboard navigation

### Error Handling
- Display error messages in UI
- Log errors to console for debugging
- Graceful degradation on API failures
- User-friendly error messages

## Integration with Jellyfin

### Page Registration
- Registered as plugin page in Plugin.cs
- Accessible from Jellyfin dashboard menu
- Uses Jellyfin's page lifecycle (pageshow/pagehide events)

### Styling
- Uses Jellyfin's CSS framework
- emby-button, emby-input, emby-select components
- Consistent with Jellyfin's design language
- Custom styles for torrent-specific elements

### Dependencies
- Jellyfin's Dashboard API (Dashboard.showLoadingMsg, etc.)
- ApiClient for authenticated requests
- emby-* web components

## Development Notes
- Vanilla JavaScript (no framework dependencies)
- ES6+ features (arrow functions, template literals, fetch API)
- HTML5 semantic markup
- CSS3 for styling and animations
- Self-contained (no external libraries)
- Minification not applied (readable for debugging)

## Accessibility
- Semantic HTML for screen readers
- Keyboard navigation support
- ARIA labels where appropriate
- Color contrast for readability

## Performance Optimizations
- Efficient DOM updates (rebuild only on changes)
- Pause polling when tab hidden
- Lazy load details on demand
- Minimal re-renders
