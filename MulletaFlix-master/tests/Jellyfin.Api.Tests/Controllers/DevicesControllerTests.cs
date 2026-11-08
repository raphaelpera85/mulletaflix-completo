using System;
using System.Threading.Tasks;
using MulletaFlix.Api.Controllers;
using MulletaFlix.Data.Dtos;
using MediaBrowser.Controller.Devices;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public class DevicesControllerTests
{
    private readonly DevicesController _subject;
    private readonly Mock<IDeviceManager> _mockDeviceManager;
    private readonly Mock<ISessionManager> _mockSessionManager;

    public DevicesControllerTests()
    {
        _mockDeviceManager = new Mock<IDeviceManager>();
        _mockSessionManager = new Mock<ISessionManager>();

        _subject = new DevicesController(
            _mockDeviceManager.Object,
            _mockSessionManager.Object);
    }

    [Fact]
    public void GetDevices_WhenUserIdNotProvided_ReturnsAllDevices()
    {
        var expected = new QueryResult<DeviceInfoDto>(Array.Empty<DeviceInfoDto>());

        _mockDeviceManager
            .Setup(m => m.GetDevicesForUser(null))
            .Returns(expected);

        var result = _subject.GetDevices(null);

        var okResult = Assert.IsType<ActionResult<QueryResult<DeviceInfoDto>>>(result);
        Assert.Same(expected, okResult.Value);
        _mockDeviceManager.Verify(m => m.GetDevicesForUser(null), Times.Once);
    }

    [Fact]
    public void GetDevices_EmptyUserId_ReturnsBadRequest()
    {
        var result = _subject.GetDevices(Guid.Empty);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("User id must not be empty.", badRequest.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetDeviceInfo_NullOrWhitespaceId_ReturnsBadRequest(string? id)
    {
        var result = _subject.GetDeviceInfo(id!);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Device id is required.", badRequest.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetDeviceOptions_NullOrWhitespaceId_ReturnsBadRequest(string? id)
    {
        var result = _subject.GetDeviceOptions(id!);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Device id is required.", badRequest.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateDeviceOptions_NullOrWhitespaceId_ReturnsBadRequest(string? id)
    {
        var result = await _subject.UpdateDeviceOptions(id!, new DeviceOptionsDto());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Device id is required.", badRequest.Value);
    }

    [Fact]
    public async Task UpdateDeviceOptions_NullPayload_ReturnsBadRequest()
    {
        var result = await _subject.UpdateDeviceOptions("dev-1", null!);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Device options are required.", badRequest.Value);
    }

    [Fact]
    public async Task DeleteDevice_NullArray_ReturnsBadRequest()
    {
        var result = await _subject.DeleteDevice(null!);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Device ids are required.", badRequest.Value);
    }

    [Fact]
    public async Task DeleteDevice_EmptyArray_ReturnsBadRequest()
    {
        var result = await _subject.DeleteDevice(Array.Empty<string>());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Device ids are required.", badRequest.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DeleteDevice_ContainsNullOrWhitespace_ReturnsBadRequest(string? id)
    {
        var result = await _subject.DeleteDevice(new[] { "dev-valid", id! });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Device id must not be empty.", badRequest.Value);
    }
}
