using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Nebula;
using MediaBrowser.Model.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MulletaFlix.Server.Health;

/// <summary>
/// Verifies that the Nebula manager can report its runtime state.
/// </summary>
public sealed class NebulaHealthCheck : IHealthCheck
{
    private readonly INebulaFtpManager _nebulaManager;
    private readonly IServerConfigurationManager _configurationManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="NebulaHealthCheck"/> class.
    /// </summary>
    /// <param name="nebulaManager">Nebula manager.</param>
    /// <param name="configurationManager">Server configuration manager.</param>
    public NebulaHealthCheck(
        INebulaFtpManager nebulaManager,
        IServerConfigurationManager configurationManager)
    {
        _nebulaManager = nebulaManager;
        _configurationManager = configurationManager;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var config = _configurationManager.GetConfiguration<NebulaFtpConfiguration>("nebulaftp")
            ?? new NebulaFtpConfiguration();
        if (!config.Enabled)
        {
            return HealthCheckResult.Healthy("Nebula está desabilitado.");
        }

        try
        {
            var status = await _nebulaManager.GetStatusAsync(cancellationToken).ConfigureAwait(false);
            var data = new System.Collections.Generic.Dictionary<string, object>
            {
                ["envioRunning"] = status.IsEnvioRunning,
                ["downloaderRunning"] = status.IsDownloaderRunning,
                ["streamOnly"] = status.StreamOnly
            };

            if (!status.IsEnvioRunning)
            {
                return HealthCheckResult.Unhealthy(
                    "Nebula está habilitado, mas o serviço de Envio (FTP/HTTP) não está em execução.",
                    data: data);
            }

            return HealthCheckResult.Healthy(
                "Nebula respondeu ao health check.",
                data: data);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Nebula não conseguiu reportar o estado.", ex);
        }
    }
}
