using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EmpireIdle.Application.Common.Behaviors
{
    /// <summary>
    /// Виконує запит не більше одного разу на ключ ідемпотентності:
    /// повтор повертає збережений результат замість нової операції.
    /// </summary>
    public sealed class IdempotencyBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly IIdempotencyRepository _repository;
        private readonly ICurrentPlayer _currentPlayer;
        private readonly IRequestContext _requestContext;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<IdempotencyBehavior<TRequest, TResponse>> _logger;

        private static readonly Regex IdempotencyKeyPattern =
            new(@"^[A-Za-z0-9._-]{16,128}$", RegexOptions.Compiled);

        public IdempotencyBehavior(
            IIdempotencyRepository repository,
            ICurrentPlayer currentPlayer,
            IRequestContext requestContext,
            TimeProvider timeProvider,
            ILogger<IdempotencyBehavior<TRequest, TResponse>> logger)
        {
            _repository = repository;
            _currentPlayer = currentPlayer;
            _requestContext = requestContext;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            if (request is not IIdempotentRequest)
                return await next(cancellationToken);

            if (_currentPlayer.PlayerId is not { } playerId)
                throw new UnauthorizedAccessException("This operation requires an authenticated player.");

            // Захист, який вимикається відсутністю заголовка, — не захист
            if (_requestContext.IdempotencyKey is not { } key)
                throw new ValidationException("Idempotency-Key header is required for this operation.");

            if (!IdempotencyKeyPattern.IsMatch(key))
                throw new ValidationException("Idempotency-Key must be 16–128 chars of [A-Za-z0-9._-].");

            var requestType = typeof(TRequest).Name;

            var existing = await _repository.FindAsync(playerId, key, cancellationToken);
            if (existing is not null)
                return Replay(existing, key, requestType);

            var record = new IdempotencyRecord(
                Guid.NewGuid(), key, playerId, requestType,
                responseJson: null,
                _timeProvider.GetUtcNow().UtcDateTime);

            // Резерв ДО виконання: гонку вирішує унікальний індекс, а не перевірка вище
            if (!await _repository.TryReserveAsync(record, cancellationToken))
            {
                var winner = await _repository.FindAsync(playerId, key, cancellationToken)
                    ?? throw new InvalidOperationException(
                        $"Idempotency key '{key}' is reserved but its record is missing.");

                return Replay(winner, key, requestType);
            }

            TResponse response;

            try
            {
                response = await next(cancellationToken);
            }
            catch
            {
                // Операція впала — знімаємо резерв, інакше клієнт не зміг би ретраїти тим самим ключем
                await _repository.ReleaseAsync(record.Id, CancellationToken.None);
                throw;
            }

            // Поза catch: операція вже закомітилась. Якщо запис відповіді впаде,
            // резерв мусить лишитись — reaper прибере його через добу. Звільнення тут
            // відкрило б шлях до повторного виконання вже виконаної операції.
            //
            // CancellationToken.None з тієї ж причини: скасований запит не має
            // лишати резерв без результату.
            await _repository.CompleteAsync(
                record.Id,
                JsonSerializer.Serialize(response),
                CancellationToken.None);

            return response;
        }

        private TResponse Replay(IdempotencyRecord record, string key, string requestType)
        {
            if (record.RequestType != requestType)
                throw new InvalidOperationException(
                    $"Idempotency key '{key}' was already used for a different operation.");

            // Резерв є, відповіді ще немає — операція виконується прямо зараз і могла
            // ще впасти. Успіх тут був би брехнею навіть для команд без результату:
            // Unit.Value означає «зроблено», а воно не зроблено.
            if (record.ResponseJson is null)
                throw new InvalidOperationException(
                    $"Operation for idempotency key '{key}' is still in progress. Retry shortly.");

            _logger.LogInformation("Idempotent replay of {RequestType} for player {PlayerId}",
                requestType, record.PlayerId);

            return JsonSerializer.Deserialize<TResponse>(record.ResponseJson)!;
        }
    }
}
