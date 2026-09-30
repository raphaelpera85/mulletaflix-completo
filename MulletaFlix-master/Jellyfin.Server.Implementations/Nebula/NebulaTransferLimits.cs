using System;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>
/// Limites de concorrência e de taxa das transferências Nebula.
/// </summary>
/// <remarks>
/// Upload e download têm orçamentos independentes, vindos de chaves de
/// configuração distintas: saturar as conexões de download não pode consumir o
/// orçamento de upload, nem o contrário. Cada valor é limitado a uma faixa
/// segura porque a configuração é livre — sem teto, um valor errado abriria
/// centenas de transferências simultâneas, esgotando banda, descritores e a
/// tolerância do Telegram a chamadas paralelas.
/// </remarks>
public static class NebulaTransferLimits
{
    /// <summary>
    /// Uploads simultâneos usados quando a configuração é inválida.
    /// </summary>
    public const int DefaultUploadConcurrency = 8;

    /// <summary>
    /// Teto de uploads simultâneos.
    /// </summary>
    public const int MaxUploadConcurrency = 32;

    /// <summary>
    /// Conexões de download por arquivo usadas quando a configuração é inválida.
    /// </summary>
    public const int DefaultDownloadConnections = 24;

    /// <summary>
    /// Teto de conexões de download por arquivo.
    /// </summary>
    public const int MaxDownloadConnections = 32;

    /// <summary>
    /// Chamadas Telegram permitidas por bot dentro de <see cref="TelegramBurstWindow"/>.
    /// </summary>
    /// <remarks>
    /// Complementa os limites de simultaneidade: com vários slots livres, os
    /// workers disparavam chamadas instantâneas no mesmo bot recém-liberado e
    /// provocavam o flood-wait seguinte. O valor é conservador de propósito —
    /// perder alguns milissegundos por chamada custa muito menos que um
    /// flood-wait de dezenas de segundos, que bloqueia o bot inteiro.
    /// </remarks>
    public const int MaxTelegramCallsPerWindow = 4;

    /// <summary>
    /// Gets a janela deslizante usada no controle de rajada do Telegram.
    /// </summary>
    public static TimeSpan TelegramBurstWindow { get; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Resolve quantos uploads podem ocorrer ao mesmo tempo.
    /// </summary>
    /// <param name="configured">Valor configurado.</param>
    /// <returns>Concorrência dentro da faixa segura.</returns>
    public static int ResolveUploadConcurrency(int configured)
    {
        if (configured <= 0)
        {
            return DefaultUploadConcurrency;
        }

        return configured > MaxUploadConcurrency ? MaxUploadConcurrency : configured;
    }

    /// <summary>
    /// Resolve quantas conexões paralelas um download pode abrir.
    /// </summary>
    /// <param name="configured">Valor configurado.</param>
    /// <returns>Número de conexões dentro da faixa segura.</returns>
    public static int ResolveDownloadConnections(int configured)
    {
        if (configured <= 0)
        {
            return DefaultDownloadConnections;
        }

        return configured > MaxDownloadConnections ? MaxDownloadConnections : configured;
    }
}
