using Jellyfin.Plugin.MediaRequests.Data;
using Jellyfin.Plugin.MediaRequests.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.MediaRequests
{
    public class PluginServiceRegistrator : IPluginServiceRegistrator
    {
        public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
        {
            serviceCollection.AddSingleton<RequestStore>();
            serviceCollection.AddSingleton<LibraryIndex>();
            serviceCollection.AddSingleton<TmdbClient>();
            serviceCollection.AddSingleton<WebhookSender>();
            serviceCollection.AddSingleton<Notifier>();
            serviceCollection.AddSingleton<RequestService>();
        }
    }
}
