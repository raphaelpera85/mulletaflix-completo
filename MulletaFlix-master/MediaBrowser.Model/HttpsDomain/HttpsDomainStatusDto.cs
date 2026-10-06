using System;

namespace MediaBrowser.Model.HttpsDomain;

/// <summary>
/// DNS reporting state for the DuckDNS/HTTPS status endpoint. This endpoint never performs a real
/// DNS lookup; it only reports whether the operator has configured enough data for a future
/// provisioning step (T9.3) to attempt one.
/// </summary>
public enum HttpsDnsStatus
{
    /// <summary>No DuckDNS subdomain has been configured yet.</summary>
    NotConfigured,

    /// <summary>A subdomain is configured, but this status endpoint performs no live DNS query.</summary>
    Unknown
}

/// <summary>Certificate reporting state, derived from a local file read only (no ACME/network call).</summary>
public enum HttpsCertificateStatus
{
    /// <summary>No certificate path has been configured.</summary>
    NotConfigured,

    /// <summary>A certificate path is configured, but no file exists there yet.</summary>
    NotFound,

    /// <summary>A certificate file was read successfully and is currently valid.</summary>
    Active,

    /// <summary>The certificate is valid but will expire within 14 days.</summary>
    ExpiringSoon,

    /// <summary>The certificate's validity period has already ended.</summary>
    Expired,

    /// <summary>The configured certificate file exists but could not be parsed.</summary>
    ReadError
}

/// <summary>
/// Read-only administrative status for the DuckDNS/HTTPS domain integration (T9.2). Never includes
/// the DuckDNS token in plain text; <see cref="MaskedToken"/> only ever reveals, at most, its last
/// four characters.
/// </summary>
public sealed class HttpsDomainStatusDto
{
    /// <summary>Gets or sets a value indicating whether the integration is enabled by the operator.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets a value indicating whether subdomain, e-mail and token are all set.</summary>
    public bool Configured { get; set; }

    /// <summary>Gets or sets the configured DuckDNS subdomain label (not a secret).</summary>
    public string DuckDnsSubdomain { get; set; } = string.Empty;

    /// <summary>Gets or sets the full computed domain (subdomain + ".duckdns.org"), when configured.</summary>
    public string FullDomain { get; set; } = string.Empty;

    /// <summary>Gets or sets the configured ACME contact e-mail.</summary>
    public string AcmeEmail { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether a DuckDNS token is stored for this server.</summary>
    public bool TokenConfigured { get; set; }

    /// <summary>
    /// Gets or sets a masked representation of the stored token (e.g. "••••ab12"). Never the full
    /// token; empty/placeholder when no token is configured.
    /// </summary>
    public string MaskedToken { get; set; } = string.Empty;

    /// <summary>Gets or sets the reported DNS state. See <see cref="HttpsDnsStatus"/>.</summary>
    public HttpsDnsStatus DnsStatus { get; set; } = HttpsDnsStatus.NotConfigured;

    /// <summary>Gets or sets the reported certificate state. See <see cref="HttpsCertificateStatus"/>.</summary>
    public HttpsCertificateStatus CertificateStatus { get; set; } = HttpsCertificateStatus.NotConfigured;

    /// <summary>Gets or sets the certificate expiration (UTC), when a certificate could be read.</summary>
    public DateTime? CertificateExpiresUtc { get; set; }

    /// <summary>Gets or sets the certificate subject, when a certificate could be read.</summary>
    public string CertificateSubject { get; set; } = string.Empty;

    /// <summary>Gets or sets when this configuration was last saved by an administrator.</summary>
    public DateTime? LastUpdatedUtc { get; set; }
}
