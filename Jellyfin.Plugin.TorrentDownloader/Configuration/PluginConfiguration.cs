using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.TorrentDownloader.Configuration
{
    /// <summary>
    /// Plugin configuration class containing all user-configurable settings.
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        /// <summary>
        /// Gets or sets the absolute path to the staging directory.
        /// </summary>
        public string StagingDirectory { get; set; } = "/var/lib/jellyfin/torrents/staging";

        /// <summary>
        /// Gets or sets the maximum number of concurrent downloads.
        /// </summary>
        public int MaxConcurrentDownloads { get; set; } = 3;

        /// <summary>
        /// Gets or sets the global download speed limit in bytes per second (0 = unlimited).
        /// </summary>
        public long MaxDownloadSpeed { get; set; } = 0;

        /// <summary>
        /// Gets or sets the global upload speed limit in bytes per second (0 = unlimited).
        /// </summary>
        public long MaxUploadSpeed { get; set; } = 0;

        /// <summary>
        /// Gets or sets a value indicating whether DHT is enabled for trackerless torrents.
        /// </summary>
        public bool EnableDHT { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether peer exchange is enabled.
        /// </summary>
        public bool EnablePEX { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether encrypted peer connections are preferred.
        /// </summary>
        public bool EnableEncryption { get; set; } = true;

        /// <summary>
        /// Gets or sets the port for incoming peer connections.
        /// </summary>
        public int ListenPort { get; set; } = 6881;

        /// <summary>
        /// Gets or sets a value indicating whether files should be deleted from staging after successful import.
        /// </summary>
        public bool RemoveAfterImport { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether automatic import is enabled.
        /// </summary>
        public bool AutoImportEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the storage warning threshold in bytes (warn when available disk space falls below this).
        /// </summary>
        public long StorageWarningThreshold { get; set; } = 10737418240; // 10 GB
    }
}
