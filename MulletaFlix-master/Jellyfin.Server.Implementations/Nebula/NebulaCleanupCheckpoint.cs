using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>
/// Checkpoint reiniciável da varredura de limpeza de staging.
/// </summary>
/// <remarks>
/// A limpeza materializava a árvore inteira com
/// <c>Directory.GetFiles(source, "*.*", AllDirectories)</c> antes de processar o
/// primeiro arquivo: numa raiz grande isso é um pico de alocação e um atraso
/// longo antes de qualquer progresso visível. Sem checkpoint, um cancelamento
/// (encerrar o servidor) descartava todo o avanço e o ciclo seguinte recomeçava
/// da primeira entrada — raízes muito grandes podiam nunca chegar ao fim.
/// Este tipo guarda, por raiz, a última entrada tratada e a contagem de
/// progresso, permitindo retomar de onde parou.
/// </remarks>
public sealed class NebulaCleanupCheckpoint
{
    private readonly ConcurrentDictionary<string, RootProgress> _progress = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registra que uma entrada foi tratada numa raiz.
    /// </summary>
    /// <param name="root">Raiz em varredura.</param>
    /// <param name="entry">Entrada processada.</param>
    public void Record(string root, string entry)
    {
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(entry))
        {
            return;
        }

        _progress.AddOrUpdate(
            root,
            _ => new RootProgress(entry, 1),
            (_, existing) => new RootProgress(entry, existing.ProcessedCount + 1));
    }

    /// <summary>
    /// Obtém a entrada a partir da qual a varredura deve retomar.
    /// </summary>
    /// <param name="root">Raiz em varredura.</param>
    /// <returns>A última entrada tratada, ou <see langword="null"/> para começar do início.</returns>
    public string? GetResumeMarker(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return null;
        }

        return _progress.TryGetValue(root, out var progress) ? progress.LastEntry : null;
    }

    /// <summary>
    /// Obtém quantas entradas já foram tratadas na raiz no ciclo atual.
    /// </summary>
    /// <param name="root">Raiz em varredura.</param>
    /// <returns>A contagem de progresso.</returns>
    public int GetProcessedCount(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return 0;
        }

        return _progress.TryGetValue(root, out var progress) ? progress.ProcessedCount : 0;
    }

    /// <summary>
    /// Marca a raiz como totalmente percorrida.
    /// </summary>
    /// <remarks>
    /// Zera apenas esta raiz: manter o marcador após concluir faria o ciclo
    /// seguinte pular arquivos novos criados antes dele.
    /// </remarks>
    /// <param name="root">Raiz concluída.</param>
    public void Complete(string root)
    {
        if (!string.IsNullOrWhiteSpace(root))
        {
            _progress.TryRemove(root, out _);
        }
    }

    /// <summary>
    /// Retoma uma enumeração preguiçosa a partir da entrada indicada.
    /// </summary>
    /// <remarks>
    /// A enumeração permanece preguiçosa de ponta a ponta: materializar a
    /// sequência anularia o ganho da travessia incremental. Se o marcador não
    /// for encontrado — o arquivo pode ter sido removido por esta própria
    /// limpeza —, tudo é processado, porque parar de progredir seria pior e a
    /// remoção é idempotente.
    /// </remarks>
    /// <param name="entries">Entradas da raiz, preferencialmente preguiçosas.</param>
    /// <param name="resumeAfter">Entrada já tratada no ciclo anterior.</param>
    /// <returns>As entradas ainda não tratadas.</returns>
    public static IEnumerable<string> EnumerateFrom(IEnumerable<string> entries, string? resumeAfter)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (string.IsNullOrWhiteSpace(resumeAfter))
        {
            return entries;
        }

        return SkipUntilAfter(entries, resumeAfter);
    }

    private static IEnumerable<string> SkipUntilAfter(IEnumerable<string> entries, string resumeAfter)
    {
        var buffered = new List<string>();
        var found = false;

        foreach (var entry in entries)
        {
            if (found)
            {
                yield return entry;
                continue;
            }

            if (string.Equals(entry, resumeAfter, StringComparison.OrdinalIgnoreCase))
            {
                found = true;
                buffered.Clear();
                continue;
            }

            // Guarda as entradas anteriores ao marcador apenas até saber se ele
            // ainda existe. Se não existir, elas precisam ser devolvidas.
            buffered.Add(entry);
        }

        if (!found)
        {
            foreach (var entry in buffered)
            {
                yield return entry;
            }
        }
    }

    private readonly record struct RootProgress(string LastEntry, int ProcessedCount);
}
