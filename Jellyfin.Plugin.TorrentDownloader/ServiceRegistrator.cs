using Jellyfin.Plugin.TorrentDownloader.Services;
using MediaBrowser.Common.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.TorrentDownloader
{
    public class ServiceRegistrator : IPluginServiceRegistrator
    {
        public void RegisterServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<ITorrentEngine, TorrentEngine>();
            serviceCollection.AddSingleton<IDownloadManager, DownloadManager>();
            serviceCollection.AddSingleton<IImportOrchestrator, ImportOrchestrator>();
            serviceCollection.AddSingleton<IStorageManager, StorageManager>();
        }
    }
}
