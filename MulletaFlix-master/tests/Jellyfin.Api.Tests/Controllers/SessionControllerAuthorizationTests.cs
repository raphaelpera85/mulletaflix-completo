using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Session;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using MulletaFlix.Data;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Server.Implementations.Users;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public class SessionControllerAuthorizationTests
{
    [Fact]
    public async Task SendPlaystateCommand_ForbidsTargetSessionNotAuthorizedForRemoteControl()
    {
        var user = CreateUser();
        var sessionManager = new Mock<ISessionManager>();
        var userManager = new Mock<IUserManager>();
        userManager.Setup(manager => manager.GetUserById(user.Id)).Returns(user);
        var controllerSession = new SessionInfo(sessionManager.Object, NullLogger.Instance)
        {
            Id = "controller-session",
            UserId = user.Id
        };
        sessionManager.Setup(manager => manager.LogSessionActivity(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                user))
            .ReturnsAsync(controllerSession);
        sessionManager.Setup(manager => manager.GetSessions(
                user.Id,
                string.Empty,
                null,
                user.Id,
                false))
            .Returns(Array.Empty<SessionInfoDto>());
        sessionManager.Setup(manager => manager.SendPlaystateCommand(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<PlaystateRequest>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = new SessionController(sessionManager.Object, userManager.Object);
        var identity = new ClaimsIdentity(
            [
                new Claim(InternalClaimTypes.UserId, user.Id.ToString("N")),
                new Claim(InternalClaimTypes.IsApiKey, bool.FalseString)
            ],
            "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        var result = await controller.SendPlaystateCommand(
            "uncontrolled-session",
            PlaystateCommand.Stop,
            null,
            null);

        Assert.IsType<ForbidResult>(result);
        sessionManager.Verify(
            manager => manager.SendPlaystateCommand(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<PlaystateRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static User CreateUser()
    {
        var user = new User(
            "reader",
            typeof(DefaultAuthenticationProvider).FullName!,
            typeof(DefaultPasswordResetProvider).FullName!);
        user.AddDefaultPermissions();
        user.AddDefaultPreferences();
        return user;
    }
}
