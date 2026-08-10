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
    public class IdempotencyBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly IIdempotencyRepository _repository;
        private readonly ICurrentPlayer _currentPlayer;
        private readonly IRequestContext _requestContext;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<IdempotencyBehavior<TRequest, TResponse>> _logger;

        public IdempotencyBehavior( IIdempotencyRepository repository, ICurrentPlayer currentPlayer,
            IRequestContext requestContext, IUnitOfWork unitOfWork, ILogger<IdempotencyBehavior<TRequest, TResponse>> logger)
        {
            _repository = repository;
            _currentPlayer = currentPlayer;
            _requestContext = requestContext;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            if (request is not IIdempotentRequest || _requestContext.IdempotencyKey is not { } key|| _currentPlayer.PlayerId is not { } playerId)
            {
                return await next(cancellationToken);
            }

            var requestType = typeof(TRequest).Name;

            var existing = await _repository.FindAsync(playerId, key, cancellationToken);
            if (existing is not null)
                return Replay(existing, key, requestType);

            var record = new IdempotencyRecord(
                Guid.NewGuid(), key, playerId, requestType, responseJson: null, DateTime.UtcNow);

            // Резерв ДО виконання: унікальний індекс вирішує гонку, а не наша перевірка вище
            if (!await _repository.TryReserveAsync(record, cancellationToken))
            {
                var winner = await _repository.FindAsync(playerId, key, cancellationToken)
                    ?? throw new InvalidOperationException(
                        $"Idempotency key '{key}' is reserved but its record is missing.");

                return Replay(winner, key, requestType);
            }

            try
            {
                var response = await next(cancellationToken);

                record.SetResponse(response is null ? null : JsonSerializer.Serialize(response));
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return response;
            }
            catch
            {
                // Операція впала — знімаємо резерв, інакше клієнт не зміг би ретраїти тим самим ключем
                await _repository.ReleaseAsync(record.Id, CancellationToken.None);
                throw;
            }
        }

        private TResponse Replay(IdempotencyRecord record, string key, string requestType)
        {
            if (record.RequestType != requestType)
                throw new InvalidOperationException(
                    $"Idempotency key '{key}' was already used for a different operation.");

            // Резерв є, відповіді ще немає — операція виконується прямо зараз
            if (record.ResponseJson is null && record.CreatedAt > DateTime.UtcNow.AddMinutes(-1))
                throw new InvalidOperationException(
                    $"Operation with idempotency key '{key}' is still in progress.");

            _logger.LogInformation("Idempotent replay of {RequestType} for player {PlayerId}",
                requestType, record.PlayerId);

            return record.ResponseJson is null
                ? default!
                : JsonSerializer.Deserialize<TResponse>(record.ResponseJson)!;
        }
    }
}
