namespace MediaBrowser.Controller.HttpsDomain;

/// <summary>Result of a local-only certificate file inspection; never performs a network call.</summary>
public sealed class HttpsCertificateInspectionResult
{
    /// <summary>Gets or sets a value indicating whether a certificate file was found at the configured path.</summary>
    public bool Found { get; set; }

    /// <summary>Gets or sets a value indicating whether the file existed but could not be parsed as a certificate.</summary>
    public bool ReadError { get; set; }

    /// <summary>Gets or sets the certificate's expiration instant (UTC), when known.</summary>
    public System.DateTime? NotAfterUtc { get; set; }

    /// <summary>Gets or sets the certificate subject, when known.</summary>
    public string Subject { get; set; } = string.Empty;
}

/// <summary>
/// Inspects a locally configured certificate file to report its validity for the HTTPS domain
/// status endpoint (T9.2). Implementations must only read the file the operator already placed on
/// disk; they must never request, renew or otherwise mutate any certificate, DNS record or network
/// resource.
/// </summary>
public interface IHttpsCertificateInspector
{
    /// <summary>Inspects the certificate file at <paramref name="certificatePath"/>, if any.</summary>
    /// <param name="certificatePath">Local filesystem path to a certificate file (PEM/CRT, no private key required).</param>
    /// <returns>The inspection result; <see cref="HttpsCertificateInspectionResult.Found"/> is false when the path does not exist.</returns>
    HttpsCertificateInspectionResult Inspect(string certificatePath);
}
