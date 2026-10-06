using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Configuration;

namespace MulletaFlix.Server.Configuration.Webhook;

/// <summary>
/// Configuration store registering <see cref="WebhookConfiguration"/> under the "webhook" key.
/// </summary>
public sealed class WebhookConfigurationStore : ConfigurationStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookConfigurationStore"/> class.
    /// </summary>
    public WebhookConfigurationStore()
    {
        Key = "webhook";
        ConfigurationType = typeof(WebhookConfiguration);
    }
}
