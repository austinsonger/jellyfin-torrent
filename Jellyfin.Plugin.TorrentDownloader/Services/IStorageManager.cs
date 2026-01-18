using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Jellyfin.Plugin.TorrentDownloader.Models;

namespace Jellyfin.Plugin.TorrentDownloader.Services
{
    /// <summary>
    /// Interface for storage monitoring and management.
    /// </summary>
    public interface IStorageManager
    {
        /// <summary>
        /// Gets a value indicating whether storage is critically low.
        /// </summary>
        bool IsStorageCritical { get; }

        /// <summary>
        /// Gets the available bytes on the staging volume.
        /// </summary>
        long StagingVolumeAvailableBytes { get; }

        /// <summary>
        /// Gets the staging volume status.
        /// </summary>
        StorageStatus StagingVolumeStatus { get; }

        /// <summary>
        /// Gets the last check timestamp.
        /// </summary>
        DateTime LastCheckTime { get; }

        /// <summary>
        /// Gets the list of all monitored volumes with their status.
        /// </summary>
        /// <returns>List of volume statuses.</returns>
        Task<IList<VolumeStatus>> GetAllVolumesStatusAsync();

        /// <summary>
        /// Checks if there is sufficient space on the staging volume for a download.
        /// </summary>
        /// <param name="requiredBytes">Required space in bytes.</param>
        /// <returns>True if sufficient space is available.</returns>
        Task<bool> HasSufficientSpaceAsync(long requiredBytes);

        /// <summary>
        /// Performs cleanup of orphaned files in the staging directory.
        /// </summary>
        /// <param name="validDownloadIds">List of valid download IDs to preserve.</param>
        /// <returns>Tuple of files deleted count and bytes freed.</returns>
        Task<(int filesDeleted, long bytesFreed)> CleanupOrphanedFilesAsync(IEnumerable<Guid> validDownloadIds);

        /// <summary>
        /// Performs cleanup of old completed downloads.
        /// </summary>
        /// <param name="olderThanDate">Delete downloads completed before this date.</param>
        /// <returns>Tuple of files deleted count and bytes freed.</returns>
        Task<(int filesDeleted, long bytesFreed)> CleanupOldDownloadsAsync(DateTime olderThanDate);

        /// <summary>
        /// Starts the storage monitoring service.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the storage monitoring service.
        /// </summary>
        void StopMonitoring();
    }
}
