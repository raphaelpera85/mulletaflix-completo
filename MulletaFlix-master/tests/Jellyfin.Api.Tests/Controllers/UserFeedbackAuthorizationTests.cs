using System.Linq;
using System.Reflection;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;
using MulletaFlix.Api.Controllers;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public class UserFeedbackAuthorizationTests
{
    [Fact]
    public void UserFeedbackEndpoints_RequireAuthenticationAndCatalogRequiresElevation()
    {
        var controllerType = typeof(UserFeedbackController);
        Assert.Contains(controllerType.GetCustomAttributes<AuthorizeAttribute>(inherit: true), _ => true);
        Assert.Empty(controllerType.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true));

        var routeMethods = controllerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any())
            .ToArray();
        Assert.NotEmpty(routeMethods);
        Assert.All(routeMethods, method =>
            Assert.Empty(method.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true)));

        var catalogMethod = controllerType.GetMethod(
            nameof(UserFeedbackController.GetMediaRequestCatalog),
            BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(catalogMethod);
        Assert.Contains(
            catalogMethod!.GetCustomAttributes<AuthorizeAttribute>(inherit: true),
            attribute => attribute.Policy == Policies.RequiresElevation);
    }
}
