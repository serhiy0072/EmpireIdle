using EmpireIdle.Application.Common.Exceptions;
using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.ValueObjects;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
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
            new(@"^[A-Za-z0-9._-]{16,100}$", RegexOptions.Compiled);

        // Відбиток запиту серіалізує всю команду. Словники за UnitStackKey (марш, лікування, викуп)
        // System.Text.Json без конвертера ключа не бере — тож даємо його тут
        private static readonly JsonSerializerOptions HashOptions =
            new() { Converters = { new UnitStackKeyJsonConverter() } };

        /// <summary>Відбиток параметрів запиту; на незручному для серіалізації графі падає назад на null.</summary>
        private static string? RequestHash(TRequest request)
        {
            try
            {
                return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request, HashOptions)));
            }
            catch (NotSupportedException)
            {
                // Невідомий складний ключ словника — не привід для 500: відбиток відсутній,
                // звірка тіла деградує до звірки типу (як було до введення хешу)
                return null;
            }
        }

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
                throw new ValidationException([
                    new ValidationFailure("Idempotency-Key",
                        "Idempotency-Key header is required for this operation.")]);

            if (!IdempotencyKeyPattern.IsMatch(key))
                throw new ValidationException([
                    new ValidationFailure("Idempotency-Key",
                        "Idempotency-Key must be 16–100 chars of [A-Za-z0-9._-].")]);

            var requestType = typeof(TRequest).Name;
            var requestHash = RequestHash(request);

            var existing = await _repository.FindAsync(playerId, key, cancellationToken);
            if (existing is not null)
                return Replay(existing, key, requestType, requestHash);

            var record = new IdempotencyRecord(
                Guid.NewGuid(), key, playerId, requestType,
                responseJson: null,
                _timeProvider.GetUtcNow().UtcDateTime,
                requestHash);

            // Резерв ДО виконання: гонку вирішує унікальний індекс, а не перевірка вище
            if (!await _repository.TryReserveAsync(record, cancellationToken))
            {
                var winner = await _repository.FindAsync(playerId, key, cancellationToken)
                    ?? throw new InvalidOperationException(
                        $"Idempotency key '{key}' is reserved but its record is missing.");

                return Replay(winner, key, requestType, requestHash);
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

        private TResponse Replay(IdempotencyRecord record, string key, string requestType, string? requestHash)
        {
            // Той самий ключ з іншою операцією чи іншими параметрами — не повтор, а баг клієнта.
            // Порівнюємо лише коли обидва відбитки є: без одного з них звіряємо тип (як до введення хешу)
            if (record.RequestType != requestType
                || (record.RequestHash is not null && requestHash is not null && record.RequestHash != requestHash))
                throw new IdempotencyKeyReusedException(key);

            // Резерв є, відповіді ще немає — операція виконується прямо зараз і могла
            // ще впасти. Успіх тут був би брехнею навіть для команд без результату:
            // Unit.Value означає «зроблено», а воно не зроблено.
            if (record.ResponseJson is null)
                throw new OperationInProgressException(key);

            _logger.LogInformation("Idempotent replay of {RequestType} for player {PlayerId}",
                requestType, record.PlayerId);

            return JsonSerializer.Deserialize<TResponse>(record.ResponseJson)!;
        }

        /// <summary>Серіалізує UnitStackKey як "unitType@level" — і значенням, і ключем словника.</summary>
        private sealed class UnitStackKeyJsonConverter : JsonConverter<UnitStackKey>
        {
            public override UnitStackKey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
                => UnitStackKey.Parse(reader.GetString()!);

            public override void Write(Utf8JsonWriter writer, UnitStackKey value, JsonSerializerOptions options)
                => writer.WriteStringValue(value.ToString());

            public override UnitStackKey ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
                => UnitStackKey.Parse(reader.GetString()!);

            public override void WriteAsPropertyName(Utf8JsonWriter writer, UnitStackKey value, JsonSerializerOptions options)
                => writer.WritePropertyName(value.ToString());
        }
    }
}
