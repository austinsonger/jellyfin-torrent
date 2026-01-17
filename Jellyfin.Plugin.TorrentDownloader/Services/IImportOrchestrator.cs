using System;
using System.Threading.Tasks;
using Jellyfin.Plugin.TorrentDownloader.Models;

namespace Jellyfin.Plugin.TorrentDownloader.Services
{
    /// <summary>
    /// Interface for import orchestration operations.
    /// </summary>
    public interface IImportOrchestrator
    {
        /// <summary>
        /// Imports a completed download into the appropriate Jellyfin library.
        /// </summary>
        /// <param name="download">The download entry to import.</param>
        /// <returns>True if import succeeded, false otherwise.</returns>
        Task<bool> ImportDownloadAsync(DownloadEntry download);
    }
}
