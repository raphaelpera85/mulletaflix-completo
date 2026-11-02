using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Emby.Server.Implementations.Devices;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Server.Implementations.Tests.Devices;

public sealed class DeviceIdTests : IDisposable
{
    private readonly string _dataPath = Path.Combine(Path.GetTempPath(), "mulletaflix-device-id-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Value_ConcurrentFirstReads_ReturnSamePersistedId()
    {
        var deviceId = CreateDeviceId();

        var values = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => deviceId.Value))).ConfigureAwait(false);

        Assert.All(values, value => Assert.Equal(values[0], value));
        Assert.True(Guid.TryParse(await File.ReadAllTextAsync(Path.Combine(_dataPath, "device.txt")).ConfigureAwait(false), out _));
        Assert.Equal(values[0], await File.ReadAllTextAsync(Path.Combine(_dataPath, "device.txt")).ConfigureAwait(false));
        Assert.Empty(Directory.EnumerateFiles(_dataPath, "*.tmp"));
    }

    [Fact]
    public void Value_ExistingId_RemainsStable()
    {
        Directory.CreateDirectory(_dataPath);
        var existingId = Guid.NewGuid().ToString("N");
        File.WriteAllText(Path.Combine(_dataPath, "device.txt"), existingId);
        var deviceId = CreateDeviceId();

        Assert.Equal(existingId, deviceId.Value);
        Assert.Equal(existingId, File.ReadAllText(Path.Combine(_dataPath, "device.txt")));
    }

    [Fact]
    public void Value_InvalidExistingId_ReplacesItWithGuid()
    {
        Directory.CreateDirectory(_dataPath);
        var path = Path.Combine(_dataPath, "device.txt");
        File.WriteAllText(path, "invalid id");
        var deviceId = CreateDeviceId();

        Assert.Equal(deviceId.Value, File.ReadAllText(path));
        Assert.True(Guid.TryParse(deviceId.Value, out _));
        Assert.Empty(Directory.EnumerateFiles(_dataPath, "*.tmp"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dataPath))
        {
            Directory.Delete(_dataPath, recursive: true);
        }
    }

    private DeviceId CreateDeviceId()
    {
        var appPaths = new Mock<IApplicationPaths>();
        appPaths.SetupGet(paths => paths.DataPath).Returns(_dataPath);
        return new DeviceId(appPaths.Object, NullLogger<DeviceId>.Instance);
    }
}
