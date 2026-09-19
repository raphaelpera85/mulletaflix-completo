#pragma warning disable CA1307 // StringComparison
#pragma warning disable CA1308 // ToLowerInvariant

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>
/// Regra única do Nebula para o conteúdo que forma o cache local de exibição:
/// capas, imagens (poster/fanart/logo/thumb), NFO/XML de metadados e legendas.
/// Esse conteúdo nunca é excluído do disco nem do catálogo, porque é ele que
/// mantém a biblioteca instantânea no web e no aplicativo; o envio ao Telegram
/// é apenas a cópia de segurança.
/// </summary>
internal static class NebulaProtectedContent
{
    private static readonly BsonRegularExpression ProtectedNameExpression = new(BuildNamePattern(), "i");

    /// <summary>
    /// Obtém a expressão regular que casa nomes de arquivo de conteúdo protegido.
    /// </summary>
    internal static BsonRegularExpression NameExpression => ProtectedNameExpression;

    /// <summary>
    /// Indica se o caminho (ou nome de arquivo) pertence ao conteúdo protegido.
    /// </summary>
    /// <param name="path">Caminho ou nome de arquivo.</param>
    /// <returns><see langword="true"/> quando o conteúdo nunca pode ser excluído.</returns>
    internal static bool IsProtectedPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var extension = Path.GetExtension(path);
        return !string.IsNullOrEmpty(extension) && NebulaMetadataExportService.MetadataSidecarExtensions.Contains(extension);
    }

    /// <summary>
    /// Indica se o documento do catálogo representa conteúdo protegido.
    /// </summary>
    /// <param name="doc">Documento da coleção <c>files</c>.</param>
    /// <returns><see langword="true"/> quando o documento nunca pode ser excluído.</returns>
    internal static bool IsProtectedDoc(BsonDocument? doc)
    {
        if (doc == null)
        {
            return false;
        }

        if (doc.TryGetValue("name", out var name) && name.IsString && IsProtectedPath(name.AsString))
        {
            return true;
        }

        return doc.TryGetValue("local_path", out var localPath) && localPath.IsString && IsProtectedPath(localPath.AsString);
    }

    /// <summary>
    /// Filtro que remove do resultado os documentos de conteúdo protegido, para uso
    /// em operações de exclusão em massa no catálogo.
    /// </summary>
    /// <returns>Filtro do MongoDB.</returns>
    internal static FilterDefinition<BsonDocument> NotProtected()
        => Builders<BsonDocument>.Filter.Not(Builders<BsonDocument>.Filter.Regex("name", ProtectedNameExpression));

    private static string BuildNamePattern()
    {
        var extensions = NebulaMetadataExportService.MetadataSidecarExtensions
            .Where(static extension => extension.StartsWith('.'))
            .Select(static extension => extension[1..])
            .Where(static extension => extension.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(static extension => extension.Length)
            .Select(Regex.Escape);

        // A âncora final garante que apenas a extensão real conte: "fanart1.jpg" é
        // protegido, "Filme (2020).mkv" não é.
        return $"\\.(?:{string.Join("|", extensions)})$";
    }
}
