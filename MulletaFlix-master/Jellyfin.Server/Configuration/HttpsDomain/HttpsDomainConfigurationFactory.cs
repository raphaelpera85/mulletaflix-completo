using System.Collections.Generic;
using MediaBrowser.Common.Configuration;

namespace MulletaFlix.Server.Configuration.HttpsDomain;

/// <summary>Exposes the <see cref="HttpsDomainConfigurationStore"/> to the configuration host.</summary>
public sealed class HttpsDomainConfigurationFactory : IConfigurationFactory
{
    /// <inheritdoc />
    public IEnumerable<ConfigurationStore> GetConfigurations()
    {
        return [new HttpsDomainConfigurationStore()];
    }
}
