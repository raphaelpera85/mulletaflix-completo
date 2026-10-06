using System;

namespace MediaBrowser.Model.Configuration;

/// <summary>
/// Persisted administrative configuration for the optional DuckDNS/HTTPS domain integration (T9).
/// This configuration only stores operator-provided values; it does not perform any DNS, ACME or
/// firewall action by itself. Provisioning/renewal actions belong to a separate slice (T9.3).
/// </summary>
public class HttpsDomainConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether the public domain/HTTPS integration is enabled.
    /// Disabled by default: an optional integration must not change behavior on fresh installs.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the DuckDNS subdomain label only (e.g. "mulletaflix"), without the
    /// ".duckdns.org" suffix. Not a secret.
    /// </summary>
    public string DuckDnsSubdomain { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the contact e-mail used for ACME (Let's Encrypt) issuance notices. Not a secret,
    /// but still operator-identifying information.
    /// </summary>
    public string AcmeEmail { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the DuckDNS update token. SECRET: must never be echoed back in API responses or
    /// written to logs. The DuckDNS protocol itself requires sending this token as an HTTPS query
    /// parameter when calling DuckDNS's own update endpoint (https://www.duckdns.org/spec.jsp) -
    /// that single outbound request is an explicit, documented protocol exception and is not
    /// performed by this configuration or by any code added for this status endpoint.
    /// </summary>
    public string DuckDnsToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional local filesystem path to the certificate file (PEM/CRT, no private
    /// key required) used by the reverse proxy, so the status endpoint can report its expiration
    /// locally. Reading this file is a local inspection only; it never triggers a certificate
    /// request, renewal or any network call.
    /// </summary>
    public string CertificatePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC timestamp of the last time this configuration was saved by an
    /// administrator, for display purposes only.
    /// </summary>
    public DateTime? LastUpdatedUtc { get; set; }
}
