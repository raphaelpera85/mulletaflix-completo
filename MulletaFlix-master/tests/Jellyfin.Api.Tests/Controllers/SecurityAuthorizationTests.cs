using System;
using System.Linq;
using System.Reflection;
using MulletaFlix.Api.Controllers;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public class SecurityAuthorizationTests
{
    [Fact]
    public void LiveTvController_RequiresAuthorization()
    {
        AssertClassHasAuthorize(typeof(LiveTvController));
    }

    [Fact]
    public void VideoAttachmentsController_RequiresAuthorization()
    {
        AssertClassHasAuthorize(typeof(VideoAttachmentsController));
    }

    [Fact]
    public void StartupMutatingEndpoints_RequireLanAccess()
    {
        AssertMethodHasPolicy(typeof(StartupController), nameof(StartupController.CompleteWizard), Policies.AnonymousLanAccessPolicy);
        AssertMethodHasPolicy(typeof(StartupController), nameof(StartupController.UpdateInitialConfiguration), Policies.AnonymousLanAccessPolicy);
        AssertMethodHasPolicy(typeof(StartupController), nameof(StartupController.SetRemoteAccess), Policies.AnonymousLanAccessPolicy);
        AssertMethodHasPolicy(typeof(StartupController), nameof(StartupController.UpdateStartupUser), Policies.AnonymousLanAccessPolicy);
    }

    [Fact]
    public void SensitiveDiagnosticsAndPluginConfigurationEndpoints_RequireElevation()
    {
        AssertClassHasPolicy(typeof(ActivityLogController), Policies.RequiresElevation);
        AssertClassHasPolicy(typeof(BackupController), Policies.RequiresElevation);
        AssertClassHasPolicy(typeof(PlaybackReportsController), Policies.RequiresElevation);
        AssertClassHasPolicy(typeof(ServerHealthController), Policies.RequiresElevation);
        AssertClassHasPolicy(typeof(NebulaFtpController), Policies.RequiresElevation);
        AssertControllerRouteMethodsDoNotAllowAnonymous(typeof(BackupController));
        AssertMethodHasPolicy(typeof(DashboardController), nameof(DashboardController.GetConfigurationPages), Policies.RequiresElevation);
        AssertMethodHasPolicy(typeof(DashboardController), nameof(DashboardController.GetDashboardConfigurationPage), Policies.RequiresElevation);
        AssertMethodHasPolicy(typeof(SystemController), nameof(SystemController.GetSystemStorage), Policies.RequiresElevation);
        AssertMethodHasPolicy(typeof(SystemController), nameof(SystemController.GetServerLogs), Policies.RequiresElevation);
        AssertMethodHasPolicy(typeof(SystemController), nameof(SystemController.GetLogFile), Policies.RequiresElevation);
        AssertClassHasPolicy(typeof(EnvironmentController), Policies.FirstTimeSetupOrElevated);
    }

    [Fact]
    public void PluginManagementRoutesRequireElevationExceptPublicPluginImage()
    {
        var controllerType = typeof(PluginsController);
        var routeMethods = controllerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any())
            .ToArray();
        var anonymousRoutes = routeMethods
            .Where(method => method.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any())
            .ToArray();

        Assert.NotEmpty(routeMethods);
        Assert.Equal(new[] { nameof(PluginsController.GetPluginImage) }, anonymousRoutes.Select(method => method.Name));
        Assert.Contains(
            controllerType.GetCustomAttributes<AuthorizeAttribute>(inherit: true),
            attribute => attribute.Policy == Policies.RequiresElevation);
    }

    [Fact]
    public void ApiKeyManagementEndpoints_RequireElevation()
    {
        AssertMethodHasPolicy(typeof(ApiKeyController), nameof(ApiKeyController.GetKeys), Policies.RequiresElevation);
        AssertMethodHasPolicy(typeof(ApiKeyController), nameof(ApiKeyController.CreateKey), Policies.RequiresElevation);
        AssertMethodHasPolicy(typeof(ApiKeyController), nameof(ApiKeyController.RevokeKey), Policies.RequiresElevation);
    }

    [Fact]
    public void SessionStreamingEndpoints_RequireAuthorization()
    {
        AssertControllerRouteMethodsRequireAuthorization(typeof(SessionController));
        AssertClassHasAuthorize(typeof(MediaInfoController));
        AssertClassHasAuthorize(typeof(DynamicHlsController));
    }

    private static void AssertClassHasAuthorize(Type controllerType)
    {
        Assert.Contains(controllerType.GetCustomAttributes<AuthorizeAttribute>(inherit: true), _ => true);
    }

    private static void AssertControllerRouteMethodsRequireAuthorization(Type controllerType)
    {
        var routeMethods = controllerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any())
            .ToArray();

        Assert.NotEmpty(routeMethods);
        foreach (var method in routeMethods)
        {
            Assert.Contains(method.GetCustomAttributes<AuthorizeAttribute>(inherit: true), _ => true);
        }
    }

    private static void AssertControllerRouteMethodsDoNotAllowAnonymous(Type controllerType)
    {
        var routeMethods = controllerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any())
            .ToArray();

        Assert.NotEmpty(routeMethods);
        foreach (var method in routeMethods)
        {
            Assert.Empty(method.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true));
        }
    }

    private static void AssertClassHasPolicy(Type controllerType, string policy)
    {
        Assert.Contains(controllerType.GetCustomAttributes<AuthorizeAttribute>(inherit: true), attribute => attribute.Policy == policy);
    }

    private static void AssertMethodHasPolicy(Type controllerType, string methodName, string policy)
    {
        var method = controllerType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(method);
        Assert.Contains(method!.GetCustomAttributes<AuthorizeAttribute>(inherit: true), attribute => attribute.Policy == policy);
    }
}
