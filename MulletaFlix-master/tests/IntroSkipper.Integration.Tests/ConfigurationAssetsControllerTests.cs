using System.IO;
using System.Reflection;
using IntroSkipper.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace IntroSkipper.Integration.Tests;

public sealed class ConfigurationAssetsControllerTests
{
    [Fact]
    public void ConfigurationPage_UsesTheAnonymousStaticAssetEndpoints()
    {
        using var stream = typeof(ConfigurationAssetsController).Assembly
            .GetManifestResourceStream("IntroSkipper.Configuration.configPage.html");

        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        var html = reader.ReadToEnd();

        Assert.Contains("/IntroSkipper/Configuration/introskipper.js", html, StringComparison.Ordinal);
        Assert.Contains("/IntroSkipper/Configuration/introskipper.css", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("GetJavaScript", "application/javascript", "IntroSkipper.Configuration.introskipper.js")]
    [InlineData("GetStylesheet", "text/css", "IntroSkipper.Configuration.introskipper.css")]
    public void ConfigurationAssets_AreAvailableWithoutDashboardAuthentication(string actionName, string contentType, string resourceName)
    {
        var route = typeof(ConfigurationAssetsController).GetCustomAttribute<Microsoft.AspNetCore.Mvc.RouteAttribute>();
        var action = typeof(ConfigurationAssetsController).GetMethod(actionName);

        Assert.Equal("IntroSkipper/Configuration", route?.Template);
        Assert.NotNull(action);
        Assert.Equal(
            resourceName.EndsWith(".js", StringComparison.Ordinal) ? "introskipper.js" : "introskipper.css",
            action!.GetCustomAttribute<HttpGetAttribute>()?.Template);
        Assert.NotNull(action!.GetCustomAttribute<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>());

        var controller = new ConfigurationAssetsController();
        var result = Assert.IsType<FileStreamResult>(Assert.IsAssignableFrom<IActionResult>(action.Invoke(controller, null)));
        Assert.Equal(contentType, result.ContentType);
        Assert.NotNull(typeof(ConfigurationAssetsController).Assembly.GetManifestResourceInfo(resourceName));
        result.FileStream.Dispose();
    }
}
