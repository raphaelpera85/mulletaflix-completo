using System;

namespace MulletaFlix.LiveTv;

/// <summary>
/// Reduz URLs de Live TV a uma forma segura para log.
/// </summary>
/// <remarks>
/// URLs de provedores IPTV carregam credenciais rotineiramente: em
/// <c>userinfo</c> (<c>http://usuario:senha@host/</c>), na query string
/// (<c>?username=...&amp;password=...</c>) ou embutidas no path, como no padrão
/// Xtream Codes (<c>/usuario/senha/id.ts</c>). Registrar a URL completa grava a
/// assinatura IPTV do usuário em texto claro nos arquivos de log, que costumam
/// ser anexados a pedidos de suporte.
/// </remarks>
public static class LiveTvUrlRedactor
{
    private const string Redacted = "[REDACTED]";

    /// <summary>
    /// Devolve apenas esquema, host e porta, descartando credenciais, path e query.
    /// </summary>
    /// <param name="url">A URL a redigir.</param>
    /// <returns>Uma forma segura para log, ou <c>[REDACTED]</c>.</returns>
    public static string Redact(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return Redacted;
        }

        // Um valor não parseável pode ser um caminho local ou lixo contendo
        // credencial; ecoá-lo anularia a redação.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed))
        {
            return Redacted;
        }

        // Atenção: GetLeftPart(UriPartial.Authority) PRESERVA o userinfo
        // (`http://usuario:senha@host`), então a authority é remontada a partir
        // do esquema, host e porta para descartar a credencial.
        var authority = parsed.IsDefaultPort
            ? string.Concat(parsed.Scheme, "://", parsed.Host)
            : string.Concat(parsed.Scheme, "://", parsed.Host, ":", parsed.Port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (string.IsNullOrEmpty(parsed.Host))
        {
            return Redacted;
        }

        // O path pode conter credenciais (Xtream Codes) e não há forma
        // confiável de distinguir segmento sensível de segmento inócuo, então
        // ele é substituído por inteiro quando existe.
        var path = parsed.AbsolutePath;
        if (string.IsNullOrEmpty(path) || string.Equals(path, "/", StringComparison.Ordinal))
        {
            return authority + "/";
        }

        return string.Concat(authority, "/", Redacted);
    }

    /// <summary>
    /// Redige apenas valores remotos (<c>http</c>/<c>https</c>), devolvendo
    /// caminhos locais inalterados.
    /// </summary>
    /// <remarks>
    /// Campos de configuração como o caminho XMLTV aceitam tanto um arquivo
    /// local quanto uma URL. O caminho local é diagnóstico útil e não carrega
    /// credencial de provedor, enquanto a URL pode carregar; aplicar
    /// <see cref="Redact"/> a tudo apagaria informação legítima.
    /// </remarks>
    /// <param name="pathOrUrl">O caminho local ou URL.</param>
    /// <returns>O caminho original, ou a URL redigida.</returns>
    public static string RedactIfRemote(string? pathOrUrl)
    {
        if (string.IsNullOrWhiteSpace(pathOrUrl))
        {
            return Redacted;
        }

        if (!pathOrUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !pathOrUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return pathOrUrl;
        }

        return Redact(pathOrUrl);
    }
}
