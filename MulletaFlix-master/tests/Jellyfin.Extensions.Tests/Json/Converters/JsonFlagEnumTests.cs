using System.Text.Json;
using MulletaFlix.Extensions.Json.Converters;
using MediaBrowser.Model.Session;
using Xunit;

namespace MulletaFlix.Extensions.Tests.Json.Converters;

public class JsonFlagEnumTests
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters =
        {
            new JsonFlagEnumConverter<TranscodeReason>()
        }
    };

    [Theory]
    [InlineData(TranscodeReason.AudioIsExternal | TranscodeReason.ContainerNotSupported, "[\"ContainerNotSupported\",\"AudioIsExternal\"]")]
    [InlineData(TranscodeReason.AudioIsExternal | TranscodeReason.ContainerNotSupported | TranscodeReason.VideoBitDepthNotSupported, "[\"ContainerNotSupported\",\"AudioIsExternal\",\"VideoBitDepthNotSupported\"]")]
    [InlineData((TranscodeReason)0, "[]")]
    public void Serialize_Transcode_Reason(TranscodeReason transcodeReason, string output)
    {
        var result = JsonSerializer.Serialize(transcodeReason, _jsonOptions);

        Assert.Equal(output, result);
    }

    [Fact]
    public void Deserialize_Transcode_Reason()
    {
        var result = JsonSerializer.Deserialize<TranscodeReason>(
            "[\"AudioIsExternal\",\"ContainerNotSupported\"]",
            _jsonOptions);

        Assert.Equal(TranscodeReason.AudioIsExternal | TranscodeReason.ContainerNotSupported, result);
    }

    [Fact]
    public void Deserialize_Unknown_Flag_Throws()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<TranscodeReason>(
            "[\"UnknownReason\"]",
            _jsonOptions));
    }
}
