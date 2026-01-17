namespace Jellyfin.Plugin.TorrentDownloader.Models
{
    /// <summary>
    /// Represents the status of a storage volume.
    /// </summary>
    public class VolumeStatus
    {
        /// <summary>
        /// Gets or sets the volume path.
        /// </summary>
        public string VolumePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the available bytes on the volume.
        /// </summary>
        public long AvailableBytes { get; set; }

        /// <summary>
        /// Gets or sets the total bytes on the volume.
        /// </summary>
        public long TotalBytes { get; set; }

        /// <summary>
        /// Gets or sets the storage status level.
        /// </summary>
        public StorageStatus Status { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this is the staging volume.
        /// </summary>
        public bool IsStagingVolume { get; set; }
    }
}
