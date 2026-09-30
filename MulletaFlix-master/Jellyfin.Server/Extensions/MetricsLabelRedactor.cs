using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace MulletaFlix.Server.Extensions;

/// <summary>
/// Reduz labels de métricas que carregariam configuração sensível.
/// </summary>
/// <remarks>
/// O endpoint <c>/metrics</c> republica automaticamente os <c>Meter</c> do
/// MySqlConnector, cujo label <c>pool_name</c> é a connection string inteira —
/// incluindo host, porta e usuário do banco. Além de violar o aceite da Fase 1
/// ("health check e métricas não revelam configuração sensível"), isso entrega a
/// topologia do banco a qualquer pessoa que alcance o endpoint.
/// </remarks>
public static class MetricsLabelRedactor
{
    private const string Unknown = "unknown";

    /// <summary>
    /// Meters cujos labels carregam configuração sensível e por isso não são
    /// republicados no endpoint Prometheus.
    /// </summary>
    private const string MySqlConnectorMeter = "MySqlConnector";

    /// <summary>
    /// Decide se um meter pode ser republicado no endpoint <c>/metrics</c>.
    /// </summary>
    /// <remarks>
    /// O <c>MeterAdapter</c> do prometheus-net só oferece filtro por instrumento,
    /// sem gancho para reescrever labels. Como TODO instrumento do MySqlConnector
    /// usa <c>pool_name</c> — a connection string inteira, com host, porta e
    /// usuário — e a saúde do banco já é servida com segurança pelo painel e
    /// pelos health checks, o meter inteiro fica fora do endpoint em vez de
    /// expor a topologia do banco.
    /// </remarks>
    /// <param name="meterName">Nome do meter.</param>
    /// <returns><see langword="true"/> quando a publicação é segura.</returns>
    public static bool ShouldPublishInstrument(string? meterName)
    {
        if (string.IsNullOrWhiteSpace(meterName))
        {
            return true;
        }

        return !meterName.StartsWith(MySqlConnectorMeter, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Converte o nome de um pool de conexões em um identificador seguro.
    /// </summary>
    /// <param name="poolName">Valor original do label.</param>
    /// <returns>O nome do banco, ou um identificador estável e anônimo.</returns>
    public static string RedactPoolName(string? poolName)
    {
        if (string.IsNullOrWhiteSpace(poolName))
        {
            return Unknown;
        }

        // O nome do banco identifica o pool sem revelar como alcançá-lo, o que
        // preserva a utilidade operacional da métrica.
        var database = ExtractDatabase(poolName);
        if (!string.IsNullOrWhiteSpace(database))
        {
            return database;
        }

        // Pools de bootstrap não nomeiam banco. Um hash curto e determinístico
        // mantém as séries distinguíveis e estáveis entre scrapes, sem ecoar o
        // valor original nem estourar a cardinalidade.
        return "pool-" + ShortStableHash(poolName);
    }

    private static string? ExtractDatabase(string poolName)
    {
        foreach (var segment in poolName.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = segment.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            var key = segment[..separator].Trim();
            if (!key.Equals("Database", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("Initial Catalog", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = segment[(separator + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string ShortStableHash(string value)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        var builder = new StringBuilder(16);
        for (var i = 0; i < 8; i++)
        {
            builder.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }
}
