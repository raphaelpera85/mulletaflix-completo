using MulletaFlix.Server.Extensions;
using Xunit;

namespace Jellyfin.Server.Tests.Observability;

/// <summary>
/// O endpoint <c>/metrics</c> expõe automaticamente os <c>Meter</c> do
/// MySqlConnector, cujo label <c>pool_name</c> é a connection string inteira.
/// O aceite da Fase 1 do roadmap proíbe expor configuração sensível nas
/// métricas, então esse label precisa ser reduzido a um identificador estável.
/// </summary>
public class MetricsLabelRedactionTests
{
    [Fact]
    public void RedactPoolName_RemovesCredentialsAndHostFromConnectionString()
    {
        const string PoolName = "Server=127.0.0.1;Port=3306;User ID=root;Database=mulletaflix;Pooling=True;Maximum Pool Size=100";

        var redacted = MetricsLabelRedactor.RedactPoolName(PoolName);

        // Nada de credencial, host ou porta.
        Assert.DoesNotContain("root", redacted, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("127.0.0.1", redacted, System.StringComparison.Ordinal);
        Assert.DoesNotContain("3306", redacted, System.StringComparison.Ordinal);
        Assert.DoesNotContain("User ID", redacted, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Server=", redacted, System.StringComparison.OrdinalIgnoreCase);

        // O nome do banco identifica o pool sem revelar como alcançá-lo, o que
        // preserva a utilidade operacional da métrica.
        Assert.Equal("mulletaflix", redacted);
    }

    [Fact]
    public void RedactPoolName_KeepsPoolsDistinguishableByDatabase()
    {
        var main = MetricsLabelRedactor.RedactPoolName(
            "Server=127.0.0.1;Port=3306;User ID=root;Database=mulletaflix;Pooling=True");
        var introSkipper = MetricsLabelRedactor.RedactPoolName(
            "Server=127.0.0.1;Port=3306;User ID=root;Database=mulletaflix_introskipper;Pooling=True");

        Assert.NotEqual(main, introSkipper);
    }

    [Fact]
    public void RedactPoolName_UsesAStableFallbackWhenThereIsNoDatabase()
    {
        // Pools de bootstrap não nomeiam banco. Devolver a string original
        // vazaria host e usuário; devolver algo aleatório criaria uma série
        // nova a cada scrape e estouraria a cardinalidade.
        const string Bootstrap = "Server=127.0.0.1;Port=3306;User ID=root;SSL Mode=None;Character Set=utf8mb4";

        var first = MetricsLabelRedactor.RedactPoolName(Bootstrap);
        var second = MetricsLabelRedactor.RedactPoolName(Bootstrap);

        Assert.Equal(first, second);
        Assert.DoesNotContain("root", first, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("127.0.0.1", first, System.StringComparison.Ordinal);
        Assert.NotEmpty(first);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RedactPoolName_HandlesMissingValue(string? poolName)
    {
        Assert.Equal("unknown", MetricsLabelRedactor.RedactPoolName(poolName));
    }

    [Fact]
    public void ShouldPublishInstrument_ExcludesMeterWhoseLabelsCarryConnectionStrings()
    {
        // O MeterAdapter do prometheus-net só permite filtrar por instrumento;
        // não há gancho para reescrever labels. Como todo instrumento do
        // MySqlConnector usa `pool_name` (a connection string inteira, com host,
        // porta e usuário) e a mesma informação já é servida de forma segura
        // pelo painel e pelos health checks, o meter inteiro fica fora do
        // endpoint público em vez de vazar a topologia do banco.
        Assert.False(MetricsLabelRedactor.ShouldPublishInstrument("MySqlConnector"));
        Assert.False(MetricsLabelRedactor.ShouldPublishInstrument("mysqlconnector"));

        // Nenhum outro meter é afetado.
        Assert.True(MetricsLabelRedactor.ShouldPublishInstrument("Microsoft.AspNetCore.Hosting"));
        Assert.True(MetricsLabelRedactor.ShouldPublishInstrument("MulletaFlix.Nebula.UploadEngine"));
        Assert.True(MetricsLabelRedactor.ShouldPublishInstrument("System.Net.Http"));
        Assert.True(MetricsLabelRedactor.ShouldPublishInstrument(null));
    }

    [Fact]
    public void RedactPoolName_DoesNotEchoAnUnparsableValue()
    {
        var redacted = MetricsLabelRedactor.RedactPoolName("Password=[REDACTED] not-a-connection-string");

        Assert.DoesNotContain("[REDACTED]", redacted, System.StringComparison.OrdinalIgnoreCase);
    }
}
