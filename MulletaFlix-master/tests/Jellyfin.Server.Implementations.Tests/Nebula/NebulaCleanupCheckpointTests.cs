using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Jellyfin.Server.Implementations.Nebula;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

/// <summary>
/// Cobre o checkpoint da varredura incremental de limpeza.
/// </summary>
/// <remarks>
/// A limpeza usava `Directory.GetFiles(source, "*.*", AllDirectories)`, que
/// materializa a árvore inteira em memória antes de processar o primeiro arquivo
/// — numa raiz grande isso é um pico de alocação e um atraso longo antes de
/// qualquer progresso visível. Pior: sem checkpoint, um cancelamento (encerrar o
/// servidor) descartava todo o avanço e o ciclo seguinte recomeçava da primeira
/// entrada, de modo que raízes muito grandes podiam nunca chegar ao fim.
/// </remarks>
public class NebulaCleanupCheckpointTests
{
    [Fact]
    public void Checkpoint_StartsEmptyAndResumesFromTheLastProcessedEntry()
    {
        var checkpoint = new NebulaCleanupCheckpoint();

        Assert.Null(checkpoint.GetResumeMarker("N:/Stage"));

        checkpoint.Record("N:/Stage", "N:/Stage/Series/ep10.mkv");

        Assert.Equal("N:/Stage/Series/ep10.mkv", checkpoint.GetResumeMarker("N:/Stage"));
    }

    [Fact]
    public void Checkpoint_IsolatesRootsFromEachOther()
    {
        var checkpoint = new NebulaCleanupCheckpoint();

        checkpoint.Record("E:/Stage", "E:/Stage/a.mkv");
        checkpoint.Record("F:/Stage", "F:/Stage/z.mkv");

        Assert.Equal("E:/Stage/a.mkv", checkpoint.GetResumeMarker("E:/Stage"));
        Assert.Equal("F:/Stage/z.mkv", checkpoint.GetResumeMarker("F:/Stage"));
    }

    [Fact]
    public void Checkpoint_ClearingARootRestartsThatRootOnly()
    {
        var checkpoint = new NebulaCleanupCheckpoint();
        checkpoint.Record("E:/Stage", "E:/Stage/a.mkv");
        checkpoint.Record("F:/Stage", "F:/Stage/z.mkv");

        // Concluir a travessia de uma raiz precisa zerar só aquela raiz, senão o
        // ciclo seguinte pularia arquivos novos criados antes do marcador.
        checkpoint.Complete("E:/Stage");

        Assert.Null(checkpoint.GetResumeMarker("E:/Stage"));
        Assert.Equal("F:/Stage/z.mkv", checkpoint.GetResumeMarker("F:/Stage"));
    }

    [Fact]
    public void EnumerateFrom_SkipsEntriesAlreadyProcessedInThePreviousRun()
    {
        var entries = new[]
        {
            "N:/Stage/a.mkv",
            "N:/Stage/b.mkv",
            "N:/Stage/c.mkv",
            "N:/Stage/d.mkv"
        };

        // Retomada a partir de "b": "a" e "b" já foram tratados.
        var resumed = NebulaCleanupCheckpoint.EnumerateFrom(entries, "N:/Stage/b.mkv").ToArray();

        Assert.Equal(["N:/Stage/c.mkv", "N:/Stage/d.mkv"], resumed);
    }

    [Fact]
    public void EnumerateFrom_WithoutMarkerReturnsEverything()
    {
        var entries = new[] { "a", "b" };

        Assert.Equal(entries, NebulaCleanupCheckpoint.EnumerateFrom(entries, null).ToArray());
        Assert.Equal(entries, NebulaCleanupCheckpoint.EnumerateFrom(entries, string.Empty).ToArray());
    }

    [Fact]
    public void EnumerateFrom_WithAVanishedMarkerProcessesEverythingInstead()
    {
        // O arquivo do checkpoint pode ter sido removido entre ciclos — por esta
        // própria limpeza. Descartar a lista inteira nesse caso faria a varredura
        // parar de progredir; reprocessar é seguro porque a remoção é idempotente.
        var entries = new[] { "a", "b", "c" };

        var resumed = NebulaCleanupCheckpoint.EnumerateFrom(entries, "arquivo-que-nao-existe-mais").ToArray();

        Assert.Equal(entries, resumed);
    }

    [Fact]
    public void EnumerateFrom_IsLazyAndDoesNotMaterializeTheWholeTree()
    {
        // O ponto central do item: a travessia precisa ser incremental. Se a
        // enumeração for materializada, este teste esgota a sequência infinita.
        var enumerated = 0;

        IEnumerable<string> InfiniteTree()
        {
            var index = 0;
            while (true)
            {
                enumerated++;
                yield return $"file-{index++}.mkv";
            }
        }

        var firstThree = NebulaCleanupCheckpoint.EnumerateFrom(InfiniteTree(), null).Take(3).ToArray();

        Assert.Equal(3, firstThree.Length);
        Assert.True(enumerated <= 4, $"A enumeração deve ser lazy; itens consumidos: {enumerated}.");
    }

    [Fact]
    public void Progress_ReportsPositionWithinTheRoot()
    {
        var checkpoint = new NebulaCleanupCheckpoint();

        checkpoint.Record("N:/Stage", "N:/Stage/a.mkv");
        checkpoint.Record("N:/Stage", "N:/Stage/b.mkv");

        // Progresso dentro de raízes grandes era invisível: o item exige
        // reportá-lo, e a contagem por raiz é o dado mínimo para isso.
        Assert.Equal(2, checkpoint.GetProcessedCount("N:/Stage"));
        Assert.Equal(0, checkpoint.GetProcessedCount("E:/Stage"));

        checkpoint.Complete("N:/Stage");
        Assert.Equal(0, checkpoint.GetProcessedCount("N:/Stage"));
    }
}
