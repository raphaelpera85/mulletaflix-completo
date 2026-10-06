using System.ComponentModel.DataAnnotations;

namespace MediaBrowser.Model.HttpsDomain;

/// <summary>
/// Request payload for configuring the DuckDNS/HTTPS domain integration (T9.2).
/// Contains operator-provided values for subdomain, ACME e-mail, and DuckDNS token.
/// The token is marked as sensitive and must never be logged or echoed back in responses.
/// </summary>
public sealed class HttpsDomainConfigureRequestDto
{
    /// <summary>
    /// Gets or sets the DuckDNS subdomain label only (e.g. "mulletaflix"), without the
    /// ".duckdns.org" suffix. Must contain only alphanumeric characters and hyphens.
    /// Required when configuring.
    /// </summary>
    [Required(ErrorMessage = "Subdomain is required")]
    [RegularExpression(
        @"^[a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?$",
        ErrorMessage = "Subdomain must contain only alphanumeric characters and hyphens, and be 1-63 characters")]
    public string DuckDnsSubdomain { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the contact e-mail used for ACME (Let's Encrypt) issuance notices.
    /// Must be a valid e-mail address. Required when configuring.
    /// </summary>
    [Required(ErrorMessage = "ACME e-mail is required")]
    [EmailAddress(ErrorMessage = "ACME e-mail must be a valid e-mail address")]
    public string AcmeEmail { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the DuckDNS update token. SECRET: must never be echoed back in API responses,
    /// written to logs, or included in any error messages or status reports.
    /// The token is required for configuration but the Configure endpoint must not repeat it.
    /// Required when configuring.
    /// </summary>
    [Required(ErrorMessage = "DuckDNS token is required")]
    public string DuckDnsToken { get; set; } = string.Empty;
}
