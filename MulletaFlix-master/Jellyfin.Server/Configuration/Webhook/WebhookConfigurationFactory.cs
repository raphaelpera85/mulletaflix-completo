using System.Collections.Generic;
using MediaBrowser.Common.Configuration;

namespace MulletaFlix.Server.Configuration.Webhook;

/// <summary>
/// A configuration factory for <see cref="MediaBrowser.Model.Configuration.WebhookConfiguration"/>.
/// </summary>
public sealed class WebhookConfigurationFactory : IConfigurationFactory
{
    /// <inheritdoc />
    public IEnumerable<ConfigurationStore> GetConfigurations()
    {
        return [new WebhookConfigurationStore()];
    }
}
