using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Nebula;
using MulletaFlix.Server.Implementations.Nebula;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

public sealed class NebulaDownloaderStartupTests
{
    [Fact]
    public void FindPythonPathFromMarkers_PrefersDataRuntimeAndFallsBackToLegacyRuntime()
    {
        var root = Path.Combine(Path.GetTempPath(), "mulletaflix-python-marker-" + Guid.NewGuid().ToString("N"));
        var dataPython = Path.Combine(root, "data-python.exe");
        var legacyPython = Path.Combine(root, "legacy-python.exe");
        var dataMarker = Path.Combine(root, "data.path");
        var legacyMarker = Path.Combine(root, "legacy.path");

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(dataPython, string.Empty);
            File.WriteAllText(legacyPython, string.Empty);
            File.WriteAllText(dataMarker, dataPython);
            File.WriteAllText(legacyMarker, legacyPython);

            Assert.Equal(dataPython, NebulaFtpManager.FindPythonPathFromMarkers([dataMarker, legacyMarker]));

            File.Delete(dataMarker);
            Assert.Equal(legacyPython, NebulaFtpManager.FindPythonPathFromMarkers([dataMarker, legacyMarker]));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    public void CanReuseRunningEnvio_RequiresPromotionWhenFullModeIsRequested(bool currentStreamOnly, bool requestedStreamOnly, bool expected)
    {
        Assert.Equal(expected, NebulaFtpManager.CanReuseRunningEnvio(currentStreamOnly, requestedStreamOnly));
    }

    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(true, true, true, true)]
    [InlineData(true, true, false, false)]
    [InlineData(false, false, false, false)]
    public void ShouldReuseRunningEnvio_UsesLiveRuntimeAndPromotesStreamOnlyWorker(
        bool runtimeAlive,
        bool currentStreamOnly,
        bool requestedStreamOnly,
        bool expected)
    {
        Assert.Equal(expected, NebulaFtpManager.ShouldReuseRunningEnvio(runtimeAlive, currentStreamOnly, requestedStreamOnly));
    }

