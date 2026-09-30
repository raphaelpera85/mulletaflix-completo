using System;
using System.Collections.Generic;
using System.Linq;

namespace MulletaFlix.Server.Implementations.FullSystemBackup;

/// <summary>
/// Um backup candidato à rotação.
/// </summary>
/// <param name="Name">Nome do arquivo.</param>
/// <param name="CreatedUtc">Data de criação, quando conhecida.</param>
public readonly record struct BackupRetentionCandidate(string Name, DateTime? CreatedUtc);

/// <summary>
/// Decide quais backups podem ser removidos pela rotação.
/// </summary>
/// <remarks>
/// Sem retenção, cada execução agendada acrescenta um arquivo de centenas de
/// megabytes e nada remove os antigos: o volume enche e o backup seguinte falha,
/// justamente quando ele é mais necessário. A regra é conservadora por
/// construção — o risco de apagar o único backup bom é maior que o de guardar um
/// arquivo a mais — e por isso o último backup nunca é removido, configuração
/// inválida cai no padrão em vez de "apagar tudo", e candidatos sem data
/// confiável ficam de fora.
/// </remarks>
public static class BackupRetentionPolicy
{
    /// <summary>
    /// Quantidade mantida quando a configuração é inválida.
    /// </summary>
    public const int DefaultKeepCount = 5;

    /// <summary>
    /// Seleciona os backups elegíveis à remoção.
    /// </summary>
    /// <param name="candidates">Backups existentes.</param>
    /// <param name="keepCount">Quantos manter. Zero cai no padrão; negativo desativa o limite por contagem.</param>
    /// <param name="maximumAge">Idade máxima; <see langword="null"/> desativa o limite por idade.</param>
    /// <returns>Os backups que podem ser apagados.</returns>
    public static IEnumerable<BackupRetentionCandidate> SelectForRemoval(
        IEnumerable<BackupRetentionCandidate> candidates,
        int keepCount,
        TimeSpan? maximumAge)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var all = candidates.ToList();
        if (all.Count <= 1)
        {
            // Zero ou um backup: nada a rotacionar. Ficar sem nenhuma cópia é
            // pior que manter uma antiga.
            return [];
        }

        // Retenção por contagem desativada explicitamente.
        var unlimitedCount = keepCount < 0;

        // Configuração inválida (zero) não pode ser lida como "apague tudo".
        var effectiveKeep = keepCount == 0 ? DefaultKeepCount : keepCount;

        if (unlimitedCount && maximumAge is null)
        {
            return [];
        }

        // Mais novos primeiro; sem data vai para o fim mas nunca é removido.
        var ordered = all
            .OrderByDescending(candidate => candidate.CreatedUtc ?? DateTime.MinValue)
            .ToList();

        var removal = new List<BackupRetentionCandidate>();
        var now = DateTime.UtcNow;

        for (var index = 0; index < ordered.Count; index++)
        {
            var candidate = ordered[index];

            // Sem data confiável não há como julgar idade nem posição real;
            // remover às cegas poderia descartar o backup mais recente.
            if (candidate.CreatedUtc is null)
            {
                continue;
            }

            var exceedsCount = !unlimitedCount && index >= effectiveKeep;
            var exceedsAge = maximumAge.HasValue
                && now - candidate.CreatedUtc.Value > maximumAge.Value;

            if (exceedsCount || exceedsAge)
            {
                removal.Add(candidate);
            }
        }

        // Nunca esvaziar o conjunto: preserva o mais recente entre os marcados
        // se a regra tentar remover todos.
        if (removal.Count >= all.Count)
        {
            var keepThisOne = removal
                .OrderByDescending(candidate => candidate.CreatedUtc ?? DateTime.MinValue)
                .First();
            removal.Remove(keepThisOne);
        }

        return removal;
    }
}
