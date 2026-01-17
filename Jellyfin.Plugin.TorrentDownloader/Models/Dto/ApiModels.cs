using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.TorrentDownloader.Models.Dto
{
    /// <summary>
    /// Request model for creating a new torrent download.
    /// </summary>
    public class CreateDownloadRequest
    {
        /// <summary>
        /// Gets or sets the torrent source (magnet link or file path).
        /// </summary>
        public string TorrentSource { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user ID initiating the download.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the optional target library ID for import.
        /// </summary>
        public Guid? TargetLibraryId { get; set; }
    }

    /// <summary>
    /// Request model for controlling a download.
    /// </summary>
    public class ControlDownloadRequest
    {
        /// <summary>
        /// Gets or sets the control action to perform.
        /// </summary>
        public string Action { get; set; } = string.Empty; // "pause", "resume", "cancel"
    }

    /// <summary>
    /// Response model for download summary.
    /// </summary>
    public class DownloadSummaryResponse
    {
        /// <summary>
        /// Gets or sets the download ID.
        /// </summary>
        public Guid DownloadId { get; set; }

        /// <summary>
        /// Gets or sets the display name.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the current status.
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the progress percentage.
        /// </summary>
        public decimal ProgressPercent { get; set; }

        /// <summary>
        /// Gets or sets the download speed in bytes per second.
        /// </summary>
        public long DownloadSpeed { get; set; }

        /// <summary>
        /// Gets or sets the upload speed in bytes per second.
        /// </summary>
        public long UploadSpeed { get; set; }

        /// <summary>
        /// Gets or sets the peer count.
        /// </summary>
        public int PeerCount { get; set; }

        /// <summary>
        /// Gets or sets the total size in bytes.
        /// </summary>
        public long TotalSize { get; set; }

        /// <summary>
        /// Gets or sets the downloaded size in bytes.
        /// </summary>
        public long DownloadedSize { get; set; }

        /// <summary>
        /// Gets or sets the estimated time remaining in seconds.
        /// </summary>
        public long? EstimatedTimeRemaining { get; set; }

        /// <summary>
        /// Gets or sets the creation timestamp.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the completion timestamp.
        /// </summary>
        public DateTime? CompletedAt { get; set; }
    }

    /// <summary>
    /// Response model for detailed download information.
    /// </summary>
    public class DownloadDetailsResponse : DownloadSummaryResponse
    {
        /// <summary>
        /// Gets or sets the torrent source.
        /// </summary>
        public string TorrentSource { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the info hash.
        /// </summary>
        public string? InfoHash { get; set; }

        /// <summary>
        /// Gets or sets the list of trackers.
        /// </summary>
        public string[]? Trackers { get; set; }

        /// <summary>
        /// Gets or sets the staging path.
        /// </summary>
        public string StagingPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the target library ID.
        /// </summary>
        public Guid? TargetLibraryId { get; set; }

        /// <summary>
        /// Gets or sets the error message if failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the import timestamp.
        /// </summary>
        public DateTime? ImportedAt { get; set; }

        /// <summary>
        /// Gets or sets the user ID who created the download.
        /// </summary>
        public Guid CreatedByUserId { get; set; }
    }

    /// <summary>
    /// Response model for storage status.
    /// </summary>
    public class StorageStatusResponse
    {
        /// <summary>
        /// Gets or sets the list of volume statuses.
        /// </summary>
        public List<VolumeStatusResponse> Volumes { get; set; } = new List<VolumeStatusResponse>();

        /// <summary>
        /// Gets or sets a value indicating whether any volume is critical.
        /// </summary>
        public bool IsStorageCritical { get; set; }

        /// <summary>
        /// Gets or sets the last check timestamp.
        /// </summary>
        public DateTime LastCheckTime { get; set; }
    }

    /// <summary>
    /// Response model for volume status.
    /// </summary>
    public class VolumeStatusResponse
    {
        /// <summary>
        /// Gets or sets the volume path.
        /// </summary>
        public string VolumePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the available bytes.
        /// </summary>
        public long AvailableBytes { get; set; }

        /// <summary>
        /// Gets or sets the total bytes.
        /// </summary>
        public long TotalBytes { get; set; }

        /// <summary>
        /// Gets or sets the status level.
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether this is the staging volume.
        /// </summary>
        public bool IsStagingVolume { get; set; }
    }

    /// <summary>
    /// Response model for cleanup operation results.
    /// </summary>
    public class CleanupResultResponse
    {
        /// <summary>
        /// Gets or sets the number of files deleted.
        /// </summary>
        public int FilesDeleted { get; set; }

        /// <summary>
        /// Gets or sets the number of bytes freed.
        /// </summary>
        public long BytesFreed { get; set; }

        /// <summary>
        /// Gets or sets the list of errors encountered.
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();
    }

    /// <summary>
    /// Response model for library information.
    /// </summary>
    public class LibraryInfoResponse
    {
        /// <summary>
        /// Gets or sets the library ID.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the library name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the collection type (movies, tvshows, music, etc.).
        /// </summary>
        public string? CollectionType { get; set; }

        /// <summary>
        /// Gets or sets the library paths.
        /// </summary>
        public List<string> Paths { get; set; } = new List<string>();
    }
}
