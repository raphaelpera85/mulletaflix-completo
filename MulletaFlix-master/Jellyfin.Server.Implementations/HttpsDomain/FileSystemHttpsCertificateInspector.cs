using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using MediaBrowser.Controller.HttpsDomain;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Server.Implementations.HttpsDomain;

/// <summary>
/// Default <see cref="IHttpsCertificateInspector"/> implementation that reads a certificate file
/// already present on local disk. Performs no network, ACME or DNS call; only file I/O and
/// certificate parsing.
/// </summary>
public sealed class FileSystemHttpsCertificateInspector : IHttpsCertificateInspector
{
    private readonly ILogger<FileSystemHttpsCertificateInspector> _logger;

    /// <summary>Initializes a new instance of the <see cref="FileSystemHttpsCertificateInspector"/> class.</summary>
    /// <param name="logger">Logger instance.</param>
    public FileSystemHttpsCertificateInspector(ILogger<FileSystemHttpsCertificateInspector> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public HttpsCertificateInspectionResult Inspect(string certificatePath)
    {
        var result = new HttpsCertificateInspectionResult();
        if (string.IsNullOrWhiteSpace(certificatePath) || !File.Exists(certificatePath))
        {
            return result;
        }

        try
        {
            using var certificate = X509CertificateLoader.LoadCertificateFromFile(certificatePath);
            result.Found = true;
            result.NotAfterUtc = certificate.NotAfter.ToUniversalTime();
            result.Subject = certificate.Subject;
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException or FormatException)
        {
            // Never log the certificate path contents or any secret; only note that parsing failed.
            _logger.LogWarning(ex, "Could not parse the configured HTTPS certificate file for status reporting.");
            result.ReadError = true;
        }

        return result;
    }
}
