using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using MulletaFlix.Data.Enums;
using MulletaFlix.Extensions.Json;
using MulletaFlix.Extensions.Json.Converters;
using MediaBrowser.MediaEncoding.Probing;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.MediaInfo;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MulletaFlix.MediaEncoding.Tests.Probing
{
    public partial class ProbeResultNormalizerTests
    {
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly ProbeResultNormalizer _probeResultNormalizer = new ProbeResultNormalizer(new NullLogger<EncoderValidatorTests>(), new Mock<ILocalizationManager>().Object);

        public ProbeResultNormalizerTests()
        {
            _jsonOptions = new JsonSerializerOptions(JsonDefaults.Options);
            _jsonOptions.Converters.Add(new JsonBoolStringConverter());
        }

        [Theory]
        [InlineData("2997/125", 23.976f)]
        [InlineData("1/50", 0.02f)]
        [InlineData("25/1", 25f)]
        [InlineData("120/1", 120f)]
        [InlineData("1704753000/71073479", 23.98578237601117f)]
        [InlineData("0/0", null)]
        [InlineData("1/1000", 0.001f)]
        [InlineData("1/90000", 1.1111111E-05f)]
        [InlineData("1/48000", 2.0833333E-05f)]
        public void GetFrameRate_Success(string value, float? expected)
            => Assert.Equal(expected, ProbeResultNormalizer.GetFrameRate(value));

        [Theory]
        [InlineData("1:1", true)]
        [InlineData("3201:3200", true)]
        [InlineData("1215:1216", true)]
        [InlineData("1001:1000", true)]
        [InlineData("16:15", false)]
        [InlineData("8:9", false)]
        [InlineData("32:27", false)]
        [InlineData("10:11", false)]
        [InlineData("64:45", false)]
        [InlineData("4:3", false)]
        [InlineData("0:1", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsNearSquarePixelSar_DetectsCorrectly(string? sar, bool expected)
            => Assert.Equal(expected, ProbeResultNormalizer.IsNearSquarePixelSar(sar));


    }
}
