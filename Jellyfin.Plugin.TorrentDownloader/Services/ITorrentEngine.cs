using System;
using System.Threading.Tasks;
using Jellyfin.Plugin.TorrentDownloader.Models;

namespace Jellyfin.Plugin.TorrentDownloader.Services
{
    /// <summary>
    /// Interface for torrent engine operations.
    /// </summary>
    public interface ITorrentEngine
    {
        /// <summary>
        /// Starts a torrent download from a magnet link or torrent file.
        /// </summary>
        /// <param name="downloadEntry">The download entry to start.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task StartDownloadAsync(DownloadEntry downloadEntry);

        /// <summary>
        /// Pauses an active download.
        /// </summary>
        /// <param name="downloadId">The download ID to pause.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task PauseDownloadAsync(Guid downloadId);

        /// <summary>
        /// Resumes a paused download.
        /// </summary>
        /// <param name="downloadId">The download ID to resume.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ResumeDownloadAsync(Guid downloadId);

        /// <summary>
        /// Stops and removes a download.
        /// </summary>
        /// <param name="downloadId">The download ID to stop.</param>
        /// <param name="deleteFiles">Whether to delete downloaded files.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task StopDownloadAsync(Guid downloadId, bool deleteFiles);

        /// <summary>
        /// Gets the current progress of a download.
        /// </summary>
        /// <param name="downloadId">The download ID to check.</param>
        /// <returns>Updated download entry with current progress.</returns>
        Task<DownloadEntry?> GetDownloadProgressAsync(Guid downloadId);

        /// <summary>
        /// Validates a torrent source (magnet link or file).
        /// </summary>
        /// <param name="torrentSource">The torrent source to validate.</param>
        /// <returns>True if valid, false otherwise.</returns>
        Task<bool> ValidateTorrentSourceAsync(string torrentSource);

        /// <summary>
        /// Initializes the torrent engine with configuration.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task InitializeAsync();

        /// <summary>
        /// Shuts down the torrent engine gracefully.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ShutdownAsync();
    }
}
