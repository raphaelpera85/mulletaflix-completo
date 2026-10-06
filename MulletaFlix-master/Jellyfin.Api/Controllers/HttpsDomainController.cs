using System;
using MediaBrowser.Common.Api;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.HttpsDomain;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.HttpsDomain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MulletaFlix.Api.Controllers;

/// <summary>
/// Read-only administrative status for the public domain/HTTPS integration (DuckDNS + reverse
/// proxy certificate). This is the T9.2 foundation: it only reports configuration/state already
/// stored on the server; it never triggers a DNS update, ACME request or firewall/proxy change.
/// </summary>
[Route("System/Https")]
[Authorize(Policy = Policies.RequiresElevation)]
public sealed class HttpsDomainController : BaseMulletaFlixApiController
{
    private const string ConfigurationKey = "httpsdomain";
    private static readonly TimeSpan ExpiringSoonThreshold = TimeSpan.FromDays(14);

    private readonly IServerConfigurationManager _configManager;
    private readonly IHttpsCertificateInspector _certificateInspector;

    /// <summary>Initializes a new instance of the <see cref="HttpsDomainController"/> class.</summary>
    /// <param name="configManager">Server configuration manager.</param>
    /// <param name="certificateInspector">Local-only certificate file inspector.</param>
    public HttpsDomainController(IServerConfigurationManager configManager, IHttpsCertificateInspector certificateInspector)
    {
        _configManager = configManager;
        _certificateInspector = certificateInspector;
    }

    /// <summary>Gets the current DuckDNS/HTTPS configuration status.</summary>
    /// <returns>A <see cref="HttpsDomainStatusDto"/> that never includes the DuckDNS token in plain text.</returns>
    [HttpGet("Status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<HttpsDomainStatusDto> GetStatus()
    {
        var config = _configManager.GetConfiguration<HttpsDomainConfiguration>(ConfigurationKey) ?? new HttpsDomainConfiguration();
        return Ok(BuildStatus(config, _certificateInspector));
    }

    /// <summary>
    /// Builds the status DTO from a configuration snapshot. Internal and static so it can be
    /// exercised directly by unit tests without standing up the full controller/DI pipeline.
    /// </summary>
    /// <param name="config">Persisted configuration.</param>
    /// <param name="certificateInspector">Local-only certificate file inspector.</param>
    /// <returns>The computed status DTO.</returns>
    internal static HttpsDomainStatusDto BuildStatus(HttpsDomainConfiguration config, IHttpsCertificateInspector certificateInspector)
    {
        var subdomain = (config.DuckDnsSubdomain ?? string.Empty).Trim();
        var email = (config.AcmeEmail ?? string.Empty).Trim();
        var token = config.DuckDnsToken ?? string.Empty;

        var hasSubdomain = !string.IsNullOrEmpty(subdomain);
        var hasEmail = !string.IsNullOrEmpty(email);
        var hasToken = !string.IsNullOrEmpty(token);

        var dto = new HttpsDomainStatusDto
        {
            Enabled = config.Enabled,
            Configured = hasSubdomain && hasEmail && hasToken,
            DuckDnsSubdomain = subdomain,
            FullDomain = hasSubdomain ? $"{subdomain}.duckdns.org" : string.Empty,
            AcmeEmail = email,
            TokenConfigured = hasToken,
            MaskedToken = MaskToken(token),
            DnsStatus = hasSubdomain ? HttpsDnsStatus.Unknown : HttpsDnsStatus.NotConfigured,
            LastUpdatedUtc = config.LastUpdatedUtc
        };

        ApplyCertificateStatus(dto, (config.CertificatePath ?? string.Empty).Trim(), certificateInspector);
        return dto;
    }

    private static void ApplyCertificateStatus(HttpsDomainStatusDto dto, string certificatePath, IHttpsCertificateInspector certificateInspector)
    {
        if (string.IsNullOrEmpty(certificatePath))
        {
            dto.CertificateStatus = HttpsCertificateStatus.NotConfigured;
            return;
        }

        var inspection = certificateInspector.Inspect(certificatePath);
        if (inspection.ReadError)
        {
            dto.CertificateStatus = HttpsCertificateStatus.ReadError;
            return;
        }

        if (!inspection.Found)
        {
            dto.CertificateStatus = HttpsCertificateStatus.NotFound;
            return;
        }

        dto.CertificateSubject = inspection.Subject;
        dto.CertificateExpiresUtc = inspection.NotAfterUtc;

        if (inspection.NotAfterUtc is not { } notAfter)
        {
            dto.CertificateStatus = HttpsCertificateStatus.Active;
            return;
        }

        var now = DateTime.UtcNow;
        dto.CertificateStatus = notAfter <= now
            ? HttpsCertificateStatus.Expired
            : notAfter - now <= ExpiringSoonThreshold
                ? HttpsCertificateStatus.ExpiringSoon
                : HttpsCertificateStatus.Active;
    }

    private static string MaskToken(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return "••••";
        }

        return token.Length > 4
            ? $"••••{token[^4..]}"
            : "••••";
    }
}
