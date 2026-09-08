#pragma warning disable CA1707 // Identifiers should not contain underscores

using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FubarDev.FtpServer.AccountManagement;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>
/// Provedor de autenticação de usuários FTP usando MongoDB e BCrypt nativo em C#.
/// </summary>
public sealed class NebulaFtpMembershipProvider : IMembershipProvider
{
    private readonly NebulaMongoContext _mongoContext;
    private readonly ILogger<NebulaFtpMembershipProvider> _logger;

    /// <summary>
    /// Inicializa uma nova instância de <see cref="NebulaFtpMembershipProvider"/>.
    /// </summary>
    public NebulaFtpMembershipProvider(NebulaMongoContext mongoContext, ILogger<NebulaFtpMembershipProvider> logger)
    {
        _mongoContext = mongoContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<MemberValidationResult> ValidateUserAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return new MemberValidationResult(MemberValidationStatus.InvalidLogin);
        }

        try
        {
            var userDoc = await _mongoContext.FindUserByLoginAsync(username).ConfigureAwait(false);
            if (userDoc == null)
            {
                _logger.LogWarning("[NEBULA-AUTH] Usuário '{Username}' não encontrado no MongoDB.", username);
                return new MemberValidationResult(MemberValidationStatus.InvalidLogin);
            }

            var hash = userDoc.GetValue("password_hash", string.Empty).AsString;
            if (string.IsNullOrWhiteSpace(hash))
            {
                return new MemberValidationResult(MemberValidationStatus.InvalidLogin);
            }

            var isValid = BCrypt.Net.BCrypt.Verify(password, hash);
            if (!isValid)
            {
                _logger.LogWarning("[NEBULA-AUTH] Senha inválida para usuário '{Username}'.", username);
                return new MemberValidationResult(MemberValidationStatus.InvalidLogin);
            }

            _logger.LogInformation("[NEBULA-AUTH] Usuário '{Username}' autenticado com sucesso no FTP!", username);
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, username)]);
            return new MemberValidationResult(MemberValidationStatus.AuthenticatedUser, new ClaimsPrincipal(identity));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NEBULA-AUTH] Erro ao validar credenciais do usuário '{Username}'.", username);
            return new MemberValidationResult(MemberValidationStatus.InvalidLogin);
        }
    }
}
