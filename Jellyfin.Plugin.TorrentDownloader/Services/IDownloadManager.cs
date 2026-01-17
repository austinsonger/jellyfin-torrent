using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Jellyfin.Plugin.TorrentDownloader.Models;

namespace Jellyfin.Plugin.TorrentDownloader.Services
{
    /// <summary>
    /// Interface for download manager operations.
    /// </summary>
    public interface IDownloadManager
    {
        /// <summary>
        /// Creates and queues a new download.
        /// </summary>
        /// <param name="torrentSource">Magnet link or torrent file path.</param>
        /// <param name="userId">User ID who initiated the download.</param>
        /// <returns>The created download entry.</returns>
        Task<DownloadEntry> CreateDownloadAsync(string torrentSource, Guid userId);

        /// <summary>
        /// Gets a download by ID.
        /// </summary>
        /// <param name="downloadId">The download ID.</param>
        /// <returns>The download entry or null if not found.</returns>
        Task<DownloadEntry?> GetDownloadAsync(Guid downloadId);

        /// <summary>
        /// Gets all downloads, optionally filtered by status.
        /// </summary>
        /// <param name="status">Optional status filter.</param>
        /// <returns>List of download entries.</returns>
        Task<IList<DownloadEntry>> GetAllDownloadsAsync(DownloadStatus? status = null);

        /// <summary>
        /// Updates the status of a download.
        /// </summary>
        /// <param name="downloadId">The download ID.</param>
        /// <param name="status">The new status.</param>
        /// <param name="errorMessage">Optional error message if failed.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task UpdateDownloadStatusAsync(Guid downloadId, DownloadStatus status, string? errorMessage = null);

        /// <summary>
        /// Pauses a download.
        /// </summary>
        /// <param name="downloadId">The download ID.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task PauseDownloadAsync(Guid downloadId);

        /// <summary>
        /// Resumes a paused download.
        /// </summary>
        /// <param name="downloadId">The download ID.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ResumeDownloadAsync(Guid downloadId);

        /// <summary>
        /// Cancels and removes a download.
        /// </summary>
        /// <param name="downloadId">The download ID.</param>
        /// <param name="deleteFiles">Whether to delete downloaded files.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CancelDownloadAsync(Guid downloadId, bool deleteFiles = true);

        /// <summary>
        /// Processes the download queue and starts downloads up to concurrent limit.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ProcessQueueAsync();

        /// <summary>
        /// Persists all download state to storage.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SaveStateAsync();

        /// <summary>
        /// Loads download state from storage.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task LoadStateAsync();
    }
}
