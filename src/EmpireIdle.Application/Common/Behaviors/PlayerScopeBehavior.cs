using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using MediatR;

namespace EmpireIdle.Application.Common.Behaviors
{
    /// <summary>
    /// Захист від доступу до чужих даних: якщо запит несе PlayerId,
    /// він має збігатися з гравцем у токені.
    /// </summary>
    public class PlayerScopeBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly ICurrentPlayer _currentPlayer;

        public PlayerScopeBehavior(ICurrentPlayer currentPlayer)
        {
            _currentPlayer = currentPlayer;
        }

        public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            if (request is IPlayerScopedRequest scoped)
            {
                var currentPlayerId = _currentPlayer.PlayerId;

                // null = фоновий джоб (тік, сканер) — там HTTP-контексту немає, перевіряти нічого
                if (currentPlayerId is not null && currentPlayerId != scoped.PlayerId)
                    throw new UnauthorizedAccessException("Request targets another player's data.");
            }

            return next();
        }
    }
}
