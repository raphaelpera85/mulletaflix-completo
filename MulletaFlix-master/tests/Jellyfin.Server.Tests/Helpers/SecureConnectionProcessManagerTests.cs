using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using MulletaFlix.Server.Helpers;
using Xunit;

namespace Jellyfin.Server.Tests.Helpers
{
    public class SecureConnectionProcessManagerTests
    {
        [Fact]
        public void FindNginxExecutable_WithEnvironmentVariable_ReturnsConfiguredPath()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                Environment.SetEnvironmentVariable("MULLETAFLIX_NGINX_PATH", tempFile);
                var resolved = SecureConnectionProcessManager.FindNginxExecutable();
                Assert.Equal(tempFile, resolved);
            }
            finally
            {
                Environment.SetEnvironmentVariable("MULLETAFLIX_NGINX_PATH", null);
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact]
        public void FindNginxExecutable_WithAppDir_ResolvesNginxSubdirectory()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "mulleta_test_" + Guid.NewGuid().ToString("N"));
            var nginxDir = Path.Combine(tempDir, "nginx");
            Directory.CreateDirectory(nginxDir);
            var fakeNginxExe = Path.Combine(nginxDir, "nginx.exe");
            File.WriteAllText(fakeNginxExe, "dummy");

            try
            {
                Environment.SetEnvironmentVariable("MULLETAFLIX_NGINX_PATH", null);
                var resolved = SecureConnectionProcessManager.FindNginxExecutable(tempDir);
                Assert.Equal(fakeNginxExe, resolved);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact]
        public async Task IsPortOpenAsync_UnusedPort_ReturnsFalse()
        {
            // Porta 59991 muito provavelmente fechada
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var isOpen = await SecureConnectionProcessManager.IsPortOpenAsync(59991, cts.Token);
            Assert.False(isOpen);
        }

        [Fact]
        public async Task EnsureSecureConnectionStartedAsync_NullAppPaths_DoesNotThrow()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var exception = await Record.ExceptionAsync(() =>
                SecureConnectionProcessManager.EnsureSecureConnectionStartedAsync(null, NullLogger.Instance, cts.Token));

            Assert.Null(exception);
        }

        [Fact]
        public async Task StopSecureConnectionAsync_NoRunningProcess_CompletesSuccessfully()
        {
            var exception = await Record.ExceptionAsync(() =>
                SecureConnectionProcessManager.StopSecureConnectionAsync(NullLogger.Instance));

            Assert.Null(exception);
        }
    }
}
