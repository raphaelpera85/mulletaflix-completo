using System;
using System.Text.Json;
using MediaBrowser.Controller.HttpsDomain;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.HttpsDomain;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MulletaFlix.Api.Controllers;
using MulletaFlix.Api.Results;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class HttpsDomainControllerTests
{
    [Fact]
    public void GetStatus_WhenNothingConfigured_ReportsNotConfiguredAndNoCertificate()
    {
        var certificateInspector = new Mock<IHttpsCertificateInspector>(MockBehavior.Strict);
        var dto = HttpsDomainController.BuildStatus(new HttpsDomainConfiguration(), certificateInspector.Object);

        Assert.False(dto.Enabled);
        Assert.False(dto.Configured);
        Assert.False(dto.TokenConfigured);
        Assert.Equal(string.Empty, dto.DuckDnsSubdomain);
        Assert.Equal(string.Empty, dto.FullDomain);
        Assert.Equal(HttpsDnsStatus.NotConfigured, dto.DnsStatus);
        Assert.Equal(HttpsCertificateStatus.NotConfigured, dto.CertificateStatus);
        Assert.Equal("••••", dto.MaskedToken);
        certificateInspector.VerifyAll();
    }

    [Fact]
    public void GetStatus_WhenFullyConfigured_ReportsConfiguredWithoutRealDnsOrCertificateCall()
    {
        var certificateInspector = new Mock<IHttpsCertificateInspector>(MockBehavior.Strict);
        certificateInspector
            .Setup(i => i.Inspect(@"C:\certs\mulletaflix.pem"))
            .Returns(new HttpsCertificateInspectionResult
            {
                Found = true,
                Subject = "CN=mulletaflix.duckdns.org",
                NotAfterUtc = DateTime.UtcNow.AddDays(90)
            });

        var config = new HttpsDomainConfiguration
        {
            Enabled = true,
            DuckDnsSubdomain = "mulletaflix",
            AcmeEmail = "admin@example.com",
            DuckDnsToken = "abcd1234-super-secret-token-ef56",
            CertificatePath = @"C:\certs\mulletaflix.pem",
            LastUpdatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        var dto = HttpsDomainController.BuildStatus(config, certificateInspector.Object);

        Assert.True(dto.Enabled);
        Assert.True(dto.Configured);
        Assert.True(dto.TokenConfigured);
        Assert.Equal("mulletaflix", dto.DuckDnsSubdomain);
        Assert.Equal("mulletaflix.duckdns.org", dto.FullDomain);
        Assert.Equal("admin@example.com", dto.AcmeEmail);
        // DNS is reported as Unknown precisely because this endpoint performs no live query.
        Assert.Equal(HttpsDnsStatus.Unknown, dto.DnsStatus);
        Assert.Equal(HttpsCertificateStatus.Active, dto.CertificateStatus);
        Assert.Equal("CN=mulletaflix.duckdns.org", dto.CertificateSubject);
        Assert.NotNull(dto.CertificateExpiresUtc);
        certificateInspector.VerifyAll();
    }

    [Theory]
    [InlineData("abcd1234-super-secret-token-ef56")]
    [InlineData("tok3n")]
    public void GetStatus_NeverSerializesTheRawTokenAnywhereInTheResponse(string rawToken)
    {
        var certificateInspector = new Mock<IHttpsCertificateInspector>(MockBehavior.Strict);
        var config = new HttpsDomainConfiguration
        {
            DuckDnsSubdomain = "mulletaflix",
            AcmeEmail = "admin@example.com",
            DuckDnsToken = rawToken
        };

        var dto = HttpsDomainController.BuildStatus(config, certificateInspector.Object);
        var json = JsonSerializer.Serialize(dto);

        Assert.DoesNotContain(rawToken, json, StringComparison.Ordinal);
        Assert.True(dto.TokenConfigured);
        Assert.NotEqual(string.Empty, dto.MaskedToken);
        Assert.DoesNotContain(rawToken, dto.MaskedToken, StringComparison.Ordinal);
    }

    [Fact]
    public void GetStatus_WhenCertificatePathIsConfiguredButFileIsMissing_ReportsNotFound()
    {
        var certificateInspector = new Mock<IHttpsCertificateInspector>(MockBehavior.Strict);
        certificateInspector
            .Setup(i => i.Inspect(@"C:\certs\missing.pem"))
            .Returns(new HttpsCertificateInspectionResult { Found = false });

        var config = new HttpsDomainConfiguration { CertificatePath = @"C:\certs\missing.pem" };
        var dto = HttpsDomainController.BuildStatus(config, certificateInspector.Object);

        Assert.Equal(HttpsCertificateStatus.NotFound, dto.CertificateStatus);
        certificateInspector.VerifyAll();
    }

    [Fact]
    public void GetStatus_WhenCertificateIsExpired_ReportsExpired()
    {
        var certificateInspector = new Mock<IHttpsCertificateInspector>(MockBehavior.Strict);
        certificateInspector
            .Setup(i => i.Inspect(@"C:\certs\old.pem"))
            .Returns(new HttpsCertificateInspectionResult
            {
                Found = true,
                NotAfterUtc = DateTime.UtcNow.AddDays(-1)
            });

        var config = new HttpsDomainConfiguration { CertificatePath = @"C:\certs\old.pem" };
        var dto = HttpsDomainController.BuildStatus(config, certificateInspector.Object);

        Assert.Equal(HttpsCertificateStatus.Expired, dto.CertificateStatus);
    }

    [Fact]
    public void GetStatus_WhenCertificateExpiresSoon_ReportsExpiringSoon()
    {
        var certificateInspector = new Mock<IHttpsCertificateInspector>(MockBehavior.Strict);
        certificateInspector
            .Setup(i => i.Inspect(@"C:\certs\soon.pem"))
            .Returns(new HttpsCertificateInspectionResult
            {
                Found = true,
                NotAfterUtc = DateTime.UtcNow.AddDays(5)
            });

        var config = new HttpsDomainConfiguration { CertificatePath = @"C:\certs\soon.pem" };
        var dto = HttpsDomainController.BuildStatus(config, certificateInspector.Object);

        Assert.Equal(HttpsCertificateStatus.ExpiringSoon, dto.CertificateStatus);
    }

    [Fact]
    public void GetStatus_WhenCertificateFileCannotBeParsed_ReportsReadError()
    {
        var certificateInspector = new Mock<IHttpsCertificateInspector>(MockBehavior.Strict);
        certificateInspector
            .Setup(i => i.Inspect(@"C:\certs\corrupt.pem"))
            .Returns(new HttpsCertificateInspectionResult { ReadError = true });

        var config = new HttpsDomainConfiguration { CertificatePath = @"C:\certs\corrupt.pem" };
        var dto = HttpsDomainController.BuildStatus(config, certificateInspector.Object);

        Assert.Equal(HttpsCertificateStatus.ReadError, dto.CertificateStatus);
    }

    [Fact]
    public void GetStatus_ControllerEndpoint_ReadsConfigurationAndReturnsDto()
    {
        var certificateInspector = new Mock<IHttpsCertificateInspector>(MockBehavior.Strict);
        var configuration = new Mock<MediaBrowser.Controller.Configuration.IServerConfigurationManager>(MockBehavior.Strict);
        configuration.Setup(m => m.GetConfiguration("httpsdomain")).Returns(new HttpsDomainConfiguration
        {
            DuckDnsSubdomain = "example",
            AcmeEmail = "admin@example.com",
            DuckDnsToken = "irrelevant-token"
        });
        var controller = new HttpsDomainController(configuration.Object, certificateInspector.Object);

        var result = controller.GetStatus();

        var dto = Assert.IsType<HttpsDomainStatusDto>(Assert.IsType<OkResult<HttpsDomainStatusDto>>(result.Result).Value);
        Assert.True(dto.Configured);
        Assert.Equal("example.duckdns.org", dto.FullDomain);
        Assert.DoesNotContain("irrelevant-token", JsonSerializer.Serialize(dto), StringComparison.Ordinal);
    }

    [Fact]
    public void GetStatus_ControllerEndpoint_WhenConfigurationMissing_ReportsNotConfigured()
    {
        var certificateInspector = new Mock<IHttpsCertificateInspector>(MockBehavior.Strict);
        var configuration = new Mock<MediaBrowser.Controller.Configuration.IServerConfigurationManager>(MockBehavior.Strict);
        configuration.Setup(m => m.GetConfiguration("httpsdomain")).Returns(null!);
        var controller = new HttpsDomainController(configuration.Object, certificateInspector.Object);

        var result = controller.GetStatus();

        var dto = Assert.IsType<HttpsDomainStatusDto>(Assert.IsType<OkResult<HttpsDomainStatusDto>>(result.Result).Value);
        Assert.False(dto.Configured);
        Assert.Equal(HttpsCertificateStatus.NotConfigured, dto.CertificateStatus);
    }
}
