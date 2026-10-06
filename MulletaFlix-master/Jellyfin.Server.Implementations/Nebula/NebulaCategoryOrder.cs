using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>
/// Fonte única da ordem de categorias usada pelo Nebula.
/// </summary>
/// <remarks>
/// A sequência estava duplicada: um <c>switch</c> com o rank numérico no
/// downloader, outro com o nome exibido e uma terceira tabela de nomes de pasta
/// no gerador de STRM. Tabelas paralelas mantidas à mão divergem — alterar a
/// ordem em um lugar e esquecer o outro faz a interface anunciar uma sequência
/// diferente da que o downloader executa. Aqui o rank é derivado da posição na
/// lista, então acrescentar, remover ou reordenar uma categoria muda ordem e
/// rótulo ao mesmo tempo.
///
/// A ordem ativa é opcionalmente substituída em runtime via
/// <see cref="ApplyCustomOrder"/>, alimentada pela preferência persistida do
/// operador (arrastar-e-soltar na UI). A troca é uma escrita volátil de um
/// array imutável: leituras concorrentes nunca veem um estado parcialmente
/// atualizado, então o downloader pode reler a ordem a cada ciclo sem lock.
/// </remarks>
public static class NebulaCategoryOrder
{
    /// <summary>Rótulo usado para categorias fora da ordem conhecida.</summary>
    public const string UnknownDisplayName = "OUTROS";

    // A ordem desta lista É a ordem de download padrão. O rank é o índice + 1.
    private static readonly (string MediaType, string DisplayName)[] DefaultOrder =
    [
        ("ANIMACAO", "ANIMAÇÕES"),
        ("FILME", "FILMES"),
        ("SERIE", "SERIES"),
        ("DORAMA", "DORAMAS"),
        ("NOVELA", "NOVELAS"),
        ("PORNO", "PORNO")
    ];

    private static (string MediaType, string DisplayName)[] _activeOrder = DefaultOrder;

    /// <summary>
    /// Gets os tipos de mídia na ordem padrão de fábrica (sem personalização do operador).
    /// </summary>
    public static IReadOnlyList<string> DefaultMediaTypeOrder { get; } =
        DefaultOrder.Select(entry => entry.MediaType).ToArray();

    /// <summary>
    /// Gets os tipos de mídia na ordem de processamento ativa (padrão ou personalizada).
    /// </summary>
    public static IReadOnlyList<string> OrderedMediaTypes =>
        Volatile.Read(ref _activeOrder).Select(entry => entry.MediaType).ToArray();

    /// <summary>
    /// Gets os nomes exibidos na ordem de processamento ativa (padrão ou personalizada).
    /// </summary>
    public static IReadOnlyList<string> OrderedDisplayNames =>
        Volatile.Read(ref _activeOrder).Select(entry => entry.DisplayName).ToArray();

    /// <summary>
    /// Resolve o rank de uma categoria na ordem ativa. Menor vem primeiro.
    /// </summary>
    /// <param name="mediaType">Tipo classificado da mídia.</param>
    /// <returns>Rank a partir de 1; categorias desconhecidas ficam por último.</returns>
    public static int GetRank(string? mediaType)
    {
        var order = Volatile.Read(ref _activeOrder);

        if (string.IsNullOrWhiteSpace(mediaType))
        {
            return order.Length + 1;
        }

        for (var index = 0; index < order.Length; index++)
        {
            if (string.Equals(order[index].MediaType, mediaType, StringComparison.OrdinalIgnoreCase))
            {
                return index + 1;
            }
        }

        return order.Length + 1;
    }

    /// <summary>
    /// Resolve o nome exibido de um rank na ordem ativa.
    /// </summary>
    /// <param name="rank">Rank devolvido por <see cref="GetRank"/>.</param>
    /// <returns>O nome exibido, ou <see cref="UnknownDisplayName"/>.</returns>
    public static string GetDisplayName(int rank)
    {
        var order = Volatile.Read(ref _activeOrder);

        if (rank < 1 || rank > order.Length)
        {
            return UnknownDisplayName;
        }

        return order[rank - 1].DisplayName;
    }

    /// <summary>
    /// Substitui a ordem ativa de categorias por uma preferência do operador.
    /// </summary>
    /// <param name="mediaTypeOrder">
    /// Sequência desejada de tipos de mídia conhecidos. Entradas desconhecidas ou
    /// duplicadas são ignoradas; categorias conhecidas ausentes da lista são
    /// acrescentadas ao final na ordem padrão, garantindo que o resultado seja
    /// sempre uma permutação completa das categorias conhecidas. Uma lista nula
    /// ou vazia restaura a ordem padrão de fábrica.
    /// </param>
    public static void ApplyCustomOrder(IReadOnlyList<string>? mediaTypeOrder)
    {
        if (mediaTypeOrder is null || mediaTypeOrder.Count == 0)
        {
            Volatile.Write(ref _activeOrder, DefaultOrder);
            return;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resolved = new List<(string MediaType, string DisplayName)>(DefaultOrder.Length);

        foreach (var mediaType in mediaTypeOrder)
        {
            if (string.IsNullOrWhiteSpace(mediaType))
            {
                continue;
            }

            var match = Array.Find(DefaultOrder, entry => string.Equals(entry.MediaType, mediaType, StringComparison.OrdinalIgnoreCase));
            if (match.MediaType is null || !seen.Add(match.MediaType))
            {
                continue;
            }

            resolved.Add(match);
        }

        // Categorias conhecidas que o operador não mencionou mantêm sua posição
        // relativa de fábrica no final, para nunca deixar de baixar silenciosamente.
        foreach (var entry in DefaultOrder)
        {
            if (seen.Add(entry.MediaType))
            {
                resolved.Add(entry);
            }
        }

        Volatile.Write(ref _activeOrder, resolved.ToArray());
    }

    /// <summary>
    /// Verifica se a ordem fornecida é uma personalização válida (contém apenas
    /// tipos de mídia conhecidos, sem duplicatas).
    /// </summary>
    /// <param name="mediaTypeOrder">Sequência proposta pelo operador.</param>
    /// <returns>Verdadeiro se todas as entradas forem tipos conhecidos e únicos.</returns>
    public static bool IsValidCustomOrder(IReadOnlyList<string>? mediaTypeOrder)
    {
        if (mediaTypeOrder is null || mediaTypeOrder.Count == 0)
        {
            return true;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mediaType in mediaTypeOrder)
        {
            if (string.IsNullOrWhiteSpace(mediaType))
            {
                return false;
            }

            var isKnown = DefaultOrder.Any(entry => string.Equals(entry.MediaType, mediaType, StringComparison.OrdinalIgnoreCase));
            if (!isKnown || !seen.Add(mediaType.Trim()))
            {
                return false;
            }
        }

        return true;
    }
}
