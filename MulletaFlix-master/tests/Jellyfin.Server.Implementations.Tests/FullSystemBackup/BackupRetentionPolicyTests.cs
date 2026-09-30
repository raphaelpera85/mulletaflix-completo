using System;
using System.Collections.Generic;
using System.Linq;
using MulletaFlix.Server.Implementations.FullSystemBackup;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.FullSystemBackup;

/// <summary>
/// Política de retenção e rotação de backups.
/// </summary>
/// <remarks>
/// T4.2 exige retenção e rotação. Sem elas, cada execução agendada acrescenta um
/// arquivo de centenas de megabytes e nada remove os antigos: o volume de backup
/// enche, e o backup seguinte falha — justamente quando ele é mais necessário.
/// A regra precisa ser conservadora por construção: o risco de apagar o único
/// backup bom é maior que o de guardar um arquivo a mais.
/// </remarks>
public class BackupRetentionPolicyTests
{
    private static BackupRetentionCandidate Candidate(string name, int daysOld)
        => new(name, DateTime.UtcNow.AddDays(-daysOld));

    [Fact]
    public void SelectForRemoval_KeepsTheMostRecentBackupsUpToTheLimit()
    {
        var candidates = new[]
        {
            Candidate("b-5.zip", 5),
            Candidate("b-1.zip", 1),
            Candidate("b-3.zip", 3),
            Candidate("b-10.zip", 10)
        };

        var removal = BackupRetentionPolicy.SelectForRemoval(candidates, keepCount: 2, maximumAge: null).ToArray();

        // Mantém os dois mais novos; remove os dois mais antigos.
        Assert.Equal(2, removal.Length);
        Assert.Contains("b-5.zip", removal.Select(c => c.Name));
        Assert.Contains("b-10.zip", removal.Select(c => c.Name));
    }

    [Fact]
    public void SelectForRemoval_NeverRemovesTheLastRemainingBackup()
    {
        var candidates = new[] { Candidate("unico.zip", 400) };

        // Mesmo vencido por idade e com limite 0, o último backup sobrevive:
        // ficar sem nenhuma cópia é pior que guardar uma antiga.
        var removal = BackupRetentionPolicy
            .SelectForRemoval(candidates, keepCount: 0, maximumAge: TimeSpan.FromDays(1))
            .ToArray();

        Assert.Empty(removal);
    }

    [Fact]
    public void SelectForRemoval_AppliesAgeLimitOnTopOfTheCountLimit()
    {
        var candidates = new[]
        {
            Candidate("novo.zip", 1),
            Candidate("velho.zip", 90),
            Candidate("medio.zip", 10)
        };

        // Limite de contagem alto o bastante para manter todos, mas a idade
        // elimina o vencido.
        var removal = BackupRetentionPolicy
            .SelectForRemoval(candidates, keepCount: 10, maximumAge: TimeSpan.FromDays(30))
            .ToArray();

        Assert.Single(removal);
        Assert.Equal("velho.zip", removal[0].Name);
    }

    [Fact]
    public void SelectForRemoval_WithNonPositiveKeepCountFallsBackToTheSafeDefault()
    {
        var candidates = Enumerable.Range(1, 10)
            .Select(i => Candidate($"b-{i}.zip", i))
            .ToArray();

        // Configuração inválida não pode ser interpretada como "apague tudo".
        var removal = BackupRetentionPolicy.SelectForRemoval(candidates, keepCount: 0, maximumAge: null).ToArray();

        var kept = 10 - removal.Length;
        Assert.Equal(BackupRetentionPolicy.DefaultKeepCount, kept);
    }

    [Fact]
    public void SelectForRemoval_WithoutLimitsKeepsEverything()
    {
        var candidates = Enumerable.Range(1, 5)
            .Select(i => Candidate($"b-{i}.zip", i * 100))
            .ToArray();

        // Retenção desativada explicitamente (contagem negativa = ilimitado)
        // não remove nada, mesmo com arquivos muito antigos.
        var removal = BackupRetentionPolicy
            .SelectForRemoval(candidates, keepCount: -1, maximumAge: null)
            .ToArray();

        Assert.Empty(removal);
    }

    [Fact]
    public void SelectForRemoval_IgnoresCandidatesWithoutATimestamp()
    {
        var candidates = new List<BackupRetentionCandidate>
        {
            Candidate("datado.zip", 1),
            new("sem-data.zip", null)
        };

        // Sem data confiável não há como julgar a idade; remover às cegas
        // poderia descartar justamente o backup mais recente.
        var removal = BackupRetentionPolicy
            .SelectForRemoval(candidates, keepCount: 1, maximumAge: TimeSpan.FromDays(1))
            .ToArray();

        Assert.DoesNotContain("sem-data.zip", removal.Select(c => c.Name));
    }

    [Fact]
    public void SelectForRemoval_HandlesAnEmptySetWithoutThrowing()
    {
        Assert.Empty(BackupRetentionPolicy.SelectForRemoval([], keepCount: 3, maximumAge: TimeSpan.FromDays(7)));
    }
}
