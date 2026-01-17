namespace Jellyfin.Plugin.TorrentDownloader.Models
{
    /// <summary>
    /// Represents the current status of a torrent download.
    /// </summary>
    public enum DownloadStatus
    {
        /// <summary>
        /// Download is queued but not yet started.
        /// </summary>
        Queued,

        /// <summary>
        /// Download is actively downloading.
        /// </summary>
        Downloading,

        /// <summary>
        /// Download has been paused by user.
        /// </summary>
        Paused,

        /// <summary>
        /// Download has completed successfully.
        /// </summary>
        Completed,

        /// <summary>
        /// Download has failed with an error.
        /// </summary>
        Failed,

        /// <summary>
        /// Download is being imported into Jellyfin library.
        /// </summary>
        Importing,

        /// <summary>
        /// Download has been successfully imported into library.
        /// </summary>
        Imported
    }
}
