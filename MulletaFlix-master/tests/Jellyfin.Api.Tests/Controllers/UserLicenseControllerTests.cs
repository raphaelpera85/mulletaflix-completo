using System;
using System.Security.Claims;
using System.Threading.Tasks;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using MulletaFlix.Api.Models.UserDtos;
using MulletaFlix.Database.Implementations.Entities;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class UserLicenseControllerTests
{
    private readonly Mock<IUserLicenseManager> _licenseManager;
    private readonly Mock<IUserManager> _userManager;
    private readonly Mock<ILogger<UserLicenseController>> _logger;
    private readonly Guid _adminId;
    private readonly UserLicenseController _controller;

    public UserLicenseControllerTests()
    {
        _licenseManager = new Mock<IUserLicenseManager>(MockBehavior.Loose);
        _userManager = new Mock<IUserManager>(MockBehavior.Loose);
        _logger = new Mock<ILogger<UserLicenseController>>(MockBehavior.Loose);
        _adminId = Guid.NewGuid();

        _controller = new UserLicenseController(
            _licenseManager.Object,
            _userManager.Object,
            _logger.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[]
                        {
                            new Claim(ClaimTypes.NameIdentifier, _adminId.ToString()),
                            new Claim(ClaimTypes.Role, UserRoles.Administrator)
                        },
                        "Custom"))
                }
            }
        };
    }

    [Fact]
    public async Task GetUserLicense_EmptyUserId_ReturnsBadRequest()
    {
        var result = await _controller.GetUserLicense(Guid.Empty);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task SetUserLicense_EmptyUserId_ReturnsBadRequest()
    {
        var result = await _controller.SetUserLicense(Guid.Empty, new SetUserLicenseRequest());

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task SetUserLicense_NullRequest_ReturnsBadRequest()
    {
        var result = await _controller.SetUserLicense(Guid.NewGuid(), null!);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-24)]
    public async Task SetUserLicense_NonPositiveDurationHours_ReturnsBadRequest(int durationHours)
    {
        var result = await _controller.SetUserLicense(
            Guid.NewGuid(),
            new SetUserLicenseRequest { DurationHours = durationHours });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task RevokeUserLicense_EmptyUserId_ReturnsBadRequest()
    {
        var result = await _controller.RevokeUserLicense(Guid.Empty);

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
