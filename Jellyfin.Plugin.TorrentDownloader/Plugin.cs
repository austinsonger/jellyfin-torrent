using System;
using System.Collections.Generic;
using Jellyfin.Plugin.TorrentDownloader.Configuration;
using Jellyfin.Plugin.TorrentDownloader.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TorrentDownloader
{
    /// <summary>
    /// The main plugin class for Jellyfin Torrent Downloader.
    /// </summary>
    public class TorrentDownloaderPlugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TorrentDownloaderPlugin"/> class.
        /// </summary>
        /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
        /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
        /// <param name="loggerFactory">Logger factory.</param>
        /// <param name="libraryManager">Library manager.</param>
        /// <param name="fileSystem">File system.</param>
        public TorrentDownloaderPlugin(
            IApplicationPaths applicationPaths, 
            IXmlSerializer xmlSerializer,
            ILoggerFactory loggerFactory,
            ILibraryManager libraryManager,
            IFileSystem fileSystem)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
            
            // Initialize services in correct dependency order
            var torrentLogger = loggerFactory.CreateLogger<TorrentEngine>();
            TorrentEngine = new TorrentEngine(torrentLogger);
            
            var storageLogger = loggerFactory.CreateLogger<StorageManager>();
            StorageManager = new StorageManager(libraryManager, storageLogger);
            
            var importLogger = loggerFactory.CreateLogger<ImportOrchestrator>();
            ImportOrchestrator = new ImportOrchestrator(libraryManager, fileSystem, StorageManager, importLogger);
            
            var downloadLogger = loggerFactory.CreateLogger<DownloadManager>();
            DownloadManager = new DownloadManager(TorrentEngine, downloadLogger, StorageManager, ImportOrchestrator);
        }

        /// <summary>
        /// Gets the current plugin instance.
        /// </summary>
        public static TorrentDownloaderPlugin? Instance { get; private set; }

        /// <summary>
        /// Gets the torrent engine.
        /// </summary>
        public ITorrentEngine TorrentEngine { get; private set; }

        /// <summary>
        /// Gets the download manager.
        /// </summary>
        public IDownloadManager DownloadManager { get; private set; }

        /// <summary>
        /// Gets the storage manager.
        /// </summary>
        public IStorageManager StorageManager { get; private set; }

        /// <summary>
        /// Gets the import orchestrator.
        /// </summary>
        public IImportOrchestrator ImportOrchestrator { get; private set; }

        /// <inheritdoc />
        public override string Name => "Torrent Downloader";

        /// <inheritdoc />
        public override Guid Id => Guid.Parse("a1b2c3d4-e5f6-4a5b-8c9d-0e1f2a3b4c5d");

        /// <inheritdoc />
        public override string Description => "Download torrents directly into your Jellyfin library";

        /// <inheritdoc />
        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = Name,
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html"
                },
                new PluginPageInfo
                {
                    Name = "Torrent Manager",
                    EmbeddedResourcePath = GetType().Namespace + ".Web.torrentManager.html"
                }
            };
        }
    }
}
