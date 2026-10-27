using MediaBrowser.Controller;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MulletaFlix.Data.Events.Users;
using MulletaFlix.Plugin.GetAvatar.Services;

namespace MulletaFlix.Plugin.GetAvatar
{
    /// <summary>
    /// Service registrator for the GetAvatar plugin.
    /// </summary>
    public class PluginServiceRegistrator : IPluginServiceRegistrator
    {
        /// <inheritdoc />
        public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
        {
            serviceCollection.AddHttpClient();
            serviceCollection.AddHttpClient(OnlinePackService.PackDownloadHttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => new System.Net.Http.HttpClientHandler { AllowAutoRedirect = false });
            serviceCollection.AddSingleton<AvatarService>();
            serviceCollection.AddSingleton<OnlinePackService>();
            serviceCollection.AddSingleton<IStartupFilter, ScriptInjectorStartup>();
            serviceCollection.AddHostedService<AvatarValidationService>();
            serviceCollection.AddScoped<IEventConsumer<UserCreatedEventArgs>, UserCreatedAvatarConsumer>();
        }
    }
}
