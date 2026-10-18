using System;
using System.Security.Claims;
using System.Text.Json;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Model.Branding;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class BrandingControllerPrivacyTests
{
    [Fact]
    public void GetBrandingOptions_DoesNotExposeServerIntroPath()
    {
        const string privateIntroPath = @"D:\private\media\intro.mp4";
        var configurationManager = new Mock<IServerConfigurationManager>(MockBehavior.Strict);
        configurationManager.Setup(manager => manager.GetConfiguration("branding"))
            .Returns(new BrandingOptions
            {
                IntroEnabled = true,
                IntroPath = privateIntroPath,
                DefaultTheme = "dark"
            });
        var controller = new BrandingController(configurationManager.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = controller.GetBrandingOptions();

        var dto = Assert.IsType<BrandingOptionsDto>(result.Value);
        Assert.True(dto.IntroEnabled);
        Assert.Equal("dark", dto.DefaultTheme);
        Assert.Null(dto.IntroPath);
        Assert.DoesNotContain(privateIntroPath, JsonSerializer.Serialize(dto), StringComparison.Ordinal);
    }

    [Fact]
    public void GetBrandingOptions_AdministratorCanReadIntroPath()
    {
        const string privateIntroPath = @"D:\private\media\intro.mp4";
        var configurationManager = new Mock<IServerConfigurationManager>(MockBehavior.Strict);
        configurationManager.Setup(manager => manager.GetConfiguration("branding"))
            .Returns(new BrandingOptions { IntroPath = privateIntroPath });
        var controller = new BrandingController(configurationManager.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.Role, UserRoles.Administrator)],
                        "TestAuth"))
                }
            }
        };

        var result = controller.GetBrandingOptions();

        Assert.Equal(privateIntroPath, result.Value?.IntroPath);
    }
}
