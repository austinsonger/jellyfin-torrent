using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Jellyfin.Plugin.TorrentDownloader.Configuration;
using Jellyfin.Plugin.TorrentDownloader.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TorrentDownloader
{
    /// <summary>
    /// The main plugin class for Jellyfin Torrent Downloader.
    /// </summary>
    public class TorrentDownloaderPlugin : BasePlugin<PluginConfiguration>, IHasWebPages, IPluginServiceRegistrator
    {
        private ITorrentEngine? _torrentEngine;
        private IStorageManager? _storageManager;
        private IDownloadManager? _downloadManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="TorrentDownloaderPlugin"/> class.
        /// </summary>
        /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
        /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
        public TorrentDownloaderPlugin(
            IApplicationPaths applicationPaths, 
            IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
        }

        /// <summary>
        /// Gets the current plugin instance.
        /// </summary>
        public static TorrentDownloaderPlugin? Instance { get; private set; }

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

        /// <inheritdoc />
        public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
        {
            // Register services with appropriate lifetimes
            serviceCollection.AddSingleton<ITorrentEngine, TorrentEngine>();
            serviceCollection.AddSingleton<IDownloadManager, DownloadManager>();
            serviceCollection.AddSingleton<IStorageManager, StorageManager>();
            serviceCollection.AddTransient<IImportOrchestrator, ImportOrchestrator>();
        }

        /// <summary>
        /// Initializes plugin services after dependency injection is complete.
        /// </summary>
        /// <param name="serviceProvider">The service provider.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task InitializeServicesAsync(IServiceProvider serviceProvider)
        {
            try
            {
                // Resolve services from container
                _torrentEngine = serviceProvider.GetRequiredService<ITorrentEngine>();
                _storageManager = serviceProvider.GetRequiredService<IStorageManager>();
                _downloadManager = serviceProvider.GetRequiredService<IDownloadManager>();

                // Initialize TorrentEngine
                await _torrentEngine.InitializeAsync().ConfigureAwait(false);

                // Start StorageManager monitoring
                _storageManager.Start();

                // Load download state
                await _downloadManager.LoadStateAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Log initialization failure but don't crash the plugin
                Console.WriteLine($"Failed to initialize Torrent Downloader services: {ex.Message}");
            }
        }

        /// <summary>
        /// Shuts down plugin services gracefully.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task ShutdownServicesAsync()
        {
            try
            {
                // Stop storage monitoring
                _storageManager?.StopMonitoring();

                // Shutdown torrent engine
                if (_torrentEngine != null)
                {
                    await _torrentEngine.ShutdownAsync().ConfigureAwait(false);
                }

                // Save final download state
                if (_downloadManager != null)
                {
                    await _downloadManager.SaveStateAsync().ConfigureAwait(false);
                }

                // Dispose disposable services
                (_torrentEngine as IDisposable)?.Dispose();
                (_downloadManager as IDisposable)?.Dispose();
                (_storageManager as IDisposable)?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during Torrent Downloader shutdown: {ex.Message}");
            }
        }
    }
}
