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
    ///
    /// Тут же фіксується присутність гравця: це єдина точка, через яку
    /// проходить кожна його дія, і решті коду про це знати не треба.
    /// </summary>
    public class PlayerScopeBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        /// <summary>
        /// Рідше за цей поріг присутність не переписується: інакше кожен
        /// запит гравця коштував би зайвого UPDATE.
        /// </summary>
        private static readonly TimeSpan PresenceThreshold = TimeSpan.FromMinutes(30);

        private readonly ICurrentPlayer _currentPlayer;
        private readonly IPlayerRepository _playerRepository;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<PlayerScopeBehavior<TRequest, TResponse>> _logger;

        public PlayerScopeBehavior(
            ICurrentPlayer currentPlayer,
            IPlayerRepository playerRepository,
            TimeProvider timeProvider,
            ILogger<PlayerScopeBehavior<TRequest, TResponse>> logger)
        {
            _currentPlayer = currentPlayer;
            _playerRepository = playerRepository;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            if (request is not IPlayerScopedRequest scoped)
                return await next(cancellationToken);

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

            var response = await next(cancellationToken);

            // Після хендлера: невдалий запит присутністю не рахується,
            // а UPDATE поза транзакцією команди не заважає її відкату
            await TouchPresenceAsync(scoped.PlayerId, cancellationToken);

            return response;
        }

        private async Task TouchPresenceAsync(Guid playerId, CancellationToken cancellationToken)
        {
            try
            {
                var now = _timeProvider.GetUtcNow().UtcDateTime;

                await _playerRepository.TouchLastSeenAsync(playerId, now, PresenceThreshold, cancellationToken);
            }
            catch (Exception ex)
            {
                // Присутність — телеметрія, а не результат запиту: гравець
                // не має бачити помилку через невдалий службовий UPDATE
                _logger.LogWarning(ex, "Failed to record presence for player {PlayerId}.", playerId);
            }
        }
    }
}
