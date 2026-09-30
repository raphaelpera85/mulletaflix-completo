using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>
/// Limita rajadas por janela de tempo deslizante.
/// </summary>
/// <remarks>
/// Limitar apenas a simultaneidade não impede rajada: com N slots livres o motor
/// dispara N chamadas no mesmo instante, e o Telegram responde com flood-wait —
/// exatamente o custo que a rotação de bots tenta evitar. Este limitador
/// complementa <see cref="NebulaTransferLimits"/> controlando a taxa, não o
/// paralelismo: quantas operações podem começar dentro de uma janela.
/// A janela é deslizante, não fixa, para que a cota volte gradualmente em vez de
/// liberar um novo pico inteiro na virada do intervalo.
/// </remarks>
public sealed class NebulaBurstLimiter
{
    private readonly int _maxPerWindow;
    private readonly TimeSpan _window;
    private readonly Func<TimeSpan> _clock;
    private readonly Queue<TimeSpan> _permits = new();
    private readonly object _gate = new();

    /// <summary>
    /// Inicializa uma nova instância de <see cref="NebulaBurstLimiter"/>.
    /// </summary>
    /// <param name="maxPerWindow">Operações permitidas por janela. Valor não positivo desativa o controle.</param>
    /// <param name="window">Duração da janela deslizante. Valor não positivo desativa o controle.</param>
    /// <param name="clock">Relógio monotônico; usado nos testes. O padrão é o cronômetro do processo.</param>
    public NebulaBurstLimiter(int maxPerWindow, TimeSpan window, Func<TimeSpan>? clock = null)
    {
        _maxPerWindow = maxPerWindow;
        _window = window;
        _clock = clock ?? (() => Stopwatch.GetElapsedTime(0));
    }

    /// <summary>
    /// Gets a value indicating whether o controle está ativo.
    /// </summary>
    /// <remarks>
    /// Uma configuração inválida desativa o controle em vez de bloquear tudo:
    /// interpretar um limite zero literalmente pararia a fila para sempre.
    /// </remarks>
    public bool IsEnabled => _maxPerWindow > 0 && _window > TimeSpan.Zero;

    /// <summary>
    /// Tenta consumir uma permissão da janela atual.
    /// </summary>
    /// <returns><see langword="true"/> quando a operação pode começar agora.</returns>
    public bool TryAcquire()
    {
        if (!IsEnabled)
        {
            return true;
        }

        lock (_gate)
        {
            var now = _clock();
            Trim(now);

            if (_permits.Count >= _maxPerWindow)
            {
                return false;
            }

            _permits.Enqueue(now);
            return true;
        }
    }

    /// <summary>
    /// Informa quanto esperar até a próxima permissão ficar disponível.
    /// </summary>
    /// <returns>O tempo restante, ou <see cref="TimeSpan.Zero"/> quando há cota livre.</returns>
    public TimeSpan GetRetryDelay()
    {
        if (!IsEnabled)
        {
            return TimeSpan.Zero;
        }

        lock (_gate)
        {
            var now = _clock();
            Trim(now);

            if (_permits.Count < _maxPerWindow)
            {
                return TimeSpan.Zero;
            }

            // A permissão mais antiga define quando a cota é devolvida.
            var oldest = _permits.Peek();
            var remaining = oldest + _window - now;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    private void Trim(TimeSpan now)
    {
        while (_permits.Count > 0 && now - _permits.Peek() >= _window)
        {
            _permits.Dequeue();
        }
    }
}
