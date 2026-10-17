using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Nebula;
using MulletaFlix.Server.Implementations.Nebula;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

public sealed class NebulaDownloaderStartupTests
{
    [Theory]
    [InlineData("[STRM-SCAN-STARTED] Varredura iniciada")]
    public void IsPythonDownloaderReadyOutput_AcceptsOnlyActualScanStart(string line)
    {
        Assert.True(NebulaFtpManager.IsPythonDownloaderReadyOutput(line));
    }

    [Fact]
    public void IsPythonDownloaderReadyOutput_IgnoresUnrelatedOutput()
    {
        Assert.False(NebulaFtpManager.IsPythonDownloaderReadyOutput("Escaneando e priorizando arquivos .strm..."));
        Assert.False(NebulaFtpManager.IsPythonDownloaderReadyOutput("[STRM-WORKER-MONGO-READY] Índice Mongo preparado."));
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
    public async Task WaitForWorkerReadinessAsync_FailsWhenWorkerNeverStartsScan()
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
