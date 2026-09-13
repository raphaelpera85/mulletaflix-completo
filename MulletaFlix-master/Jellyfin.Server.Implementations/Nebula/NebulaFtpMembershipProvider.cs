#pragma warning disable CA1707 // Identifiers should not contain underscores

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
    private static readonly TimeSpan FailedLoginWindow = TimeSpan.FromMinutes(15);
    private const int MaxFailedLogins = 5;
    private const int MaxTrackedUsers = 10_000;

    private readonly NebulaMongoContext _mongoContext;
    private readonly ILogger<NebulaFtpMembershipProvider> _logger;
    private readonly ConcurrentDictionary<string, FailedLoginEntry> _failedLogins = new(StringComparer.OrdinalIgnoreCase);

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

        var loginKey = username.Trim();
        if (IsBlocked(loginKey))
        {
            _logger.LogWarning("[NEBULA-AUTH] Login FTP temporariamente bloqueado para '{Username}' após falhas consecutivas.", username);
            return new MemberValidationResult(MemberValidationStatus.InvalidLogin);
        }

        try
        {
            var userDoc = await _mongoContext.FindUserByLoginAsync(loginKey).ConfigureAwait(false);
            if (userDoc == null)
            {
                _logger.LogWarning("[NEBULA-AUTH] Usuário '{Username}' não encontrado no MongoDB.", username);
                RecordFailure(loginKey);
                return new MemberValidationResult(MemberValidationStatus.InvalidLogin);
            }

            var hash = userDoc.GetValue("password_hash", string.Empty).AsString;
            if (string.IsNullOrWhiteSpace(hash))
            {
                RecordFailure(loginKey);
                return new MemberValidationResult(MemberValidationStatus.InvalidLogin);
            }

            var isValid = BCrypt.Net.BCrypt.Verify(password, hash);
            if (!isValid)
            {
                _logger.LogWarning("[NEBULA-AUTH] Senha inválida para usuário '{Username}'.", username);
                RecordFailure(loginKey);
                return new MemberValidationResult(MemberValidationStatus.InvalidLogin);
            }

            _failedLogins.TryRemove(loginKey, out _);
            _logger.LogInformation("[NEBULA-AUTH] Usuário '{Username}' autenticado com sucesso no FTP!", loginKey);
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, loginKey)]);
            return new MemberValidationResult(MemberValidationStatus.AuthenticatedUser, new ClaimsPrincipal(identity));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NEBULA-AUTH] Erro ao validar credenciais do usuário '{Username}'.", username);
            RecordFailure(loginKey);
            return new MemberValidationResult(MemberValidationStatus.InvalidLogin);
        }
    }

    private bool IsBlocked(string username)
    {
        if (!_failedLogins.TryGetValue(username, out var entry))
        {
            return false;
        }

        return entry.IsBlocked();
    }

    private void RecordFailure(string username)
    {
        var entry = _failedLogins.GetOrAdd(username, _ => new FailedLoginEntry());
        entry.RecordFailure();

        if (_failedLogins.Count > MaxTrackedUsers)
        {
            foreach (var item in _failedLogins)
            {
                if (item.Value.IsExpired())
                {
                    _failedLogins.TryRemove(item.Key, out _);
                }
            }
        }
    }

    private sealed class FailedLoginEntry
    {
        private readonly object _syncRoot = new();

        private readonly List<DateTime> _timestamps = new();

        public bool IsBlocked()
        {
            lock (_syncRoot)
            {
                Prune(this);
                return _timestamps.Count >= MaxFailedLogins;
            }
        }

        public void RecordFailure()
        {
            lock (_syncRoot)
            {
                Prune(this);
                _timestamps.Add(DateTime.UtcNow);
            }
        }

        public bool IsExpired()
        {
            lock (_syncRoot)
            {
                Prune(this);
                return _timestamps.Count == 0;
            }
        }

        private void Prune(FailedLoginEntry entry)
        {
            var cutoff = DateTime.UtcNow - FailedLoginWindow;
            entry._timestamps.RemoveAll(timestamp => timestamp < cutoff);
        }
    }
}
