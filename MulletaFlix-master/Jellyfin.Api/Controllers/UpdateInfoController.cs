using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller;
using MediaBrowser.Model.System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MulletaFlix.Api.Controllers;

/// <summary>
/// Exposes server update availability for the dashboard update center.
/// </summary>
[Route("System")]
[Authorize(Policy = Policies.RequiresElevation)]
public class UpdateInfoController : BaseMulletaFlixApiController
{
    private const string UpdateManifestUrlEnv = "MulletaFlix_UPDATE_MANIFEST_URL";

    private readonly IServerApplicationHost _applicationHost;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<UpdateInfoController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateInfoController"/> class.
    /// </summary>
    /// <param name="applicationHost">The application host.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="logger">The logger.</param>
    public UpdateInfoController(
        IServerApplicationHost applicationHost,
        IHttpClientFactory httpClientFactory,
        ILogger<UpdateInfoController> logger)
    {
        _applicationHost = applicationHost;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Gets current vs. available server version and changelog.
    /// </summary>
    /// <response code="200">Update information returned.</response>
    /// <response code="403">User does not have permission to check for updates.</response>
    /// <returns>An <see cref="UpdateInfoDto"/> with update availability.</returns>
    [HttpGet("UpdateInfo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UpdateInfoDto>> GetUpdateInfo(CancellationToken cancellationToken)
    {
        var info = new UpdateInfoDto
        {
            CurrentVersion = _applicationHost.ApplicationVersionString
        };

        var manifestUrl = Environment.GetEnvironmentVariable(UpdateManifestUrlEnv);
        if (string.IsNullOrWhiteSpace(manifestUrl))
        {
            return info;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var manifest = await client.GetFromJsonAsync<ServerUpdateManifest>(manifestUrl, cancellationToken).ConfigureAwait(false);

            if (manifest is not null
                && !string.IsNullOrWhiteSpace(manifest.Version)
                && Version.TryParse(manifest.Version, out var remoteVersion))
            {
                info.AvailableVersion = remoteVersion.ToString(3);
                info.UpdateAvailable = remoteVersion > _applicationHost.ApplicationVersion;
                info.Changelog = manifest.Changelog;
            }

            info.LastCheckedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            // The update center must not fail the request when the manifest is unreachable.
            // Return current version with no available update info.
            _logger.LogWarning(ex, "Failed to fetch server update manifest from {ManifestUrl}.", manifestUrl);
        }

        return info;
    }

    private sealed class ServerUpdateManifest
    {
        public string Version { get; set; } = string.Empty;

        public string ArchiveUrl { get; set; } = string.Empty;

        public string? Checksum { get; set; }

        public string? Changelog { get; set; }
    }
}
