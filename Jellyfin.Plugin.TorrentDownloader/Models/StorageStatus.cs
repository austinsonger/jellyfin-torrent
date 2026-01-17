namespace Jellyfin.Plugin.TorrentDownloader.Models
{
    /// <summary>
    /// Represents the storage status level.
    /// </summary>
    public enum StorageStatus
    {
        /// <summary>
        /// Storage is at normal levels.
        /// </summary>
        Normal,

        /// <summary>
        /// Storage is below warning threshold.
        /// </summary>
        Warning,

        /// <summary>
        /// Storage is critically low.
        /// </summary>
        Critical
    }
}
