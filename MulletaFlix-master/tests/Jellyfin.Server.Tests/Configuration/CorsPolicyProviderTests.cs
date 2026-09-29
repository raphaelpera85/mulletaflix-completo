using System.Threading.Tasks;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Model.Configuration;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;
using MulletaFlix.Server.Configuration;

namespace MulletaFlix.Server.Tests.Configuration;

/// <summary>
/// Tests for <see cref="CorsPolicyProvider"/>. This is the fix for SECURITY_AUDIT_REPORT.md
/// finding #2 ("CORS Misconfiguration - Allow Any Origin by Default"): an unconfigured or
/// wildcard-only <see cref="ServerConfiguration.CorsHosts"/> must NOT translate into an
/// allow-any-origin CORS policy with credentials — it must produce a same-origin-only policy
/// (no explicit origins, no <c>AllowCredentials</c>) instead.
/// </summary>
public sealed class CorsPolicyProviderTests
{
    private static CorsPolicyProvider CreateProvider(string[] corsHosts)
    {
        var configManager = new Mock<IServerConfigurationManager>();
        configManager.Setup(m => m.Configuration).Returns(new ServerConfiguration
        {
            CorsHosts = corsHosts
        });
        return new CorsPolicyProvider(configManager.Object);
    }

    [Fact]
    public async Task GetPolicyAsync_NoConfiguredHosts_DoesNotAllowCredentialsOrExplicitOrigins()
    {
        var provider = CreateProvider(System.Array.Empty<string>());

        var policy = await provider.GetPolicyAsync(new DefaultHttpContext(), policyName: null);

        Assert.NotNull(policy);
        Assert.False(policy!.SupportsCredentials);
        Assert.Empty(policy.Origins);
    }

    [Fact]
    public async Task GetPolicyAsync_WildcardOnlyHost_DoesNotAllowCredentialsOrExplicitOrigins()
    {
        // The historical default (ServerConfiguration.CorsHosts defaults to ["*"]) must not be
        // interpreted as "allow any origin with credentials" — that was the exact misconfiguration
        // SECURITY_AUDIT_REPORT.md finding #2 flagged as CVSS 7.5 (High).
        var provider = CreateProvider(new[] { "*" });

        var policy = await provider.GetPolicyAsync(new DefaultHttpContext(), policyName: null);

        Assert.NotNull(policy);
        Assert.False(policy!.SupportsCredentials);
        Assert.Empty(policy.Origins);
    }

    [Fact]
    public async Task GetPolicyAsync_ExplicitHosts_AllowsOnlyThoseOriginsWithCredentials()
    {
        var provider = CreateProvider(new[] { "https://mulletaflix.example.com", "https://app.mulletaflix.example.com" });

        var policy = await provider.GetPolicyAsync(new DefaultHttpContext(), policyName: null);

        Assert.NotNull(policy);
        Assert.True(policy!.SupportsCredentials);
        Assert.Equal(new[] { "https://mulletaflix.example.com", "https://app.mulletaflix.example.com" }, policy.Origins);
    }

    [Fact]
    public async Task GetPolicyAsync_AlwaysAllowsAnyMethodAndAnyHeader()
    {
        // AllowAnyMethod/AllowAnyHeader are safe regardless of the origin restriction above,
        // since the browser still enforces same-origin unless an explicit origin matched.
        var restrictedPolicy = await CreateProvider(System.Array.Empty<string>())
            .GetPolicyAsync(new DefaultHttpContext(), policyName: null);
        var openPolicy = await CreateProvider(new[] { "https://mulletaflix.example.com" })
            .GetPolicyAsync(new DefaultHttpContext(), policyName: null);

        Assert.True(restrictedPolicy!.AllowAnyMethod);
        Assert.True(restrictedPolicy.AllowAnyHeader);
        Assert.True(openPolicy!.AllowAnyMethod);
        Assert.True(openPolicy.AllowAnyHeader);
    }
}
