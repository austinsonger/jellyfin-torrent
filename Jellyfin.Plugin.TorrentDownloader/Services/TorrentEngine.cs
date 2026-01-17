using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Jellyfin.Plugin.TorrentDownloader.Configuration;
using Jellyfin.Plugin.TorrentDownloader.Models;
using Microsoft.Extensions.Logging;
using MonoTorrent;
using MonoTorrent.Client;

namespace Jellyfin.Plugin.TorrentDownloader.Services
{
    /// <summary>
    /// Implements torrent engine operations using MonoTorrent.
    /// </summary>
    public class TorrentEngine : ITorrentEngine, IDisposable
    {
        private readonly ILogger<TorrentEngine> _logger;
        private readonly Dictionary<Guid, TorrentManager> _torrentManagers;
        private ClientEngine? _engine;
        private EngineSettings? _engineSettings;
        private readonly object _lock = new object();
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="TorrentEngine"/> class.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        public TorrentEngine(ILogger<TorrentEngine> logger)
        {
            _logger = logger;
            _torrentManagers = new Dictionary<Guid, TorrentManager>();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _engine?.Dispose();
                }

                _disposed = true;
            }
        }

        /// <inheritdoc />
        public async Task InitializeAsync()
        {
            try
            {
                var config = TorrentDownloaderPlugin.Instance?.Configuration;
                if (config == null)
                {
                    throw new InvalidOperationException("Plugin configuration not available");
                }

                var settingBuilder = new EngineSettingsBuilder
                {
                    MaximumDownloadRate = config.MaxDownloadSpeed > 0 ? (int)config.MaxDownloadSpeed : 0,
                    MaximumUploadRate = config.MaxUploadSpeed > 0 ? (int)config.MaxUploadSpeed : 0,
                    AllowPortForwarding = true
                };
                settingBuilder.ListenEndPoints.Add("ipv4", new IPEndPoint(IPAddress.Any, config.ListenPort));

                _engineSettings = settingBuilder.ToSettings();
                _engine = new ClientEngine(_engineSettings);

                if (config.EnableDHT)
                {
                    _logger.LogInformation("DHT engine enabled on port {Port}", config.ListenPort);
                }

                _logger.LogInformation("Torrent engine initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize torrent engine");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task StartDownloadAsync(DownloadEntry downloadEntry)
        {
            try
            {
                if (_engine == null)
                {
                    throw new InvalidOperationException("Torrent engine not initialized");
                }

                _logger.LogInformation("Starting download for {Name}", downloadEntry.DisplayName);

                // Parse torrent source and create manager
                TorrentManager manager;
                if (downloadEntry.TorrentSource.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
                {
                    var magnetLink = MagnetLink.Parse(downloadEntry.TorrentSource);
                    manager = await _engine.AddStreamingAsync(magnetLink, downloadEntry.StagingPath);
                }
                else
                {
                    var torrent = await Torrent.LoadAsync(downloadEntry.TorrentSource);
                    var torrentSettings = new TorrentSettingsBuilder
                    {
                        AllowInitialSeeding = false,
                        MaximumConnections = 60
                    }.ToSettings();
                    manager = await _engine.AddAsync(torrent, downloadEntry.StagingPath, torrentSettings);
                }
                
                lock (_lock)
                {
                    _torrentManagers[downloadEntry.DownloadId] = manager;
                }

                // Start download
                await manager.StartAsync();

                _logger.LogInformation(
                    "Download started: {Name}, InfoHash: {Hash}",
                    downloadEntry.DisplayName,
                    manager.InfoHashes.V1.ToHex());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start download for {Name}", downloadEntry.DisplayName);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task PauseDownloadAsync(Guid downloadId)
        {
            try
            {
                var manager = GetTorrentManager(downloadId);
                if (manager != null)
                {
                    await manager.PauseAsync();
                    _logger.LogInformation("Download paused: {DownloadId}", downloadId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to pause download {DownloadId}", downloadId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task ResumeDownloadAsync(Guid downloadId)
        {
            try
            {
                var manager = GetTorrentManager(downloadId);
                if (manager != null)
                {
                    await manager.StartAsync();
                    _logger.LogInformation("Download resumed: {DownloadId}", downloadId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resume download {DownloadId}", downloadId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task StopDownloadAsync(Guid downloadId, bool deleteFiles)
        {
            try
            {
                var manager = GetTorrentManager(downloadId);
                if (manager != null)
                {
                    await manager.StopAsync();
                    
                    if (_engine != null)
                    {
                        await _engine.RemoveAsync(manager);
                    }

                    lock (_lock)
                    {
                        _torrentManagers.Remove(downloadId);
                    }

                    if (deleteFiles && Directory.Exists(manager.SavePath))
                    {
                        Directory.Delete(manager.SavePath, true);
                    }

                    _logger.LogInformation("Download stopped: {DownloadId}, DeleteFiles: {DeleteFiles}", downloadId, deleteFiles);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop download {DownloadId}", downloadId);
                throw;
            }
        }

        /// <inheritdoc />
        public Task<DownloadEntry?> GetDownloadProgressAsync(Guid downloadId)
        {
            try
            {
                var manager = GetTorrentManager(downloadId);
                if (manager == null)
                {
                    return Task.FromResult<DownloadEntry?>(null);
                }

                var entry = new DownloadEntry
                {
                    DownloadId = downloadId,
                    TotalSize = manager.Torrent?.Size ?? 0,
                    DownloadedSize = manager.Monitor.DataBytesDownloaded,
                    ProgressPercent = (decimal)manager.Progress,
                    DownloadSpeed = manager.Monitor.DownloadSpeed,
                    UploadSpeed = manager.Monitor.UploadSpeed,
                    PeerCount = manager.OpenConnections,
                    DisplayName = manager.Torrent?.Name ?? "Unknown",
                    InfoHash = manager.InfoHashes.V1.ToHex()
                };

                // Calculate ETA
                if (entry.DownloadSpeed > 0)
                {
                    var remaining = entry.TotalSize - entry.DownloadedSize;
                    entry.EstimatedTimeRemaining = remaining / entry.DownloadSpeed;
                }

                return Task.FromResult<DownloadEntry?>(entry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get progress for download {DownloadId}", downloadId);
                return Task.FromResult<DownloadEntry?>(null);
            }
        }

        /// <inheritdoc />
        public Task<bool> ValidateTorrentSourceAsync(string torrentSource)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(torrentSource))
                {
                    return Task.FromResult(false);
                }

                // Validate magnet link
                if (torrentSource.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        MagnetLink.Parse(torrentSource);
                        return Task.FromResult(true);
                    }
                    catch
                    {
                        return Task.FromResult(false);
                    }
                }

                // Validate torrent file
                if (File.Exists(torrentSource) && torrentSource.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(true);
                }

                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate torrent source");
                return Task.FromResult(false);
            }
        }

        /// <inheritdoc />
        public async Task ShutdownAsync()
        {
            try
            {
                if (_engine != null)
                {
                    foreach (var manager in _torrentManagers.Values.ToList())
                    {
                        await manager.StopAsync();
                    }

                    await _engine.StopAllAsync();
                    _torrentManagers.Clear();
                }

                _logger.LogInformation("Torrent engine shut down successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during torrent engine shutdown");
            }
        }

        private TorrentManager? GetTorrentManager(Guid downloadId)
        {
            lock (_lock)
            {
                return _torrentManagers.TryGetValue(downloadId, out var manager) ? manager : null;
            }
        }
    }
}
