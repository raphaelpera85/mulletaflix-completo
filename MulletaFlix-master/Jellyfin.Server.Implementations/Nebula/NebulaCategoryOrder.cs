using System;
using System.Collections.Generic;

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
/// </remarks>
public static class NebulaCategoryOrder
{
    /// <summary>Rótulo usado para categorias fora da ordem conhecida.</summary>
    public const string UnknownDisplayName = "OUTROS";

    // A ordem desta lista É a ordem de download. O rank é o índice + 1.
    private static readonly (string MediaType, string DisplayName)[] _order =
    [
        ("ANIMACAO", "ANIMAÇÕES"),
        ("FILME", "FILMES"),
        ("SERIE", "SERIES"),
        ("DORAMA", "DORAMAS"),
        ("NOVELA", "NOVELAS"),
        ("PORNO", "PORNO")
    ];

    /// <summary>
    /// Gets os tipos de mídia na ordem de processamento.
    /// </summary>
    public static IReadOnlyList<string> OrderedMediaTypes { get; } = BuildOrderedMediaTypes();

    /// <summary>
    /// Gets os nomes exibidos na ordem de processamento.
    /// </summary>
    public static IReadOnlyList<string> OrderedDisplayNames { get; } = BuildOrderedDisplayNames();

    /// <summary>
    /// Resolve o rank de uma categoria. Menor vem primeiro.
    /// </summary>
    /// <param name="mediaType">Tipo classificado da mídia.</param>
    /// <returns>Rank a partir de 1; categorias desconhecidas ficam por último.</returns>
    public static int GetRank(string? mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType))
        {
            return _order.Length + 1;
        }

        for (var index = 0; index < _order.Length; index++)
        {
            if (string.Equals(_order[index].MediaType, mediaType, StringComparison.OrdinalIgnoreCase))
            {
                return index + 1;
            }
        }

        return _order.Length + 1;
    }

    /// <summary>
    /// Resolve o nome exibido de um rank.
    /// </summary>
    /// <param name="rank">Rank devolvido por <see cref="GetRank"/>.</param>
    /// <returns>O nome exibido, ou <see cref="UnknownDisplayName"/>.</returns>
    public static string GetDisplayName(int rank)
    {
        if (rank < 1 || rank > _order.Length)
        {
            return UnknownDisplayName;
        }

        return _order[rank - 1].DisplayName;
    }

    private static IReadOnlyList<string> BuildOrderedMediaTypes()
    {
        var result = new string[_order.Length];
        for (var index = 0; index < _order.Length; index++)
        {
            result[index] = _order[index].MediaType;
        }

        return result;
    }

    private static IReadOnlyList<string> BuildOrderedDisplayNames()
    {
        var result = new string[_order.Length];
        for (var index = 0; index < _order.Length; index++)
        {
            result[index] = _order[index].DisplayName;
        }

        return result;
    }
}