    [Theory]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    public void ShouldRestartRuntimeForModePromotion_StopsStreamOnlyWorkerBeforeFullMode(
        bool runtimeAlive,
        bool currentStreamOnly,
        bool requestedStreamOnly,
        bool expected)
    {
        Assert.Equal(
            expected,
            NebulaFtpManager.ShouldRestartRuntimeForModePromotion(runtimeAlive, currentStreamOnly, requestedStreamOnly));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void ShouldScheduleMountRetryAfterDownloaderStart_RequiresMappedDriveAndHealthyEnvio(bool useMappedDrive, bool envioRunning, bool expected)
    {
        Assert.Equal(expected, NebulaFtpManager.ShouldScheduleMountRetryAfterDownloaderStart(useMappedDrive, envioRunning));
    }

    [Theory]
    [InlineData("{\"status\":\"ready\",\"engine\":\"python\",\"pid\":42}", 42, true)]
    [InlineData("{\"status\":\"ready\",\"engine\":\"python\",\"pid\":7}", 42, false)]
    [InlineData("{\"status\":\"ready\",\"engine\":\"python\"}", 42, false)]
    [InlineData("not-json", 42, false)]
    public void IsExpectedPythonWorkerHealth_OnlyAcceptsHealthFromOwnedProcess(string payload, int expectedProcessId, bool expected)
    {
        Assert.Equal(expected, NebulaFtpManager.IsExpectedPythonWorkerHealth(payload, expectedProcessId));
    }

    [Fact]
    public void IsPythonDownloaderReadyOutput_RequiresMongoIndexBeforeLibraryDiscovery()
    {
        Assert.False(NebulaFtpManager.IsPythonDownloaderReadyOutput(
            "[STRM-WORKER-READY] Worker iniciado; preparando índice Mongo e fila."));
        Assert.True(NebulaFtpManager.IsPythonDownloaderReadyOutput(
            "[STRM-WORKER-MONGO-READY] Índice Mongo preparado; descobrindo raízes STRM."));
        Assert.True(NebulaFtpManager.IsPythonDownloaderReadyOutput(
            "[STRM-SCAN-STARTED] Varredura iniciada"));
        Assert.True(NebulaFtpManager.IsPythonDownloaderReadyOutput(
            "Escaneando e priorizando arquivos .strm..."));
    }

    [Fact]
    public void IsPythonDownloaderReadyOutput_IgnoresUnrelatedOutput()
    {
        Assert.False(NebulaFtpManager.IsPythonDownloaderReadyOutput("Conectando ao Telegram..."));
    }

    [Fact]
    public void TryApplyPythonDownloaderProgressOutput_ReportsWorkerStartupBeforeMongoPreparation()
    {
        var status = new NebulaDownloadStatusDto();

        var handled = NebulaFtpManager.TryApplyPythonDownloaderProgressOutput(
            "[STRM-WORKER-READY] Worker iniciado; preparando índice Mongo e fila.",
            status);

        Assert.True(handled);
        Assert.Equal("Preparando fila de download", status.Name);
        Assert.Equal("Downloader iniciado; preparando índice Mongo...", status.StageStep);
        Assert.Equal("O processo está ativo; a varredura começa após preparar o índice.", status.DetailText);
    }

    [Fact]
    public void TryApplyPythonDownloaderProgressOutput_ReportsMongoPreparationWithoutDeclaringScanReady()
    {
        var status = new NebulaDownloadStatusDto();

        var handled = NebulaFtpManager.TryApplyPythonDownloaderProgressOutput(
            "[STRM-WORKER-MONGO-READY] Índice Mongo preparado; descobrindo raízes STRM.",
            status);

        Assert.True(handled);
        Assert.Equal("Preparando fila de download", status.Name);
        Assert.Equal("Índice Mongo preparado; descobrindo bibliotecas...", status.StageStep);
        Assert.Contains("O scan ainda não começou", status.DetailText, StringComparison.Ordinal);
    }

    [Fact]
    public void TryApplyPythonDownloaderProgressOutput_DoesNotReportEmptyWholeQueueForAnEmptyTitle()
    {
        var status = new NebulaDownloadStatusDto();
        NebulaFtpManager.TryApplyPythonDownloaderProgressOutput("Indexando título: Alpha", status);

        var handled = NebulaFtpManager.TryApplyPythonDownloaderProgressOutput(
            "Total de arquivos .strm encontrados: 0 no título Alpha",
            status);

        Assert.True(handled);
        Assert.Equal("Preparando fila de download", status.Name);
        Assert.Equal("Título sem mídia elegível; seguindo para o próximo...", status.StageStep);
        Assert.Equal("Alpha não contém arquivos elegíveis.", status.DetailText);
        Assert.Equal(0, status.QueueCount);
    }

    [Fact]
    public void TryApplyPythonDownloaderProgressOutput_ReportsEligibleItemsForCurrentTitle()
    {
        var status = new NebulaDownloadStatusDto();
        NebulaFtpManager.TryApplyPythonDownloaderProgressOutput("Indexando título: Alpha", status);

        var handled = NebulaFtpManager.TryApplyPythonDownloaderProgressOutput(
            "Total de arquivos .strm encontrados: 3 no título Alpha",
            status);

        Assert.True(handled);
        Assert.Equal("Fila de download preparada", status.Name);
        Assert.Equal("Título indexado: Alpha", status.StageStep);
        Assert.Equal("3 arquivo(s) elegível(is); iniciando este título antes do próximo.", status.DetailText);
        Assert.Equal(3, status.QueueCount);
    }

    [Fact]
    public async Task WaitForWorkerReadinessAsync_CompletesWhenScanStarts()
    {
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var wait = NebulaFtpManager.WaitForWorkerReadinessAsync(
            ready.Task,
            exited.Task,
            () => 0,
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        ready.SetResult();

        await wait;
    }

    [Fact]
    public async Task WaitForWorkerReadinessAsync_ReportsEarlyWorkerExit()
    {
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        exited.SetResult();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NebulaFtpManager.WaitForWorkerReadinessAsync(
                ready.Task,
                exited.Task,
                () => 17,
                TimeSpan.FromSeconds(1),
                CancellationToken.None));

        Assert.Contains("código 17", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WaitForWorkerReadinessAsync_TimesOutBeforeMongoIndexIsReady()
    {
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await Assert.ThrowsAsync<TimeoutException>(() =>
            NebulaFtpManager.WaitForWorkerReadinessAsync(
                ready.Task,
                exited.Task,
                () => 0,
                TimeSpan.FromMilliseconds(20),
                CancellationToken.None));
    }

    [Fact]
    public async Task WaitForWorkerReadinessAsync_PropagatesCancellation()
    {
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        var wait = NebulaFtpManager.WaitForWorkerReadinessAsync(
            ready.Task,
            exited.Task,
            () => 0,
            TimeSpan.FromSeconds(1),
            cancellation.Token);

        cancellation.CancelAfter(TimeSpan.FromMilliseconds(20));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
    }
}
