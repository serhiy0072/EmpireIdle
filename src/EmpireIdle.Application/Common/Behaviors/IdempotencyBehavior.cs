using System.Text.Json;
using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Common.Behaviors
{
    /// <summary>
    /// Виконує запит не більше одного разу на ключ ідемпотентності:
    /// повтор повертає збережений результат замість нової операції.
    /// </summary>
    public class IdempotencyBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest: notnull
    {
        private readonly IIdempotencyRepository _repository;
        private readonly ICurrentPlayer _currentPlayer;
        private readonly IRequestContext _requestContext;
        private readonly ILogger<IdempotencyBehavior<TRequest, TResponse>> _logger;

        public IdempotencyBehavior(IIdempotencyRepository repository, ICurrentPlayer currentPlayer, IRequestContext requestContext, ILogger<IdempotencyBehavior<TRequest, TResponse>> logger)
        {
            _repository = repository;
            _currentPlayer = currentPlayer;
            _requestContext = requestContext;
            _logger = logger;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            if (request is not IIdempotentRequest || _requestContext.IdempotencyKey is not { } key || _currentPlayer.PlayerId is not { } playerId)
                return await next(cancellationToken);

            var requestType = typeof(TRequest).Name;

            var existing = await _repository.FindAsync(playerId, key, cancellationToken);
            if (existing is not null)
            {
                if(existing.RequestType != requestType)
                    throw new InvalidOperationException($"Idempotency key '{key}' was already used for a different operation.");

                _logger.LogInformation("Idempotent replay of {RequestType} for player {PlayerId}", requestType, playerId);
                
                return existing.ResponseJson is null ? default! : JsonSerializer.Deserialize<TResponse>(existing.ResponseJson)!;
            }

            var response = await next(cancellationToken;

            // Запис іде в ту саму транзакцію, що й сама операція:
            // або збережеться все разом, або нічого
            var json = response is null ? null : JsonSerializer.Serialize(response);

            await _repository.AddAsync(new IdempotencyRecord(Guid.NewGuid(), key, playerId, requestType, json, DateTime.UtcNow), cancellationToken);
            return response;
        }
    }
}
