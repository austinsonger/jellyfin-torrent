using System;

namespace Jellyfin.Plugin.TorrentDownloader.Models
{
    /// <summary>
    /// Represents a torrent download entry with all tracking information.
    /// </summary>
    public class DownloadEntry
    {
        /// <summary>
        /// Gets or sets the unique identifier for this download.
        /// </summary>
        public Guid DownloadId { get; set; }

        /// <summary>
        /// Gets or sets the torrent source (magnet link or torrent file path).
        /// </summary>
        public string TorrentSource { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the human-readable display name for this download.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the current download status.
        /// </summary>
        public DownloadStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the total size of the download in bytes.
        /// </summary>
        public long TotalSize { get; set; }

        /// <summary>
        /// Gets or sets the number of bytes downloaded so far.
        /// </summary>
        public long DownloadedSize { get; set; }

        /// <summary>
        /// Gets or sets the download completion percentage (0.00 to 100.00).
        /// </summary>
        public decimal ProgressPercent { get; set; }

        /// <summary>
        /// Gets or sets the current download speed in bytes per second.
        /// </summary>
        public long DownloadSpeed { get; set; }

        /// <summary>
        /// Gets or sets the current upload speed in bytes per second.
        /// </summary>
        public long UploadSpeed { get; set; }

        /// <summary>
        /// Gets or sets the number of connected peers.
        /// </summary>
        public int PeerCount { get; set; }

        /// <summary>
        /// Gets or sets the path in the staging directory.
        /// </summary>
        public string StagingPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the target Jellyfin library ID for import.
        /// </summary>
        public Guid? TargetLibraryId { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the download was created (UTC).
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the download completed (UTC).
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the download was imported (UTC).
        /// </summary>
        public DateTime? ImportedAt { get; set; }

        /// <summary>
        /// Gets or sets the error message if the download failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the ID of the administrator who created this download.
        /// </summary>
        public Guid CreatedByUserId { get; set; }

        /// <summary>
        /// Gets or sets the torrent info hash (for tracking).
        /// </summary>
        public string? InfoHash { get; set; }

        /// <summary>
        /// Gets or sets the list of trackers.
        /// </summary>
        public string[]? Trackers { get; set; }

        /// <summary>
        /// Gets or sets the estimated time remaining in seconds.
        /// </summary>
        public long? EstimatedTimeRemaining { get; set; }
    }
}
