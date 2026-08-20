using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Compatibility;

public class CompatibilityFacadeTests
{
    [Fact]
    public void JellyfinDataFacade_BindsLegacyEnumsAndEventsToImplementationAssembly()
    {
        var legacyNames = Enum.GetNames(typeof(Jellyfin.Data.Enums.BaseItemKind));
        var implementationNames = Enum.GetNames(typeof(MulletaFlix.Data.Enums.BaseItemKind));

        Assert.Equal(implementationNames, legacyNames);
    }

    [Fact]
    public void JellyfinDatabaseImplementationsFacade_ResolvesImplementationTypesByAssemblyName()
    {
        var accessSchedule = Type.GetType("MulletaFlix.Database.Implementations.Entities.AccessSchedule, Jellyfin.Database.Implementations");
        var userType = Type.GetType("MulletaFlix.Database.Implementations.Entities.User, Jellyfin.Database.Implementations");

        Assert.Equal(typeof(MulletaFlix.Database.Implementations.Entities.AccessSchedule), accessSchedule);
        Assert.Equal(typeof(MulletaFlix.Database.Implementations.Entities.User), userType);
    }
}
