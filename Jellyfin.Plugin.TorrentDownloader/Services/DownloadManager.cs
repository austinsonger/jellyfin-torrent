using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.TorrentDownloader.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TorrentDownloader.Services
{
    /// <summary>
    /// Manages torrent downloads with queue processing and state persistence.
    /// </summary>
    public class DownloadManager : IDownloadManager
    {
        private readonly ITorrentEngine _torrentEngine;
        private readonly ILogger<DownloadManager> _logger;
        private readonly List<DownloadEntry> _downloads;
        private readonly SemaphoreSlim _lock;
        private readonly string _stateFilePath;
        private Timer? _progressTimer;

        /// <summary>
        /// Initializes a new instance of the <see cref="DownloadManager"/> class.
        /// </summary>
        /// <param name="torrentEngine">Torrent engine instance.</param>
        /// <param name="logger">Logger instance.</param>
        public DownloadManager(ITorrentEngine torrentEngine, ILogger<DownloadManager> logger)
        {
            _torrentEngine = torrentEngine;
            _logger = logger;
            _downloads = new List<DownloadEntry>();
            _lock = new SemaphoreSlim(1, 1);
            
            var pluginDataPath = Plugin.Instance?.DataFolderPath ?? Path.Combine(Path.GetTempPath(), "jellyfin-torrents");
            Directory.CreateDirectory(pluginDataPath);
            _stateFilePath = Path.Combine(pluginDataPath, "downloads.json");

            // Start progress monitoring timer
            _progressTimer = new Timer(async _ => await UpdateProgressAsync(), null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
        }

        /// <inheritdoc />
        public async Task<DownloadEntry> CreateDownloadAsync(string torrentSource, Guid userId)
        {
            await _lock.WaitAsync();
            try
            {
                // Validate torrent source
                if (!await _torrentEngine.ValidateTorrentSourceAsync(torrentSource))
                {
                    throw new ArgumentException("Invalid torrent source", nameof(torrentSource));
                }

                var config = Plugin.Instance?.Configuration;
                if (config == null)
                {
                    throw new InvalidOperationException("Plugin configuration not available");
                }

                // Create download entry
                var downloadId = Guid.NewGuid();
                var stagingPath = Path.Combine(config.StagingDirectory, downloadId.ToString());
                Directory.CreateDirectory(stagingPath);

                var entry = new DownloadEntry
                {
                    DownloadId = downloadId,
                    TorrentSource = torrentSource,
                    DisplayName = Path.GetFileNameWithoutExtension(torrentSource),
                    Status = DownloadStatus.Queued,
                    StagingPath = stagingPath,
                    CreatedAt = DateTime.UtcNow,
                    CreatedByUserId = userId
                };

                _downloads.Add(entry);
                await SaveStateAsync();

                _logger.LogInformation("Created download {DownloadId} for user {UserId}", downloadId, userId);

                // Process queue
                _ = Task.Run(async () => await ProcessQueueAsync());

                return entry;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<DownloadEntry?> GetDownloadAsync(Guid downloadId)
        {
            await _lock.WaitAsync();
            try
            {
                return _downloads.FirstOrDefault(d => d.DownloadId == downloadId);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<IList<DownloadEntry>> GetAllDownloadsAsync(DownloadStatus? status = null)
        {
            await _lock.WaitAsync();
            try
            {
                if (status.HasValue)
                {
                    return _downloads.Where(d => d.Status == status.Value).ToList();
                }

                return _downloads.ToList();
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task UpdateDownloadStatusAsync(Guid downloadId, DownloadStatus status, string? errorMessage = null)
        {
            await _lock.WaitAsync();
            try
            {
                var download = _downloads.FirstOrDefault(d => d.DownloadId == downloadId);
                if (download != null)
                {
                    download.Status = status;
                    download.ErrorMessage = errorMessage;

                    if (status == DownloadStatus.Completed)
                    {
                        download.CompletedAt = DateTime.UtcNow;
                        download.ProgressPercent = 100;
                    }
                    else if (status == DownloadStatus.Imported)
                    {
                        download.ImportedAt = DateTime.UtcNow;
                    }

                    await SaveStateAsync();
                    _logger.LogInformation("Download {DownloadId} status updated to {Status}", downloadId, status);
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task PauseDownloadAsync(Guid downloadId)
        {
            await _torrentEngine.PauseDownloadAsync(downloadId);
            await UpdateDownloadStatusAsync(downloadId, DownloadStatus.Paused);
        }

        /// <inheritdoc />
        public async Task ResumeDownloadAsync(Guid downloadId)
        {
            await _torrentEngine.ResumeDownloadAsync(downloadId);
            await UpdateDownloadStatusAsync(downloadId, DownloadStatus.Downloading);
        }

        /// <inheritdoc />
        public async Task CancelDownloadAsync(Guid downloadId, bool deleteFiles = true)
        {
            await _lock.WaitAsync();
            try
            {
                await _torrentEngine.StopDownloadAsync(downloadId, deleteFiles);
                _downloads.RemoveAll(d => d.DownloadId == downloadId);
                await SaveStateAsync();

                _logger.LogInformation("Download {DownloadId} cancelled", downloadId);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task ProcessQueueAsync()
        {
            await _lock.WaitAsync();
            try
            {
                var config = Plugin.Instance?.Configuration;
                if (config == null)
                {
                    return;
                }

                var activeDownloads = _downloads.Count(d => d.Status == DownloadStatus.Downloading);
                var queuedDownloads = _downloads.Where(d => d.Status == DownloadStatus.Queued).ToList();

                foreach (var download in queuedDownloads)
                {
                    if (activeDownloads >= config.MaxConcurrentDownloads)
                    {
                        break;
                    }

                    try
                    {
                        await _torrentEngine.StartDownloadAsync(download);
                        download.Status = DownloadStatus.Downloading;
                        activeDownloads++;

                        _logger.LogInformation("Started download {DownloadId} from queue", download.DownloadId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to start queued download {DownloadId}", download.DownloadId);
                        download.Status = DownloadStatus.Failed;
                        download.ErrorMessage = ex.Message;
                    }
                }

                await SaveStateAsync();
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task SaveStateAsync()
        {
            try
            {
                var json = JsonSerializer.Serialize(_downloads, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_stateFilePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save download state");
            }
        }

        /// <inheritdoc />
        public async Task LoadStateAsync()
        {
            await _lock.WaitAsync();
            try
            {
                if (File.Exists(_stateFilePath))
                {
                    var json = await File.ReadAllTextAsync(_stateFilePath);
                    var downloads = JsonSerializer.Deserialize<List<DownloadEntry>>(json);

                    if (downloads != null)
                    {
                        _downloads.Clear();
                        _downloads.AddRange(downloads);

                        _logger.LogInformation("Loaded {Count} downloads from state file", downloads.Count);

                        // Resume interrupted downloads
                        foreach (var download in _downloads.Where(d => d.Status == DownloadStatus.Downloading))
                        {
                            download.Status = DownloadStatus.Queued;
                        }

                        await ProcessQueueAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load download state");
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task UpdateProgressAsync()
        {
            try
            {
                var activeDownloads = _downloads.Where(d => d.Status == DownloadStatus.Downloading).ToList();

                foreach (var download in activeDownloads)
                {
                    var progress = await _torrentEngine.GetDownloadProgressAsync(download.DownloadId);
                    if (progress != null)
                    {
                        download.DownloadedSize = progress.DownloadedSize;
                        download.TotalSize = progress.TotalSize;
                        download.ProgressPercent = progress.ProgressPercent;
                        download.DownloadSpeed = progress.DownloadSpeed;
                        download.UploadSpeed = progress.UploadSpeed;
                        download.PeerCount = progress.PeerCount;
                        download.EstimatedTimeRemaining = progress.EstimatedTimeRemaining;

                        // Check if complete
                        if (progress.ProgressPercent >= 100)
                        {
                            await UpdateDownloadStatusAsync(download.DownloadId, DownloadStatus.Completed);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating download progress");
            }
        }
    }
}
