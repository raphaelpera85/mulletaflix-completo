using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Configuration;

namespace MulletaFlix.Server.Configuration.HttpsDomain;

/// <summary>Registers the <see cref="HttpsDomainConfiguration"/> persisted under the "httpsdomain" key.</summary>
public sealed class HttpsDomainConfigurationStore : ConfigurationStore
{
    /// <summary>Initializes a new instance of the <see cref="HttpsDomainConfigurationStore"/> class.</summary>
    public HttpsDomainConfigurationStore()
    {
        Key = "httpsdomain";
        ConfigurationType = typeof(HttpsDomainConfiguration);
    }
}
