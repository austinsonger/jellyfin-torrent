using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.TorrentDownloader.Models;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TorrentDownloader.Services
{
    /// <summary>
    /// Manages storage monitoring and cleanup operations.
    /// </summary>
    public class StorageManager : IStorageManager, IDisposable
    {
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger<StorageManager> _logger;
        private readonly List<VolumeStatus> _volumeStatuses;
        private readonly SemaphoreSlim _lock;
        private Timer? _monitoringTimer;
        private bool _wasInCriticalState;
        private bool _disposed;

        /// <inheritdoc />
        public bool IsStorageCritical { get; private set; }

        /// <inheritdoc />
        public long StagingVolumeAvailableBytes { get; private set; }

        /// <inheritdoc />
        public StorageStatus StagingVolumeStatus { get; private set; }

        /// <inheritdoc />
        public DateTime LastCheckTime { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="StorageManager"/> class.
        /// </summary>
        /// <param name="libraryManager">Library manager instance.</param>
        /// <param name="logger">Logger instance.</param>
        public StorageManager(ILibraryManager libraryManager, ILogger<StorageManager> logger)
        {
            _libraryManager = libraryManager;
            _logger = logger;
            _volumeStatuses = new List<VolumeStatus>();
            _lock = new SemaphoreSlim(1, 1);
            _wasInCriticalState = false;
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
                    _monitoringTimer?.Dispose();
                }

                _disposed = true;
            }
        }

        /// <inheritdoc />
        public void Start()
        {
            var config = TorrentDownloaderPlugin.Instance?.Configuration;
            if (config == null)
            {
                _logger.LogWarning("Cannot start storage manager: plugin configuration not available");
                return;
            }

            var interval = TimeSpan.FromSeconds(config.StorageCheckIntervalActive);
            _monitoringTimer = new Timer(async _ => await CheckStorageAsync(), null, TimeSpan.Zero, interval);
            _logger.LogInformation("Storage manager started with {Interval}s check interval", interval.TotalSeconds);
        }

        /// <inheritdoc />
        public void Stop()
        {
            _monitoringTimer?.Dispose();
            _monitoringTimer = null;
            _logger.LogInformation("Storage manager stopped");
        }

        /// <inheritdoc />
        public async Task<IList<VolumeStatus>> GetAllVolumesStatusAsync()
        {
            await _lock.WaitAsync();
            try
            {
                return _volumeStatuses.ToList();
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<bool> HasSufficientSpaceAsync(long requiredBytes)
        {
            await CheckStorageAsync();
            return StagingVolumeAvailableBytes >= requiredBytes && !IsStorageCritical;
        }

        /// <inheritdoc />
        public async Task<(int filesDeleted, long bytesFreed)> CleanupOrphanedFilesAsync(IEnumerable<Guid> validDownloadIds)
        {
            var config = TorrentDownloaderPlugin.Instance?.Configuration;
            if (config == null || string.IsNullOrWhiteSpace(config.StagingDirectory))
            {
                return (0, 0);
            }

            if (!Directory.Exists(config.StagingDirectory))
            {
                return (0, 0);
            }

            var validIdStrings = validDownloadIds.Select(id => id.ToString()).ToHashSet();
            var filesDeleted = 0;
            var bytesFreed = 0L;

            await Task.Run(() =>
            {
                var directories = Directory.GetDirectories(config.StagingDirectory);
                foreach (var dir in directories)
                {
                    var dirName = Path.GetFileName(dir);
                    if (!validIdStrings.Contains(dirName))
                    {
                        try
                        {
                            var dirInfo = new DirectoryInfo(dir);
                            var size = GetDirectorySize(dirInfo);
                            Directory.Delete(dir, true);
                            filesDeleted++;
                            bytesFreed += size;
                            _logger.LogInformation("Cleaned up orphaned directory {Directory}, freed {Bytes} bytes", dir, size);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to delete orphaned directory {Directory}", dir);
                        }
                    }
                }
            });

            return (filesDeleted, bytesFreed);
        }

        /// <inheritdoc />
        public async Task<(int filesDeleted, long bytesFreed)> CleanupOldDownloadsAsync(DateTime olderThanDate)
        {
            var config = TorrentDownloaderPlugin.Instance?.Configuration;
            if (config == null || string.IsNullOrWhiteSpace(config.StagingDirectory))
            {
                return (0, 0);
            }

            if (!Directory.Exists(config.StagingDirectory))
            {
                return (0, 0);
            }

            var filesDeleted = 0;
            var bytesFreed = 0L;

            await Task.Run(() =>
            {
                var directories = Directory.GetDirectories(config.StagingDirectory);
                foreach (var dir in directories)
                {
                    try
                    {
                        var dirInfo = new DirectoryInfo(dir);
                        if (dirInfo.LastWriteTimeUtc < olderThanDate)
                        {
                            var size = GetDirectorySize(dirInfo);
                            Directory.Delete(dir, true);
                            filesDeleted++;
                            bytesFreed += size;
                            _logger.LogInformation("Cleaned up old directory {Directory}, freed {Bytes} bytes", dir, size);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to delete old directory {Directory}", dir);
                    }
                }
            });

            return (filesDeleted, bytesFreed);
        }

        private async Task CheckStorageAsync()
        {
            await _lock.WaitAsync();
            try
            {
                var config = TorrentDownloaderPlugin.Instance?.Configuration;
                if (config == null)
                {
                    return;
                }

                _volumeStatuses.Clear();
                LastCheckTime = DateTime.UtcNow;

                // Check staging volume
                if (!string.IsNullOrWhiteSpace(config.StagingDirectory))
                {
                    var stagingStatus = CheckVolume(config.StagingDirectory, true);
                    if (stagingStatus != null)
                    {
                        _volumeStatuses.Add(stagingStatus);
                        StagingVolumeAvailableBytes = stagingStatus.AvailableBytes;
                        StagingVolumeStatus = stagingStatus.Status;
                    }
                }

                // Check library volumes
                try
                {
                    var libraries = _libraryManager.GetVirtualFolders();
                    foreach (var library in libraries)
                    {
                        foreach (var location in library.Locations ?? Array.Empty<string>())
                        {
                            var volumeStatus = CheckVolume(location, false);
                            if (volumeStatus != null)
                            {
                                // Avoid duplicates for same volume
                                if (!_volumeStatuses.Any(v => v.VolumePath == volumeStatus.VolumePath))
                                {
                                    _volumeStatuses.Add(volumeStatus);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking library volumes");
                }

                // Determine overall critical state
                var previousCriticalState = IsStorageCritical;
                IsStorageCritical = _volumeStatuses.Any(v => v.Status == StorageStatus.Critical);

                // Log status changes
                if (IsStorageCritical && !previousCriticalState)
                {
                    _logger.LogError("Storage critical: one or more volumes below critical threshold");
                    _wasInCriticalState = true;
                }
                else if (!IsStorageCritical && _wasInCriticalState)
                {
                    _logger.LogInformation("Storage recovered: all volumes above critical threshold");
                    _wasInCriticalState = false;
                }

                // Log warnings
                foreach (var volume in _volumeStatuses.Where(v => v.Status == StorageStatus.Warning))
                {
                    _logger.LogWarning("Storage warning on {Volume}: {Available} bytes available", 
                        volume.VolumePath, volume.AvailableBytes);
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        private VolumeStatus? CheckVolume(string path, bool isStagingVolume)
        {
            try
            {
                var config = TorrentDownloaderPlugin.Instance?.Configuration;
                if (config == null)
                {
                    return null;
                }

                var rootPath = Path.GetPathRoot(path);
                if (string.IsNullOrEmpty(rootPath))
                {
                    _logger.LogWarning("Cannot determine root path for {Path}", path);
                    return null;
                }

                var driveInfo = new DriveInfo(rootPath);
                if (!driveInfo.IsReady)
                {
                    _logger.LogWarning("Drive not ready for {Path}", path);
                    return null;
                }

                var availableBytes = driveInfo.AvailableFreeSpace;
                var totalBytes = driveInfo.TotalSize;

                var status = StorageStatus.Normal;
                if (availableBytes < config.StorageCriticalThreshold)
                {
                    status = StorageStatus.Critical;
                }
                else if (availableBytes < config.StorageWarningThreshold)
                {
                    status = StorageStatus.Warning;
                }

                return new VolumeStatus
                {
                    VolumePath = rootPath,
                    AvailableBytes = availableBytes,
                    TotalBytes = totalBytes,
                    Status = status,
                    IsStagingVolume = isStagingVolume
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking volume for path {Path}", path);
                return null;
            }
        }

        private long GetDirectorySize(DirectoryInfo directoryInfo)
        {
            long size = 0;

            try
            {
                var files = directoryInfo.GetFiles();
                foreach (var file in files)
                {
                    size += file.Length;
                }

                var directories = directoryInfo.GetDirectories();
                foreach (var dir in directories)
                {
                    size += GetDirectorySize(dir);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error calculating directory size for {Directory}", directoryInfo.FullName);
            }

            return size;
        }
    }
}
