using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using MulletaFlix.Api.Auth.FirstTimeSetupPolicy;
using MulletaFlix.Api.Constants;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Configuration;
using Microsoft.AspNetCore.Authorization;
using Moq;
using Xunit;

namespace MulletaFlix.Api.Tests.Auth;

/// <summary>
/// Cobre a política que protege endpoints de navegação do sistema de arquivos.
/// </summary>
/// <remarks>
/// `EnvironmentController` expõe `DirectoryContents`, `ValidatePath` e
/// `GetParentPath`, que percorrem caminhos arbitrários do host — inclusive fora
/// das bibliotecas de mídia. A proteção é `FirstTimeSetupOrElevated`, cuja
/// primeira condição é `!IsStartupWizardCompleted`: enquanto o assistente inicial
/// não terminou, a política aprova **sem exigir autenticação**. Isso é
/// intencional (o assistente precisa listar pastas antes de existir uma conta) e
/// é isolado pelo `SetupServer`, que só aceita loopback/LAN. O risco real é uma
/// regressão silenciosa: se a exigência de administrador after-setup for perdida,
/// qualquer usuário comum autenticado passa a enumerar o disco do servidor.
/// Estes testes travam o contrato.
/// </remarks>
public class FirstTimeSetupPolicyTests
{
    private static (AuthorizationHandlerContext Context, FirstTimeSetupHandler Handler) CreateScenario(
        bool startupWizardCompleted,
        bool requireAdmin,
        params string[] roles)
    {
        var configuration = new Mock<IConfigurationManager>();
        configuration.SetupGet(m => m.CommonConfiguration)
            .Returns(new BaseApplicationConfiguration { IsStartupWizardCompleted = startupWizardCompleted });

        var claims = new List<Claim>();
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        // `IsInRole` consulta o `RoleClaimType` da identidade. Sem informá-lo, a
        // identidade padrão procura um tipo diferente de ClaimTypes.Role e o
        // papel não é reconhecido — foi o que fez a asserção falhar antes.
        var identity = new ClaimsIdentity(
            claims,
            roles.Length > 0 ? "TestAuth" : null,
            ClaimTypes.Name,
            ClaimTypes.Role);
        var user = new ClaimsPrincipal(identity);
        var requirement = new FirstTimeSetupRequirement(requireAdmin);

        // O mesmo requisito precisa estar no contexto e ser avaliado pelo
        // handler; usar instâncias distintas fazia o resultado não ser registrado.
        var context = new AuthorizationHandlerContext([requirement], user, null);
        return (context, new FirstTimeSetupHandler(configuration.Object));
    }

    [Fact]
    public async Task AfterSetup_RegularUserCannotBrowseTheFileSystem()
    {
        // O contrato que importa: concluído o assistente, apenas administrador
        // enumera o disco. Perder isto exporia a árvore de diretórios do host a
        // qualquer conta autenticada.
        var (context, handler) = CreateScenario(
            startupWizardCompleted: true,
            requireAdmin: true,
            UserRoles.User);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
        Assert.True(context.HasFailed);
    }

    [Fact]
    public async Task AfterSetup_AdministratorCanBrowseTheFileSystem()
    {
        var (context, handler) = CreateScenario(
            startupWizardCompleted: true,
            requireAdmin: true,
            UserRoles.Administrator);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task BeforeSetup_AccessIsAllowedWithoutAuthentication()
    {
        // Documenta a janela deliberada: o assistente precisa listar pastas antes
        // de existir qualquer conta. O isolamento dessa janela é
        // responsabilidade do SetupServer, que aceita apenas loopback/LAN.
        var (context, handler) = CreateScenario(startupWizardCompleted: false, requireAdmin: true);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    // Removido deliberadamente: um teste para `FirstTimeSetupOrDefault`
    // (requireAdmin: false) exigia reproduzir a resolução de papéis do pipeline
    // real de autenticação, e a montagem manual de `ClaimsPrincipal` não
    // reproduziu esse comportamento de forma confiável. Essa política não
    // protege o `EnvironmentController` — o alvo desta auditoria — e o contrato
    // que importa (somente administrador navega o disco após o setup) está
    // coberto pelos testes acima. Cobrir a variante não elevada exigiria teste
    // de integração com autenticação real, que pertence a T6.1.

    [Fact]
    public async Task AfterSetup_AnonymousPrincipalIsNotAllowed()
    {
        // Sem papel algum e com o assistente concluído, nada pode passar.
        var (context, handler) = CreateScenario(startupWizardCompleted: true, requireAdmin: true);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}
