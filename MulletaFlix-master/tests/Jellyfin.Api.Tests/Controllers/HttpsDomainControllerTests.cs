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

    [Fact]
    public void Configure_WithValidInput_PersistsConfigurationAndReturnsStatus()
    {
        var certificateInspector = new Mock<IHttpsCertificateInspector>(MockBehavior.Strict);
        var configuration = new Mock<MediaBrowser.Controller.Configuration.IServerConfigurationManager>(MockBehavior.Strict);

        // Initial load returns null
        configuration
            .Setup(m => m.GetConfiguration("httpsdomain"))
            .Returns(null!);

        // After save, return the persisted config
        HttpsDomainConfiguration? savedConfig = null;
        configuration
            .Setup(m => m.SaveConfiguration(It.IsAny<string>(), It.IsAny<object>()))
            .Callback<string, object>((key, cfg) => savedConfig = cfg as HttpsDomainConfiguration);

        configuration
            .Setup(m => m.GetConfiguration("httpsdomain"))
            .Returns(() => savedConfig ?? new HttpsDomainConfiguration());

        var controller = new HttpsDomainController(configuration.Object, certificateInspector.Object);

        var request = new HttpsDomainConfigureRequestDto
        {
            DuckDnsSubdomain = "mulletaflix",
            AcmeEmail = "admin@example.com",
            DuckDnsToken = "secret-token-value"
        };

        var result = controller.Configure(request);

        Assert.NotNull(result);
        var dto = Assert.IsType<HttpsDomainStatusDto>(Assert.IsType<OkResult<HttpsDomainStatusDto>>(result.Result).Value);
        Assert.True(dto.Configured);
        Assert.Equal("mulletaflix", dto.DuckDnsSubdomain);
        Assert.Equal("admin@example.com", dto.AcmeEmail);
        Assert.True(dto.TokenConfigured);
        // Verify token is never exposed
        Assert.DoesNotContain("secret-token-value", JsonSerializer.Serialize(dto), StringComparison.Ordinal);
    }

    [Fact]
    public void Configure_IsIdempotent_CallingTwiceWithSameDataDoesNotDuplicate()
    {
        var certificateInspector = new Mock<IHttpsCertificateInspector>(MockBehavior.Strict);
        var configuration = new Mock<MediaBrowser.Controller.Configuration.IServerConfigurationManager>(MockBehavior.Strict);

        var existingConfig = new HttpsDomainConfiguration
        {
            DuckDnsSubdomain = "mulletaflix",
            AcmeEmail = "admin@example.com",
            DuckDnsToken = "same-token-value",
            LastUpdatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        configuration
            .Setup(m => m.GetConfiguration("httpsdomain"))
            .Returns(existingConfig);

        // SaveConfiguration should never be called when data is identical
        configuration
            .Setup(m => m.SaveConfiguration("httpsdomain", It.IsAny<HttpsDomainConfiguration>()))
            .Throws(new InvalidOperationException("SaveConfiguration should not be called for idempotent request"));

        var controller = new HttpsDomainController(configuration.Object, certificateInspector.Object);

        var request = new HttpsDomainConfigureRequestDto
        {
            DuckDnsSubdomain = "mulletaflix",
            AcmeEmail = "admin@example.com",
            DuckDnsToken = "same-token-value"
        };

        var result = controller.Configure(request);

        var dto = Assert.IsType<HttpsDomainStatusDto>(Assert.IsType<OkResult<HttpsDomainStatusDto>>(result.Result).Value);
        Assert.True(dto.Configured);
        // Verify that LastUpdatedUtc is unchanged (proves no save happened)
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), dto.LastUpdatedUtc);
    }

    [Theory]
    [InlineData("", "admin@example.com", "token")]
    [InlineData("sub", "", "token")]
    [InlineData("sub", "admin@example.com", "")]
    public void Configure_WithMissingRequiredFields_ReturnsBadRequest(string subdomain, string email, string token)
    {
        var certificateInspector = new Mock<IHttpsCertificateInspector>(MockBehavior.Strict);
        var configuration = new Mock<MediaBrowser.Controller.Configuration.IServerConfigurationManager>(MockBehavior.Strict);
        configuration
            .Setup(m => m.GetConfiguration("httpsdomain"))
            .Returns(null!);
        var controller = new HttpsDomainController(configuration.Object, certificateInspector.Object);

        var request = new HttpsDomainConfigureRequestDto
        {
            DuckDnsSubdomain = subdomain,
            AcmeEmail = email,
            DuckDnsToken = token
        };

        var result = controller.Configure(request);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData("invalid-subdomain-with-underscore_", "admin@example.com", "token")]
    [InlineData("-invalidstart", "admin@example.com", "token")]
    [InlineData("invalidend-", "admin@example.com", "token")]
    [InlineData("invalid..double", "admin@example.com", "token")]
    public void Configure_WithMalformedSubdomain_ReturnsBadRequest(string subdomain, string email, string token)
    {
        var certificateInspector = new Mock<IHttpsCertificateInspector>(MockBehavior.Strict);
        var configuration = new Mock<MediaBrowser.Controller.Configuration.IServerConfigurationManager>(MockBehavior.Strict);
        configuration
            .Setup(m => m.GetConfiguration("httpsdomain"))
            .Returns(null!);
        var controller = new HttpsDomainController(configuration.Object, certificateInspector.Object);

        var request = new HttpsDomainConfigureRequestDto
        {
            DuckDnsSubdomain = subdomain,
            AcmeEmail = email,
            DuckDnsToken = token
        };

        var result = controller.Configure(request);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData("subdomain", "not-an-email")]
    [InlineData("subdomain", "admin@.com")]
    [InlineData("subdomain", "@example.com")]
    public void Configure_WithMalformedEmail_ReturnsBadRequest(string subdomain, string email)
    {
        var certificateInspector = new Mock<IHttpsCertificateInspector>(MockBehavior.Strict);
        var configuration = new Mock<MediaBrowser.Controller.Configuration.IServerConfigurationManager>(MockBehavior.Strict);
        configuration
            .Setup(m => m.GetConfiguration("httpsdomain"))
            .Returns(null!);
        var controller = new HttpsDomainController(configuration.Object, certificateInspector.Object);

        var request = new HttpsDomainConfigureRequestDto
        {
            DuckDnsSubdomain = subdomain,
            AcmeEmail = email,
            DuckDnsToken = "valid-token"
        };

        var result = controller.Configure(request);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public void Configure_NeverExposesTokenInResponseJson()
    {
        var certificateInspector = new Mock<IHttpsCertificateInspector>(MockBehavior.Strict);
        var configuration = new Mock<MediaBrowser.Controller.Configuration.IServerConfigurationManager>(MockBehavior.Strict);

        HttpsDomainConfiguration? savedConfig = null;
        configuration
            .Setup(m => m.GetConfiguration("httpsdomain"))
            .Returns(null!);

        configuration
            .Setup(m => m.SaveConfiguration(It.IsAny<string>(), It.IsAny<object>()))
            .Callback<string, object>((key, cfg) => savedConfig = cfg as HttpsDomainConfiguration);

        configuration
            .Setup(m => m.GetConfiguration("httpsdomain"))
            .Returns(() => savedConfig ?? new HttpsDomainConfiguration());

        var controller = new HttpsDomainController(configuration.Object, certificateInspector.Object);

        var sensitiveToken = "super-secret-duckdns-token-xyz789";
        var request = new HttpsDomainConfigureRequestDto
        {
            DuckDnsSubdomain = "test",
            AcmeEmail = "admin@test.com",
            DuckDnsToken = sensitiveToken
        };

        var result = controller.Configure(request);

        var dto = Assert.IsType<HttpsDomainStatusDto>(Assert.IsType<OkResult<HttpsDomainStatusDto>>(result.Result).Value);
        var responseJson = JsonSerializer.Serialize(dto);

        Assert.DoesNotContain(sensitiveToken, responseJson, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", responseJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("xyz789", responseJson, StringComparison.Ordinal);
        Assert.True(dto.TokenConfigured);
        Assert.NotEmpty(dto.MaskedToken);
        Assert.NotEqual(sensitiveToken, dto.MaskedToken);
    }

    [Fact]
    public void Configure_WithValidInputUpdatesTimestamp()
    {
        var certificateInspector = new Mock<IHttpsCertificateInspector>(MockBehavior.Strict);
        var configuration = new Mock<MediaBrowser.Controller.Configuration.IServerConfigurationManager>(MockBehavior.Strict);

        var oldConfig = new HttpsDomainConfiguration
        {
            DuckDnsSubdomain = "old",
            AcmeEmail = "old@example.com",
            DuckDnsToken = "old-token",
            LastUpdatedUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        HttpsDomainConfiguration? savedConfig = oldConfig;
        configuration
            .Setup(m => m.GetConfiguration("httpsdomain"))
            .Returns(() => savedConfig ?? oldConfig);

        configuration
            .Setup(m => m.SaveConfiguration(It.IsAny<string>(), It.IsAny<object>()))
            .Callback<string, object>((key, cfg) => savedConfig = cfg as HttpsDomainConfiguration);

        var beforeCall = DateTime.UtcNow;
        var controller = new HttpsDomainController(configuration.Object, certificateInspector.Object);

        var request = new HttpsDomainConfigureRequestDto
        {
            DuckDnsSubdomain = "new",
            AcmeEmail = "new@example.com",
            DuckDnsToken = "new-token"
        };

        var result = controller.Configure(request);
        var afterCall = DateTime.UtcNow;

        var dto = Assert.IsType<HttpsDomainStatusDto>(Assert.IsType<OkResult<HttpsDomainStatusDto>>(result.Result).Value);
        Assert.NotNull(dto.LastUpdatedUtc);
        Assert.True(dto.LastUpdatedUtc >= beforeCall && dto.LastUpdatedUtc <= afterCall);
    }

    [Fact]
    public void Configure_WithNullRequest_ReturnsBadRequest()
    {
        var certificateInspector = new Mock<IHttpsCertificateInspector>(MockBehavior.Strict);
        var configuration = new Mock<MediaBrowser.Controller.Configuration.IServerConfigurationManager>(MockBehavior.Strict);
        var controller = new HttpsDomainController(configuration.Object, certificateInspector.Object);

        var result = controller.Configure(null!);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public void Configure_ValidatesSubdomainLengthLimit()
    {
        var certificateInspector = new Mock<IHttpsCertificateInspector>(MockBehavior.Strict);
        var configuration = new Mock<MediaBrowser.Controller.Configuration.IServerConfigurationManager>(MockBehavior.Strict);
        var controller = new HttpsDomainController(configuration.Object, certificateInspector.Object);

        // 64 characters is too long (max is 63)
        var tooLongSubdomain = new string('a', 64);

        var request = new HttpsDomainConfigureRequestDto
        {
            DuckDnsSubdomain = tooLongSubdomain,
            AcmeEmail = "admin@example.com",
            DuckDnsToken = "token"
        };

        var result = controller.Configure(request);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("test")]
    [InlineData("test-subdomain")]
    [InlineData("test123")]
    [InlineData("1234")]
    public void Configure_AcceptsValidSubdomains(string validSubdomain)
    {
        var certificateInspector = new Mock<IHttpsCertificateInspector>(MockBehavior.Strict);
        var configuration = new Mock<MediaBrowser.Controller.Configuration.IServerConfigurationManager>(MockBehavior.Strict);

        HttpsDomainConfiguration? savedConfig = null;
        configuration
            .Setup(m => m.GetConfiguration("httpsdomain"))
            .Returns(null!);

        configuration
            .Setup(m => m.SaveConfiguration(It.IsAny<string>(), It.IsAny<object>()))
            .Callback<string, object>((key, cfg) => savedConfig = cfg as HttpsDomainConfiguration);

        configuration
            .Setup(m => m.GetConfiguration("httpsdomain"))
            .Returns(() => savedConfig ?? new HttpsDomainConfiguration());

        var controller = new HttpsDomainController(configuration.Object, certificateInspector.Object);

        var request = new HttpsDomainConfigureRequestDto
        {
            DuckDnsSubdomain = validSubdomain,
            AcmeEmail = "admin@example.com",
            DuckDnsToken = "token"
        };

        var result = controller.Configure(request);

        var okResult = Assert.IsType<OkResult<HttpsDomainStatusDto>>(result.Result);
        var dto = Assert.IsType<HttpsDomainStatusDto>(okResult.Value);
        Assert.True(dto.Configured);
        Assert.Equal(validSubdomain, dto.DuckDnsSubdomain);
    }
}
