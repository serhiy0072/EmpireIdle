using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Common.Behaviors
{
    /// <summary>
    /// Захист від доступу до чужих даних (IDOR): запит, що несе PlayerId,
    /// виконується лише якщо цей PlayerId збігається з гравцем у токені.
    /// Відсутність гравця в скоупі — це відмова, а не дозвіл.
    /// </summary>
    public class PlayerScopeBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly ICurrentPlayer _currentPlayer;
        private readonly ILogger<PlayerScopeBehavior<TRequest, TResponse>> _logger;

        public PlayerScopeBehavior(ICurrentPlayer currentPlayer, ILogger<PlayerScopeBehavior<TRequest, TResponse>> logger)
        {
            _currentPlayer = currentPlayer;
            _logger = logger;
        }

        public Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            if (request is not IPlayerScopedRequest scoped)
                return next(cancellationToken);

            var requestName = typeof(TRequest).Name;
            var currentPlayerId = _currentPlayer.PlayerId;

            // Fail-closed: немає ідентичності — немає доступу.
            // Фонові джоби player-scoped команд не надсилають; якщо колись знадобиться —
            // це має бути явна імперсонація, а не мовчазний обхід перевірки.
            if (currentPlayerId is null)
            {
                _logger.LogWarning("Rejected {RequestName}: player-scoped request without an authenticated player.", requestName);

                throw new UnauthorizedAccessException("This operation requires an authenticated player.");
            }

            if (currentPlayerId.Value != scoped.PlayerId)
            {
                // Логуємо як подію безпеки: це або баг клієнта, або спроба перебору чужих id
                _logger.LogWarning("IDOR attempt blocked: player {ActorId} targeted {TargetId} via {RequestName}.", currentPlayerId.Value, scoped.PlayerId, requestName);
                throw new UnauthorizedAccessException("Request targets another player's data.");
            }

            return next(cancellationToken);
        }
    }
}
