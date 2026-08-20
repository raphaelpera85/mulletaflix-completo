using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using MulletaFlix.Networking.Manager;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using IConfigurationManager = MediaBrowser.Common.Configuration.IConfigurationManager;

namespace MulletaFlix.Networking.Tests
{
    public partial class NetworkParseTests
    {
        internal static IConfigurationManager GetMockConfig(NetworkConfiguration conf)
        {
            var configManager = new Mock<IConfigurationManager>
            {
                CallBase = true
            };
            configManager.Setup(x => x.GetConfiguration(It.IsAny<string>())).Returns(conf);
            return configManager.Object;
        }

        /// <summary>
        /// Checks the ability to ignore virtual interfaces.
        /// </summary>
        /// <param name="interfaces">Mock network setup, in the format (IP address, interface index, interface name) | .... </param>
        /// <param name="lan">LAN addresses.</param>
        /// <param name="value">Bind addresses that are excluded.</param>
        [Theory]
        // All valid
        [InlineData("192.168.1.208/24,-16,eth16|200.200.200.200/24,11,eth11", "192.168.1.0/24;200.200.200.0/24", "[192.168.1.208/24,200.200.200.200/24]")]
        // eth16 only
        [InlineData("192.168.1.208/24,-16,eth16|200.200.200.200/24,11,eth11", "192.168.1.0/24", "[192.168.1.208/24]")]
        // eth16 only without mask
        [InlineData("192.168.1.208,-16,eth16|200.200.200.200,11,eth11", "192.168.1.0/24", "[192.168.1.208/32]")]
        // All interfaces excluded. (including loopbacks)
        [InlineData("192.168.1.208/24,-16,vEthernet1|192.168.2.208/24,-16,vEthernet212|200.200.200.200/24,11,eth11", "192.168.1.0/24", "[]")]
        // vEthernet1 and vEthernet212 should be excluded.
        [InlineData("192.168.1.200/24,-20,vEthernet1|192.168.2.208/24,-16,vEthernet212|200.200.200.200/24,11,eth11", "192.168.1.0/24;200.200.200.200/24", "[200.200.200.200/24]")]
        // Overlapping interface,
        [InlineData("192.168.1.110/24,-20,br0|192.168.1.10/24,-16,br0|200.200.200.200/24,11,eth11", "192.168.1.0/24", "[192.168.1.110/24,192.168.1.10/24]")]
        public void IgnoreVirtualInterfaces(string interfaces, string lan, string value)
        {
            var conf = new NetworkConfiguration()
            {
                EnableIPv6 = true,
                EnableIPv4 = true,
                LocalNetworkSubnets = lan?.Split(';') ?? throw new ArgumentNullException(nameof(lan))
            };

            NetworkManager.MockNetworkSettings = interfaces;
            var startupConf = new Mock<IConfiguration>();
            using var nm = new NetworkManager(NetworkParseTests.GetMockConfig(conf), startupConf.Object, new NullLogger<NetworkManager>());
            NetworkManager.MockNetworkSettings = string.Empty;

            Assert.Equal(value, "[" + string.Join(",", nm.GetInternalBindAddresses().Select(x => x.Address + "/" + x.Subnet.PrefixLength)) + "]");
        }

        /// <summary>
        /// Checks valid IP address formats.
        /// </summary>
        /// <param name="address">IP Address.</param>
        [Theory]
        [InlineData("127.0.0.1")]
        [InlineData("127.0.0.1/8")]
        [InlineData("192.168.1.2")]
        [InlineData("192.168.1.2/24")]
        [InlineData("fd23:184f:2029:0:3139:7386:67d7:d517")]
        [InlineData("[fd23:184f:2029:0:3139:7386:67d7:d517]")]
        [InlineData("fe80::7add:12ff:febb:c67b%16")]
        [InlineData("[fe80::7add:12ff:febb:c67b%16]:123")]
        [InlineData("fe80::7add:12ff:febb:c67b%16:123")]
        [InlineData("[fe80::7add:12ff:febb:c67b%16]")]
        [InlineData("fd23:184f:2029:0:3139:7386:67d7:d517/56")]
        public static void TryParseValidIPStringsTrue(string address)
        {
            Assert.True(NetworkUtils.TryParseToSubnet(address, out _));
            Assert.True(NetworkUtils.TryParseToSubnet('!' + address, out _, true));
        }

