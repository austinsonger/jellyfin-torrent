using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Plugin.TorrentDownloader.Models;
using Jellyfin.Plugin.TorrentDownloader.Models.Dto;
using Jellyfin.Plugin.TorrentDownloader.Services;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TorrentDownloader.Controllers
{
    /// <summary>
    /// API controller for torrent download operations.
    /// </summary>
    [ApiController]
    [Route("api/torrents")]
    [Authorize(Policy = "RequiresElevation")]
    public class TorrentsController : ControllerBase
    {
        private readonly IDownloadManager _downloadManager;
        private readonly IUserManager _userManager;
        private readonly IStorageManager? _storageManager;
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger<TorrentsController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TorrentsController"/> class.
        /// </summary>
        /// <param name="downloadManager">Download manager instance.</param>
        /// <param name="userManager">User manager instance.</param>
        /// <param name="libraryManager">Library manager instance.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="storageManager">Storage manager instance (optional).</param>
        public TorrentsController(
            IDownloadManager downloadManager,
            IUserManager userManager,
            ILibraryManager libraryManager,
            ILogger<TorrentsController> logger,
            IStorageManager? storageManager = null)
        {
            _downloadManager = downloadManager;
            _userManager = userManager;
            _libraryManager = libraryManager;
            _logger = logger;
            _storageManager = storageManager;
        }

        /// <summary>
        /// Creates a new torrent download.
        /// </summary>
        /// <param name="request">Create download request.</param>
        /// <returns>Created download entry.</returns>
        [HttpPost("download")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<DownloadSummaryResponse>> CreateDownload([FromBody] CreateDownloadRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            
            try
            {
                // Validate torrent source
                if (string.IsNullOrWhiteSpace(request.TorrentSource))
                {
                    return BadRequest("Torrent source is required");
                }

                // Validate torrent source length (prevent resource exhaustion)
                if (request.TorrentSource.Length > 10000)
                {
                    return BadRequest("Torrent source exceeds maximum length");
                }

                // Validate torrent source scheme
                if (!request.TorrentSource.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
                {
                    // If it's a file path, validate it securely
                    var fullPath = Path.GetFullPath(request.TorrentSource);
                    
                    // Reject path traversal attempts
                    if (fullPath.Contains("..", StringComparison.Ordinal) || 
                        request.TorrentSource.Contains("..", StringComparison.Ordinal))
                    {
                        return BadRequest("Path traversal detected");
                    }
                    
                    // Validate extension
                    if (!fullPath.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
                    {
                        return BadRequest("Invalid torrent file extension");
                    }
                    
                    // Verify file exists
                    if (!System.IO.File.Exists(fullPath))
                    {
                        return BadRequest("Torrent file not found");
                    }
                }

                var userId = request.UserId;
                if (userId == Guid.Empty)
                {
                    return BadRequest("User ID is required");
                }

                _logger.LogInformation("User {UserId} creating download from source", userId);

                var download = await _downloadManager.CreateDownloadAsync(request.TorrentSource, userId).ConfigureAwait(false);
                
                // Set target library if specified
                if (request.TargetLibraryId.HasValue)
                {
                    download.TargetLibraryId = request.TargetLibraryId.Value;
                }

                return Ok(MapToSummaryResponse(download));
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid torrent source");
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create download");
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to create download");
            }
        }

        /// <summary>
        /// Gets all downloads with optional status filter.
        /// </summary>
        /// <param name="status">Optional status filter.</param>
        /// <returns>List of downloads.</returns>
        [HttpGet("list")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<DownloadSummaryResponse>>> GetAllDownloads([FromQuery] string? status = null)
        {
            try
            {
                DownloadStatus? statusFilter = null;
                if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<DownloadStatus>(status, true, out var parsedStatus))
                {
                    statusFilter = parsedStatus;
                }

                var downloads = await _downloadManager.GetAllDownloadsAsync(statusFilter).ConfigureAwait(false);
                return Ok(downloads.Select(MapToSummaryResponse));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get downloads");
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to retrieve downloads");
            }
        }

        /// <summary>
        /// Gets a download by ID.
        /// </summary>
        /// <param name="id">Download ID.</param>
        /// <returns>Download summary.</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<DownloadSummaryResponse>> GetDownload(Guid id)
        {
            try
            {
                var download = await _downloadManager.GetDownloadAsync(id).ConfigureAwait(false);
                if (download == null)
                {
                    return NotFound();
                }

                return Ok(MapToSummaryResponse(download));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get download {DownloadId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to retrieve download");
            }
        }

        /// <summary>
        /// Gets detailed download information.
        /// </summary>
        /// <param name="id">Download ID.</param>
        /// <returns>Detailed download information.</returns>
        [HttpGet("{id}/details")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<DownloadDetailsResponse>> GetDownloadDetails(Guid id)
        {
            try
            {
                var download = await _downloadManager.GetDownloadAsync(id).ConfigureAwait(false);
                if (download == null)
                {
                    return NotFound();
                }

                return Ok(MapToDetailsResponse(download));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get download details {DownloadId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to retrieve download details");
            }
        }

        /// <summary>
        /// Controls a download (pause, resume, cancel).
        /// </summary>
        /// <param name="id">Download ID.</param>
        /// <param name="request">Control request.</param>
        /// <returns>Action result.</returns>
        [HttpPost("{id}/control")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> ControlDownload(Guid id, [FromBody] ControlDownloadRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            
            try
            {
                // Validate action
                if (string.IsNullOrWhiteSpace(request.Action))
                {
                    return BadRequest("Action is required");
                }

                if (request.Action.Length > 20)
                {
                    return BadRequest("Invalid action");
                }

                var download = await _downloadManager.GetDownloadAsync(id).ConfigureAwait(false);
                if (download == null)
                {
                    return NotFound();
                }

                switch (request.Action.ToUpperInvariant())
                {
                    case "PAUSE":
                        await _downloadManager.PauseDownloadAsync(id).ConfigureAwait(false);
                        _logger.LogInformation("Download {DownloadId} paused", id);
                        break;

                    case "RESUME":
                        await _downloadManager.ResumeDownloadAsync(id).ConfigureAwait(false);
                        _logger.LogInformation("Download {DownloadId} resumed", id);
                        break;

                    case "CANCEL":
                        await _downloadManager.CancelDownloadAsync(id, true).ConfigureAwait(false);
                        _logger.LogInformation("Download {DownloadId} cancelled", id);
                        break;

                    default:
                        return BadRequest($"Invalid action: {request.Action}");
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to control download {DownloadId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to control download");
            }
        }

        /// <summary>
        /// Deletes a download.
        /// </summary>
        /// <param name="id">Download ID.</param>
        /// <param name="deleteFiles">Whether to delete files.</param>
        /// <returns>Action result.</returns>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> DeleteDownload(Guid id, [FromQuery] bool deleteFiles = true)
        {
            try
            {
                var download = await _downloadManager.GetDownloadAsync(id).ConfigureAwait(false);
                if (download == null)
                {
                    return NotFound();
                }

                await _downloadManager.CancelDownloadAsync(id, deleteFiles).ConfigureAwait(false);
                _logger.LogInformation("Download {DownloadId} deleted", id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete download {DownloadId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to delete download");
            }
        }

        /// <summary>
        /// Gets storage status across all volumes.
        /// </summary>
        /// <returns>Storage status information.</returns>
        [HttpGet("storage/status")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<StorageStatusResponse>> GetStorageStatus()
        {
            try
            {
                if (_storageManager == null)
                {
                    return Ok(new StorageStatusResponse
                    {
                        IsStorageCritical = false,
                        LastCheckTime = DateTime.UtcNow
                    });
                }

                var volumes = await _storageManager.GetAllVolumesStatusAsync().ConfigureAwait(false);
                var response = new StorageStatusResponse
                {
                    IsStorageCritical = _storageManager.IsStorageCritical,
                    LastCheckTime = _storageManager.LastCheckTime
                };
                
                foreach (var v in volumes)
                {
                    response.Volumes.Add(new VolumeStatusResponse
                    {
                        VolumePath = v.VolumePath,
                        AvailableBytes = v.AvailableBytes,
                        TotalBytes = v.TotalBytes,
                        Status = v.Status.ToString(),
                        IsStagingVolume = v.IsStagingVolume
                    });
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get storage status");
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to retrieve storage status");
            }
        }

        /// <summary>
        /// Triggers manual cleanup of orphaned and old files.
        /// </summary>
        /// <returns>Cleanup result.</returns>
        [HttpPost("storage/cleanup")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<CleanupResultResponse>> TriggerCleanup()
        {
            try
            {
                if (_storageManager == null)
                {
                    var errorResponse = new CleanupResultResponse
                    {
                        FilesDeleted = 0,
                        BytesFreed = 0
                    };
                    errorResponse.Errors.Add("Storage manager not available");
                    return Ok(errorResponse);
                }

                var config = TorrentDownloaderPlugin.Instance?.Configuration;
                if (config == null)
                {
                    return BadRequest("Plugin configuration not available");
                }

                var downloads = await _downloadManager.GetAllDownloadsAsync().ConfigureAwait(false);
                var validIds = downloads.Select(d => d.DownloadId).ToList();

                // Cleanup orphaned files
                var (orphanedFiles, orphanedBytes) = await _storageManager.CleanupOrphanedFilesAsync(validIds).ConfigureAwait(false);

                // Cleanup old downloads
                var retentionDate = DateTime.UtcNow.AddDays(-config.CleanupRetentionDays);
                var (oldFiles, oldBytes) = await _storageManager.CleanupOldDownloadsAsync(retentionDate).ConfigureAwait(false);

                var response = new CleanupResultResponse
                {
                    FilesDeleted = orphanedFiles + oldFiles,
                    BytesFreed = orphanedBytes + oldBytes
                };

                _logger.LogInformation("Cleanup completed: {Files} files deleted, {Bytes} bytes freed",
                    response.FilesDeleted, response.BytesFreed);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to perform cleanup");
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to perform cleanup");
            }
        }

        /// <summary>
        /// Gets list of available Jellyfin libraries.
        /// </summary>
        /// <returns>List of library information.</returns>
        [HttpGet("libraries")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<IEnumerable<LibraryInfoResponse>> GetLibraries()
        {
            try
            {
                var virtualFolders = _libraryManager.GetVirtualFolders();
                var libraries = virtualFolders.Select(vf =>
                {
                    var lib = new LibraryInfoResponse
                    {
                        Id = string.IsNullOrEmpty(vf.ItemId) ? Guid.Empty : Guid.Parse(vf.ItemId),
                        Name = vf.Name ?? string.Empty,
                        CollectionType = vf.CollectionType?.ToString() ?? string.Empty
                    };
                    
                    if (vf.Locations != null)
                    {
                        foreach (var location in vf.Locations)
                        {
                            lib.Paths.Add(location);
                        }
                    }
                    
                    return lib;
                }).ToList();

                return Ok(libraries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get libraries");
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to retrieve libraries");
            }
        }

        private static DownloadSummaryResponse MapToSummaryResponse(DownloadEntry entry)
        {
            return new DownloadSummaryResponse
            {
                DownloadId = entry.DownloadId,
                DisplayName = entry.DisplayName,
                Status = entry.Status.ToString(),
                ProgressPercent = entry.ProgressPercent,
                DownloadSpeed = entry.DownloadSpeed,
                UploadSpeed = entry.UploadSpeed,
                PeerCount = entry.PeerCount,
                TotalSize = entry.TotalSize,
                DownloadedSize = entry.DownloadedSize,
                EstimatedTimeRemaining = entry.EstimatedTimeRemaining,
                CreatedAt = entry.CreatedAt,
                CompletedAt = entry.CompletedAt
            };
        }

        private static DownloadDetailsResponse MapToDetailsResponse(DownloadEntry entry)
        {
            return new DownloadDetailsResponse
            {
                DownloadId = entry.DownloadId,
                DisplayName = entry.DisplayName,
                Status = entry.Status.ToString(),
                ProgressPercent = entry.ProgressPercent,
                DownloadSpeed = entry.DownloadSpeed,
                UploadSpeed = entry.UploadSpeed,
                PeerCount = entry.PeerCount,
                TotalSize = entry.TotalSize,
                DownloadedSize = entry.DownloadedSize,
                EstimatedTimeRemaining = entry.EstimatedTimeRemaining,
                CreatedAt = entry.CreatedAt,
                CompletedAt = entry.CompletedAt,
                TorrentSource = entry.TorrentSource,
                InfoHash = entry.InfoHash,
                Trackers = entry.Trackers,
                StagingPath = entry.StagingPath,
                TargetLibraryId = entry.TargetLibraryId,
                ErrorMessage = entry.ErrorMessage,
                ImportedAt = entry.ImportedAt,
                CreatedByUserId = entry.CreatedByUserId
            };
        }
    }
}
