using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Jellyfin.Server.Tests.Dependencies;

/// <summary>
/// Verifica que dependências depreciadas não chegam ao artefato publicado.
/// </summary>
/// <remarks>
/// `dotnet list package --deprecated` reporta `Microsoft.AspNetCore.Http.Features
/// 2.0.0` e `Microsoft.Extensions.DependencyModel 1.1.0` como transitivos de
/// `FubarDev.FtpServer` e do MVC legado — versões de 2017 num projeto .NET 10.
/// O grafo de restauração, porém, não é o que roda: o runtime unifica para as
/// versões do framework. Este teste trava esse comportamento, para que uma
/// mudança futura de pacote não reintroduza silenciosamente um assembly antigo
/// no artefato — o mesmo padrão de problema que trouxe o `Newtonsoft.Json 9.0.1`
/// vulnerável.
/// </remarks>
public class DeprecatedDependencyResolutionTests
{
    [Theory]
    [InlineData("Microsoft.Extensions.DependencyModel", 10)]
    [InlineData("Microsoft.AspNetCore.Http.Features", 10)]
    public void RuntimeResolvesDeprecatedTransitivePackagesToCurrentMajor(string assemblyName, int minimumMajor)
    {
        // O assembly é procurado ao lado do assembly de teste, que reflete o
        // conjunto efetivamente copiado pela build.
        var baseDirectory = Path.GetDirectoryName(typeof(DeprecatedDependencyResolutionTests).Assembly.Location)!;
        var candidate = Path.Combine(baseDirectory, assemblyName + ".dll");

        if (!File.Exists(candidate))
        {
            Assert.Skip($"{assemblyName}.dll não está presente na saída de teste; nada a verificar aqui.");
            return;
        }

        var version = AssemblyName.GetAssemblyName(candidate).Version;

        Assert.NotNull(version);
        Assert.True(
            version!.Major >= minimumMajor,
            $"{assemblyName} resolvido como {version}; esperado major >= {minimumMajor}. "
            + "Uma dependência transitiva depreciada voltou a vencer a unificação de versões.");
    }

    [Fact]
    public void NoAssemblyFromTheAbandonedNewtonsoftLineIsPresent()
    {
        // Regressão do problema já corrigido: a linha 9.x do Newtonsoft.Json era
        // vulnerável e chegava por FubarDev e pelo MVC legado.
        var baseDirectory = Path.GetDirectoryName(typeof(DeprecatedDependencyResolutionTests).Assembly.Location)!;
        var newtonsoft = Path.Combine(baseDirectory, "Newtonsoft.Json.dll");

        if (!File.Exists(newtonsoft))
        {
            Assert.Skip("Newtonsoft.Json.dll não está presente na saída de teste.");
            return;
        }

        var version = AssemblyName.GetAssemblyName(newtonsoft).Version;

        Assert.NotNull(version);
        Assert.True(
            version!.Major >= 13,
            $"Newtonsoft.Json resolvido como {version}; a linha 9.x é vulnerável e não pode voltar.");
    }
}
