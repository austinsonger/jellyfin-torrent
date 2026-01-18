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
    public class DownloadManager : IDownloadManager, IDisposable
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };
        
        private readonly ITorrentEngine _torrentEngine;
        private readonly ILogger<DownloadManager> _logger;
        private readonly IStorageManager? _storageManager;
        private readonly IImportOrchestrator? _importOrchestrator;
        private readonly List<DownloadEntry> _downloads;
        private readonly SemaphoreSlim _lock;
        private readonly string _stateFilePath;
        private Timer? _progressTimer;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="DownloadManager"/> class.
        /// </summary>
        /// <param name="torrentEngine">Torrent engine instance.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="storageManager">Storage manager instance (optional).</param>
        /// <param name="importOrchestrator">Import orchestrator instance (optional).</param>
        public DownloadManager(
            ITorrentEngine torrentEngine,
            ILogger<DownloadManager> logger,
            IStorageManager? storageManager = null,
            IImportOrchestrator? importOrchestrator = null)
        {
            _torrentEngine = torrentEngine;
            _logger = logger;
            _storageManager = storageManager;
            _importOrchestrator = importOrchestrator;
            _downloads = new List<DownloadEntry>();
            _lock = new SemaphoreSlim(1, 1);
            
            var pluginDataPath = TorrentDownloaderPlugin.Instance?.DataFolderPath ?? Path.Combine(Path.GetTempPath(), "jellyfin-torrents");
            Directory.CreateDirectory(pluginDataPath);
            _stateFilePath = Path.Combine(pluginDataPath, "downloads.json");

            // Start progress monitoring timer
            _progressTimer = new Timer(async _ => await UpdateProgressAsync().ConfigureAwait(false), null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
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
                    _lock.Dispose();
                    _progressTimer?.Dispose();
                }

                _disposed = true;
            }
        }

        /// <inheritdoc />
        public async Task<DownloadEntry> CreateDownloadAsync(string torrentSource, Guid userId)
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                // Validate torrent source
                if (!await _torrentEngine.ValidateTorrentSourceAsync(torrentSource).ConfigureAwait(false))
                {
                    throw new ArgumentException("Invalid torrent source", nameof(torrentSource));
                }

                var config = TorrentDownloaderPlugin.Instance?.Configuration;
                if (config == null)
                {
                    throw new InvalidOperationException("Plugin configuration not available");
                }

                // Check storage availability
                if (_storageManager != null && _storageManager.IsStorageCritical)
                {
                    throw new InvalidOperationException("Storage space critically low, cannot create new downloads");
                }

                // Create download entry
                var downloadId = Guid.NewGuid();
                var stagingPath = Path.Combine(config.StagingDirectory, downloadId.ToString());
                Directory.CreateDirectory(stagingPath);

                // Sanitize display name from torrent source
                var displayName = Path.GetFileNameWithoutExtension(torrentSource);
                displayName = SanitizeDisplayName(displayName);

                var entry = new DownloadEntry
                {
                    DownloadId = downloadId,
                    TorrentSource = torrentSource,
                    DisplayName = displayName,
                    Status = DownloadStatus.Queued,
                    StagingPath = stagingPath,
                    CreatedAt = DateTime.UtcNow,
                    CreatedByUserId = userId
                };

                _downloads.Add(entry);
                await SaveStateAsync().ConfigureAwait(false);

                _logger.LogInformation("Created download {DownloadId} for user {UserId}", downloadId, userId);

                // Process queue
                _ = Task.Run(async () => await ProcessQueueAsync().ConfigureAwait(false));

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
            await _lock.WaitAsync().ConfigureAwait(false);
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
            await _lock.WaitAsync().ConfigureAwait(false);
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
            await _lock.WaitAsync().ConfigureAwait(false);
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

                    await SaveStateAsync().ConfigureAwait(false);
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
            await _torrentEngine.PauseDownloadAsync(downloadId).ConfigureAwait(false);
            await UpdateDownloadStatusAsync(downloadId, DownloadStatus.Paused).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task ResumeDownloadAsync(Guid downloadId)
        {
            await _torrentEngine.ResumeDownloadAsync(downloadId).ConfigureAwait(false);
            await UpdateDownloadStatusAsync(downloadId, DownloadStatus.Downloading).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task CancelDownloadAsync(Guid downloadId, bool deleteFiles = true)
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                await _torrentEngine.StopDownloadAsync(downloadId, deleteFiles).ConfigureAwait(false);
                _downloads.RemoveAll(d => d.DownloadId == downloadId);
                await SaveStateAsync().ConfigureAwait(false);

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
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                var config = TorrentDownloaderPlugin.Instance?.Configuration;
                if (config == null)
                {
                    return;
                }

                // Check storage before processing queue
                if (_storageManager != null && _storageManager.IsStorageCritical)
                {
                    _logger.LogWarning("Storage critical, pausing queue processing");
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

                    // Check storage before starting download
                    if (_storageManager != null && _storageManager.IsStorageCritical)
                    {
                        _logger.LogWarning("Storage critical, stopping queue processing");
                        break;
                    }

                    try
                    {
                        await _torrentEngine.StartDownloadAsync(download).ConfigureAwait(false);
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

                await SaveStateAsync().ConfigureAwait(false);
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
                var json = JsonSerializer.Serialize(_downloads, JsonOptions);
                
                // Atomic write: write to temp file first, then move
                var tempFilePath = _stateFilePath + ".tmp";
                var backupFilePath = _stateFilePath + ".bak";
                
                // Write to temporary file
                await File.WriteAllTextAsync(tempFilePath, json).ConfigureAwait(false);
                
                // Create backup of existing state file if it exists
                if (File.Exists(_stateFilePath))
                {
                    if (File.Exists(backupFilePath))
                    {
                        File.Delete(backupFilePath);
                    }
                    File.Move(_stateFilePath, backupFilePath);
                }
                
                // Atomically replace the state file with the new one
                File.Move(tempFilePath, _stateFilePath);
                
                _logger.LogDebug("Successfully saved state for {Count} downloads", _downloads.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save download state");
            }
        }

        /// <inheritdoc />
        public async Task LoadStateAsync()
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (File.Exists(_stateFilePath))
                {
                    var json = await File.ReadAllTextAsync(_stateFilePath).ConfigureAwait(false);
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

                        await ProcessQueueAsync().ConfigureAwait(false);
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
                    var progress = await _torrentEngine.GetDownloadProgressAsync(download.DownloadId).ConfigureAwait(false);
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
                            await UpdateDownloadStatusAsync(download.DownloadId, DownloadStatus.Completed).ConfigureAwait(false);
                            
                            // Trigger import orchestrator if available
                            if (_importOrchestrator != null)
                            {
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        _logger.LogInformation("Starting import for download {DownloadId}", download.DownloadId);
                                        await UpdateDownloadStatusAsync(download.DownloadId, DownloadStatus.Importing).ConfigureAwait(false);
                                        
                                        var importSuccess = await _importOrchestrator.ImportDownloadAsync(download).ConfigureAwait(false);
                                        
                                        if (importSuccess)
                                        {
                                            await UpdateDownloadStatusAsync(download.DownloadId, DownloadStatus.Imported).ConfigureAwait(false);
                                            _logger.LogInformation("Successfully imported download {DownloadId}", download.DownloadId);
                                        }
                                        else
                                        {
                                            await UpdateDownloadStatusAsync(download.DownloadId, DownloadStatus.Completed, "Import failed, manual import required").ConfigureAwait(false);
                                            _logger.LogWarning("Import failed for download {DownloadId}, manual import required", download.DownloadId);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, "Error importing download {DownloadId}", download.DownloadId);
                                        await UpdateDownloadStatusAsync(download.DownloadId, DownloadStatus.Completed, $"Import error: {ex.Message}").ConfigureAwait(false);
                                    }
                                }).ConfigureAwait(false);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating download progress");
            }
        }

        /// <summary>
        /// Sanitizes display name by removing potentially dangerous characters.
        /// </summary>
        /// <param name="name">The name to sanitize.</param>
        /// <returns>Sanitized name.</returns>
        private static string SanitizeDisplayName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "Unknown";
            }

            // Limit length
            if (name.Length > 200)
            {
                name = name.Substring(0, 200);
            }

            // Remove invalid path characters
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var c in invalidChars)
            {
                name = name.Replace(c, '_');
            }

            // Remove control characters
            name = new string(name.Where(c => !char.IsControl(c)).ToArray());

            // Trim whitespace
            name = name.Trim();

            return string.IsNullOrWhiteSpace(name) ? "Unknown" : name;
        }
    }
}
