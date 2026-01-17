using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.TorrentDownloader.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TorrentDownloader.Services
{
    /// <summary>
    /// Orchestrates automatic import of completed downloads into Jellyfin libraries.
    /// </summary>
    public class ImportOrchestrator : IImportOrchestrator
    {
        private readonly ILibraryManager _libraryManager;
        private readonly IFileSystem _fileSystem;
        private readonly IStorageManager _storageManager;
        private readonly ILogger<ImportOrchestrator> _logger;

        private static readonly HashSet<string> VideoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v", ".mpg", ".mpeg", ".ts"
        };

        private static readonly HashSet<string> AudioExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".flac", ".m4a", ".aac", ".ogg", ".wma", ".wav", ".opus"
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="ImportOrchestrator"/> class.
        /// </summary>
        /// <param name="libraryManager">Library manager instance.</param>
        /// <param name="fileSystem">File system instance.</param>
        /// <param name="storageManager">Storage manager instance.</param>
        /// <param name="logger">Logger instance.</param>
        public ImportOrchestrator(
            ILibraryManager libraryManager,
            IFileSystem fileSystem,
            IStorageManager storageManager,
            ILogger<ImportOrchestrator> logger)
        {
            _libraryManager = libraryManager;
            _fileSystem = fileSystem;
            _storageManager = storageManager;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<bool> ImportDownloadAsync(DownloadEntry download)
        {
            ArgumentNullException.ThrowIfNull(download);

            var config = TorrentDownloaderPlugin.Instance?.Configuration;
            if (config == null)
            {
                _logger.LogError("Cannot import: plugin configuration not available");
                return false;
            }

            if (!config.AutoImportEnabled)
            {
                _logger.LogInformation("Auto-import disabled, skipping import for {DownloadId}", download.DownloadId);
                return false;
            }

            if (!Directory.Exists(download.StagingPath))
            {
                _logger.LogError("Staging path does not exist for download {DownloadId}: {Path}",
                    download.DownloadId, download.StagingPath);
                return false;
            }

            var retryCount = 0;
            var maxRetries = config.ImportRetryAttempts;
            var retryDelay = config.ImportRetryDelaySeconds;

            while (retryCount <= maxRetries)
            {
                try
                {
                    if (retryCount > 0)
                    {
                        var delay = retryDelay * (int)Math.Pow(2, retryCount - 1);
                        _logger.LogInformation("Retry {Retry}/{Max} for import {DownloadId}, waiting {Delay}s",
                            retryCount, maxRetries, download.DownloadId, delay);
                        await Task.Delay(TimeSpan.FromSeconds(delay));
                    }

                    // Detect media type
                    var mediaType = DetectMediaType(download.StagingPath);
                    _logger.LogInformation("Detected media type {MediaType} for download {DownloadId}",
                        mediaType, download.DownloadId);

                    // Select target library
                    var targetLibrary = await SelectTargetLibraryAsync(download, mediaType);
                    if (targetLibrary == null)
                    {
                        _logger.LogWarning("No suitable library found for download {DownloadId}, media type {MediaType}",
                            download.DownloadId, mediaType);
                        return false;
                    }

                    _logger.LogInformation("Selected library {LibraryName} ({LibraryId}) for download {DownloadId}",
                        targetLibrary.Name, targetLibrary.Id, download.DownloadId);

                    // Get target path
                    var targetPath = GetTargetPath(targetLibrary, download);
                    if (string.IsNullOrEmpty(targetPath))
                    {
                        _logger.LogError("Could not determine target path for download {DownloadId}", download.DownloadId);
                        return false;
                    }

                    // Check storage on target volume
                    var stagingSize = GetDirectorySize(download.StagingPath);
                    if (!await _storageManager.HasSufficientSpaceAsync(stagingSize))
                    {
                        _logger.LogError("Insufficient storage space for import {DownloadId}, required {Bytes} bytes",
                            download.DownloadId, stagingSize);
                        return false;
                    }

                    // Move files
                    await MoveFilesAsync(download.StagingPath, targetPath);
                    _logger.LogInformation("Successfully moved files from {Source} to {Target}",
                        download.StagingPath, targetPath);

                    // Trigger library scan
                    await TriggerLibraryScanAsync(targetLibrary);

                    // Cleanup staging if configured
                    if (config.RemoveAfterImport)
                    {
                        try
                        {
                            Directory.Delete(download.StagingPath, true);
                            _logger.LogInformation("Deleted staging directory for download {DownloadId}", download.DownloadId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to delete staging directory {Path}", download.StagingPath);
                        }
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Import attempt {Retry}/{Max} failed for download {DownloadId}",
                        retryCount + 1, maxRetries + 1, download.DownloadId);
                    retryCount++;

                    if (retryCount > maxRetries)
                    {
                        _logger.LogError("Import failed after {Retries} attempts for download {DownloadId}",
                            maxRetries + 1, download.DownloadId);
                        return false;
                    }
                }
            }

            return false;
        }

        private string DetectMediaType(string path)
        {
            try
            {
                var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
                var extensions = files.Select(f => Path.GetExtension(f)).ToList();

                var videoCount = extensions.Count(ext => VideoExtensions.Contains(ext));
                var audioCount = extensions.Count(ext => AudioExtensions.Contains(ext));

                if (videoCount > audioCount)
                {
                    return "video";
                }
                else if (audioCount > 0)
                {
                    return "audio";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error detecting media type for path {Path}", path);
            }

            return "unknown";
        }

        private async Task<BaseItem?> SelectTargetLibraryAsync(DownloadEntry download, string mediaType)
        {
            try
            {
                // Check if user specified a target library
                if (download.TargetLibraryId.HasValue)
                {
                    var specifiedLibrary = _libraryManager.GetItemById(download.TargetLibraryId.Value);
                    if (specifiedLibrary != null)
                    {
                        return specifiedLibrary;
                    }
                }

                // Get all virtual folders
                var virtualFolders = _libraryManager.GetVirtualFolders();

                // Try to match by media type
                BaseItem? matchedLibrary = null;

                if (mediaType == "video")
                {
                    var videoFolder = virtualFolders.FirstOrDefault(f =>
                        f.CollectionType == MediaBrowser.Model.Entities.CollectionTypeOptions.Movies ||
                        f.CollectionType == MediaBrowser.Model.Entities.CollectionTypeOptions.TvShows);

                    if (videoFolder != null && videoFolder.ItemId != null)
                    {
                        matchedLibrary = _libraryManager.GetItemById(Guid.Parse(videoFolder.ItemId));
                    }
                }
                else if (mediaType == "audio")
                {
                    var audioFolder = virtualFolders.FirstOrDefault(f =>
                        f.CollectionType == MediaBrowser.Model.Entities.CollectionTypeOptions.Music);

                    if (audioFolder != null && audioFolder.ItemId != null)
                    {
                        matchedLibrary = _libraryManager.GetItemById(Guid.Parse(audioFolder.ItemId));
                    }
                }

                if (matchedLibrary != null)
                {
                    return matchedLibrary;
                }

                // Try default library from config
                var config = TorrentDownloaderPlugin.Instance?.Configuration;
                if (config?.DefaultLibraryId != null)
                {
                    var defaultLibrary = _libraryManager.GetItemById(config.DefaultLibraryId.Value);
                    if (defaultLibrary != null)
                    {
                        return defaultLibrary;
                    }
                }

                // Fall back to first available library
                var firstFolder = virtualFolders.FirstOrDefault();
                if (firstFolder?.ItemId != null)
                {
                    return _libraryManager.GetItemById(Guid.Parse(firstFolder.ItemId));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error selecting target library");
            }

            return await Task.FromResult<BaseItem?>(null);
        }

        private string GetTargetPath(BaseItem library, DownloadEntry download)
        {
            try
            {
                var virtualFolders = _libraryManager.GetVirtualFolders();
                var libraryFolder = virtualFolders.FirstOrDefault(f => f.ItemId != null && Guid.Parse(f.ItemId) == library.Id);

                if (libraryFolder?.Locations != null && libraryFolder.Locations.Length > 0)
                {
                    var libraryPath = libraryFolder.Locations[0];
                    var folderName = Path.GetFileName(download.StagingPath);

                    if (string.IsNullOrEmpty(folderName))
                    {
                        folderName = download.DisplayName;
                    }

                    return Path.Combine(libraryPath, folderName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error determining target path for library {LibraryId}", library.Id);
            }

            return string.Empty;
        }

        private async Task MoveFilesAsync(string sourcePath, string targetPath)
        {
            await Task.Run(() =>
            {
                // Ensure target directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? string.Empty);

                // Handle collision
                if (Directory.Exists(targetPath))
                {
                    var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                    targetPath = $"{targetPath}_{timestamp}";
                    _logger.LogWarning("Target path exists, using timestamped path: {Path}", targetPath);
                }

                // Move directory
                Directory.Move(sourcePath, targetPath);
            });
        }

        private async Task TriggerLibraryScanAsync(BaseItem library)
        {
            try
            {
                await _libraryManager.ValidateMediaLibrary(new Progress<double>(), CancellationToken.None);
                _logger.LogInformation("Triggered library scan for library {LibraryName}", library.Name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to trigger library scan, but files were moved successfully");
            }
        }

        private long GetDirectorySize(string path)
        {
            long size = 0;

            try
            {
                var dirInfo = new DirectoryInfo(path);
                var files = dirInfo.GetFiles("*", SearchOption.AllDirectories);
                size = files.Sum(f => f.Length);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error calculating directory size for {Path}", path);
            }

            return size;
        }
    }
}
