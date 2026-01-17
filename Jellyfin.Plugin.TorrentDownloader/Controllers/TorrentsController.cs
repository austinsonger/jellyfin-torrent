using System;
using System.Collections.Generic;
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
        private readonly ILogger<TorrentsController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TorrentsController"/> class.
        /// </summary>
        /// <param name="downloadManager">Download manager instance.</param>
        /// <param name="userManager">User manager instance.</param>
        /// <param name="logger">Logger instance.</param>
        public TorrentsController(
            IDownloadManager downloadManager,
            IUserManager userManager,
            ILogger<TorrentsController> logger)
        {
            _downloadManager = downloadManager;
            _userManager = userManager;
            _logger = logger;
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
            try
            {
                if (string.IsNullOrWhiteSpace(request.TorrentSource))
                {
                    return BadRequest("Torrent source is required");
                }

                var userId = request.UserId;
                if (userId == Guid.Empty)
                {
                    return BadRequest("User ID is required");
                }

                _logger.LogInformation("User {UserId} creating download from {Source}", userId, request.TorrentSource);

                var download = await _downloadManager.CreateDownloadAsync(request.TorrentSource, userId);

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

                var downloads = await _downloadManager.GetAllDownloadsAsync(statusFilter);
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
                var download = await _downloadManager.GetDownloadAsync(id);
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
                var download = await _downloadManager.GetDownloadAsync(id);
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
            try
            {
                var download = await _downloadManager.GetDownloadAsync(id);
                if (download == null)
                {
                    return NotFound();
                }

                switch (request.Action.ToLowerInvariant())
                {
                    case "pause":
                        await _downloadManager.PauseDownloadAsync(id);
                        _logger.LogInformation("Download {DownloadId} paused", id);
                        break;

                    case "resume":
                        await _downloadManager.ResumeDownloadAsync(id);
                        _logger.LogInformation("Download {DownloadId} resumed", id);
                        break;

                    case "cancel":
                        await _downloadManager.CancelDownloadAsync(id, true);
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
                var download = await _downloadManager.GetDownloadAsync(id);
                if (download == null)
                {
                    return NotFound();
                }

                await _downloadManager.CancelDownloadAsync(id, deleteFiles);
                _logger.LogInformation("Download {DownloadId} deleted", id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete download {DownloadId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to delete download");
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