        /// <summary>
        /// Checks invalid IP address formats.
        /// </summary>
        /// <param name="address">IP Address.</param>
        [Theory]
        [InlineData("127.0.0.1#")]
        [InlineData("localhost!")]
        [InlineData("256.128.0.0.0.1")]
        [InlineData("fd23:184f:2029:0:3139:7386:67d7:d517:1231")]
        [InlineData("[fd23:184f:2029:0:3139:7386:67d7:d517:1231]")]
        [InlineData("fd23:184f:2029:0100/56")]
        public static void TryParseInvalidIPStringsFalse(string address)
            => Assert.False(NetworkUtils.TryParseToSubnet(address, out _));

        /// <summary>
        /// Verifies that <see cref="NetworkUtils.TryParseToSubnets"/> emits a targeted warning
        /// for IPv6 prefix-only notation and a generic warning for other malformed entries.
        /// </summary>
        [Fact]
        public static void TryParseToSubnets_InvalidEntries_LogsWarnings()
        {
            var logger = new Mock<ILogger>();

            var values = new[] { "10.0.0.0/8", "fd23:184f:2029:0100/56", "not-an-address" };
            Assert.True(NetworkUtils.TryParseToSubnets(values, out var result, false, logger.Object));
            Assert.NotNull(result);
            Assert.Single(result);

            // IPv6 prefix-only notation should produce a specific, actionable warning.
            logger.Verify(
                l => l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("IPv6 prefix-only", StringComparison.Ordinal)
                        && state.ToString()!.Contains("fd23:184f:2029:0100/56", StringComparison.Ordinal)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            // Other malformed entries should still produce a generic warning.
            logger.Verify(
                l => l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("not-an-address", StringComparison.Ordinal)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        /// <summary>
        /// Verifies that IPv4 entries whose '!' polarity doesn't match the requested pass are skipped silently,
        /// not logged as invalid. Callers parse the same list twice (LAN and excluded) so the off-polarity
        /// entries are expected, not erroneous.
        /// </summary>
        [Fact]
        public static void TryParseToSubnets_PolarityMismatchIPv4_DoesNotWarn()
        {
            var logger = new Mock<ILogger>();
            var values = new[] { "127.0.0.0/8", "192.168.178.0/24", "!10.0.0.0/8" };

            // Non-negated pass picks up the two non-'!' entries and ignores '!10.0.0.0/8' silently.
            Assert.True(NetworkUtils.TryParseToSubnets(values, out var lanResult, false, logger.Object));
            Assert.NotNull(lanResult);
            Assert.Equal(2, lanResult.Count);

            // Negated pass picks up the single '!' entry and ignores the others silently.
            Assert.True(NetworkUtils.TryParseToSubnets(values, out var excludedResult, true, logger.Object));
            Assert.NotNull(excludedResult);
            Assert.Single(excludedResult);

            logger.Verify(
                l => l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }

        /// <summary>
        /// Same as the IPv4 case but for IPv6 entries â€” makes sure the polarity pre-check works
        /// for IPv6 CIDR notation (with '::') as well.
        /// </summary>
        [Fact]
        public static void TryParseToSubnets_PolarityMismatchIPv6_DoesNotWarn()
        {
            var logger = new Mock<ILogger>();
            var values = new[] { "fd00::/8", "fe80::/10", "!fd12:3456:789a::/48" };

            Assert.True(NetworkUtils.TryParseToSubnets(values, out var lanResult, false, logger.Object));
            Assert.NotNull(lanResult);
            Assert.Equal(2, lanResult.Count);

            Assert.True(NetworkUtils.TryParseToSubnets(values, out var excludedResult, true, logger.Object));
            Assert.NotNull(excludedResult);
            Assert.Single(excludedResult);

            logger.Verify(
                l => l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }

        /// <summary>
        /// Checks if IPv4 address is within a defined subnet.
        /// </summary>
        /// <param name="netMask">Network mask.</param>
        /// <param name="ipAddress">IP Address.</param>
        [Theory]
        [InlineData("192.168.5.85/24", "192.168.5.1")]
        [InlineData("192.168.5.85/24", "192.168.5.254")]
        [InlineData("10.128.240.50/30", "10.128.240.48")]
        [InlineData("10.128.240.50/30", "10.128.240.49")]
        [InlineData("10.128.240.50/30", "10.128.240.50")]
        [InlineData("10.128.240.50/30", "10.128.240.51")]
        [InlineData("127.0.0.1/8", "127.0.0.1")]
        public void IPv4SubnetMaskMatchesValidIPAddress(string netMask, string ipAddress)
        {
            var ipa = IPAddress.Parse(ipAddress);
            Assert.True(NetworkUtils.TryParseToSubnet(netMask, out var subnet) && subnet.Subnet.Contains(IPAddress.Parse(ipAddress)));
        }

        /// <summary>
        /// Checks if IPv4 address is not within a defined subnet.
        /// </summary>
        /// <param name="netMask">Network mask.</param>
        /// <param name="ipAddress">IP Address.</param>
        [Theory]
        [InlineData("192.168.5.85/24", "192.168.4.254")]
        [InlineData("192.168.5.85/24", "191.168.5.254")]
        [InlineData("10.128.240.50/30", "10.128.240.47")]
        [InlineData("10.128.240.50/30", "10.128.240.52")]
        [InlineData("10.128.240.50/30", "10.128.239.50")]
        [InlineData("10.128.240.50/30", "10.127.240.51")]
        public void IPv4SubnetMaskDoesNotMatchInvalidIPAddress(string netMask, string ipAddress)
        {
            var ipa = IPAddress.Parse(ipAddress);
            Assert.False(NetworkUtils.TryParseToSubnet(netMask, out var subnet) && subnet.Subnet.Contains(IPAddress.Parse(ipAddress)));
        }

        /// <summary>
        /// Checks if IPv6 address is within a defined subnet.
        /// </summary>
        /// <param name="netMask">Network mask.</param>
        /// <param name="ipAddress">IP Address.</param>
        [Theory]
        [InlineData("2001:db8:abcd:0012::0/64", "2001:0DB8:ABCD:0012:0000:0000:0000:0000")]
        [InlineData("2001:db8:abcd:0012::0/64", "2001:0DB8:ABCD:0012:FFFF:FFFF:FFFF:FFFF")]
        [InlineData("2001:db8:abcd:0012::0/64", "2001:0DB8:ABCD:0012:0001:0000:0000:0000")]
        [InlineData("2001:db8:abcd:0012::0/64", "2001:0DB8:ABCD:0012:FFFF:FFFF:FFFF:FFF0")]
        [InlineData("2001:db8:abcd:0012::0/128", "2001:0DB8:ABCD:0012:0000:0000:0000:0000")]
        public void IPv6SubnetMaskMatchesValidIPAddress(string netMask, string ipAddress)
        {
            Assert.True(NetworkUtils.TryParseToSubnet(netMask, out var subnet) && subnet.Subnet.Contains(IPAddress.Parse(ipAddress)));
        }

        [Theory]
        [InlineData("2001:db8:abcd:0012::0/64", "2001:0DB8:ABCD:0011:FFFF:FFFF:FFFF:FFFF")]
        [InlineData("2001:db8:abcd:0012::0/64", "2001:0DB8:ABCD:0013:0000:0000:0000:0000")]
        [InlineData("2001:db8:abcd:0012::0/64", "2001:0DB8:ABCD:0013:0001:0000:0000:0000")]
        [InlineData("2001:db8:abcd:0012::0/64", "2001:0DB8:ABCD:0011:FFFF:FFFF:FFFF:FFF0")]
        [InlineData("2001:db8:abcd:0012::0/128", "2001:0DB8:ABCD:0012:0000:0000:0000:0001")]
        public void IPv6SubnetMaskDoesNotMatchInvalidIPAddress(string netMask, string ipAddress)
        {
            Assert.False(NetworkUtils.TryParseToSubnet(netMask, out var subnet) && subnet.Subnet.Contains(IPAddress.Parse(ipAddress)));
        }

    }
}
