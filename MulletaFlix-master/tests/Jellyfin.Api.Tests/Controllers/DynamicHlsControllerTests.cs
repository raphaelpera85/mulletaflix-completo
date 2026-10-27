using System;
using MediaBrowser.Controller.Streaming;
using MulletaFlix.Api.Controllers;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers
{
    public class DynamicHlsControllerTests
    {
        [Theory]
        [MemberData(nameof(GetSegmentLengths_Success_TestData))]
        public void GetSegmentLengths_Success(long runtimeTicks, int segmentlength, double[] expected)
        {
            var res = DynamicHlsController.GetSegmentLengthsInternal(runtimeTicks, segmentlength);
            Assert.Equal(expected.Length, res.Length);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], res[i]);
            }
        }

        [Fact]
        public void GetSegmentEndingPositionTicks_UsesTheCurrentRequestValues()
        {
            var first = new StreamingRequestDto { CurrentRuntimeTicks = 10, ActualSegmentLengthTicks = 3 };
            var second = new StreamingRequestDto { CurrentRuntimeTicks = 40, ActualSegmentLengthTicks = 5 };

            Assert.Equal(13, DynamicHlsController.GetSegmentEndingPositionTicks(first));
            Assert.Equal(45, DynamicHlsController.GetSegmentEndingPositionTicks(second));
        }

        [Fact]
        public void SetTranscodeStartTimeTicks_UpdatesTheCachedStateRequest()
        {
            var cachedStateRequest = new StreamingRequestDto { StartTimeTicks = 0 };
            var segmentRequest = new StreamingRequestDto { CurrentRuntimeTicks = 42 };

            DynamicHlsController.SetTranscodeStartTimeTicks(cachedStateRequest, segmentRequest);

            Assert.Equal(42, cachedStateRequest.StartTimeTicks);
        }

        public static TheoryData<long, int, double[]> GetSegmentLengths_Success_TestData()
        {
            var data = new TheoryData<long, int, double[]>();
            data.Add(0, 6, Array.Empty<double>());
            data.Add(
                TimeSpan.FromSeconds(3).Ticks,
                6,
                new double[] { 3 });
            data.Add(
                TimeSpan.FromSeconds(6).Ticks,
                6,
                new double[] { 6 });
            data.Add(
                TimeSpan.FromSeconds(3.3333333).Ticks,
                6,
                new double[] { 3.3333333 });
            data.Add(
                TimeSpan.FromSeconds(9.3333333).Ticks,
                6,
                new double[] { 6, 3.3333333 });

            return data;
        }
    }
}

