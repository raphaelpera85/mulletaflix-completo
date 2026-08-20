using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using BitFaster.Caching;
using Emby.Server.Implementations.Localization;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Model.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Localization
{
    public partial class LocalizationManagerTests
    {
        [Theory]
        [InlineData("Default", "Default")]
        [InlineData("HeaderLiveTV", "Live TV")]
        public void GetLocalizedString_Valid_Success(string key, string expected)
        {
            var localizationManager = Setup(new ServerConfiguration()
            {
                UICulture = "en-US"
            });

            var translated = localizationManager.GetServerLocalizedString(key);
            Assert.NotNull(translated);
            Assert.Equal(expected, translated);
        }

        [Fact]
        public void GetLocalizedString_Invalid_Success()
        {
            var localizationManager = Setup(new ServerConfiguration()
            {
                UICulture = "en-US"
            });

            var key = "SuperInvalidTranslationKeyThatWillNeverBeAdded";

            var translated = localizationManager.GetLocalizedString(key);
            Assert.NotNull(translated);
            Assert.Equal(key, translated);
        }

        [Fact]
        public void GetLocalizedString_WithCulture_ReturnsTranslation()
        {
            var localizationManager = Setup(new ServerConfiguration
            {
                UICulture = "en-US"
            });

            var translated = localizationManager.GetLocalizedString("Artists", "de");
            Assert.Equal("Interpreten", translated);
        }

        [Fact]
        public void GetLocalizedString_WithCulture_FallsBackToEnUs()
        {
            var localizationManager = Setup(new ServerConfiguration
            {
                UICulture = "en-US"
            });

            // A culture with no translation file should fall back to en-US
            var translated = localizationManager.GetLocalizedString("Artists", "zz");
            Assert.Equal("Artists", translated);
        }

        [Fact]
        public void GetLocalizedString_WithBcp47Normalization_ReturnsTranslation()
        {
            var localizationManager = Setup(new ServerConfiguration
            {
                UICulture = "en-US"
            });

            // es-419 is stored as es_419 in MulletaFlix
            var translated = localizationManager.GetLocalizedString("Default", "es-419");
            Assert.NotEqual("Default", translated);
        }

        [Fact]
        public void GetServerLocalizedString_UsesServerCulture()
        {
            var localizationManager = Setup(new ServerConfiguration
            {
                UICulture = "de"
            });

            // Even if CurrentUICulture is fr, GetServerLocalizedString should use the server's "de"
            var previousCulture = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr");
                var translated = localizationManager.GetServerLocalizedString("Artists");
                Assert.Equal("Interpreten", translated);
            }
            finally
            {
                CultureInfo.CurrentUICulture = previousCulture;
            }
        }

        [Fact]
        public void GetLocalizedString_UsesCurrentUICulture()
        {
            var localizationManager = Setup(new ServerConfiguration
            {
                UICulture = "en-US"
            });

            var previousCulture = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de");
                var translated = localizationManager.GetLocalizedString("Artists");
                Assert.Equal("Interpreten", translated);
            }
            finally
            {
                CultureInfo.CurrentUICulture = previousCulture;
            }
        }

        [Fact]
        public void GetSupportedUICultures_IncludesCommonCultures()
        {
            var supported = LocalizationManager.GetSupportedUICultures();
            Assert.Contains(supported, c => c.Name.Equals("de", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(supported, c => c.Name.Equals("en-US", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(supported, c => c.Name.Equals("fr", StringComparison.OrdinalIgnoreCase));
            // Underscore variants get normalized to BCP-47 hyphen form for CultureInfo compatibility.
            Assert.Contains(supported, c => c.Name.Equals("es-419", StringComparison.OrdinalIgnoreCase));
        }


    }
}
