using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Effects.Commands
{
    /// <summary>
    /// Прибирає ефекти, що скінчились понад Retention.ExpiredEffectHours тому. Лишається bulk-операцією:
    /// ExecuteDelete не вантажить агрегатів, конфліктувати паралелізмом нема з чим.
    /// Запас потрібен бусту виробництва: його вікно читає наступний збір, і без рядка
    /// виробіток під бустом за останній проміжок порахувався б без нього.
    /// </summary>
    public record RemoveExpiredEffectsCommand : IRequest;

    public sealed class RemoveExpiredEffectsCommandHandler : IRequestHandler<RemoveExpiredEffectsCommand>
    {
        private readonly IActiveEffectRepository _effectRepository;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<RemoveExpiredEffectsCommandHandler> _logger;

        public RemoveExpiredEffectsCommandHandler(
            IActiveEffectRepository effectRepository,
            GameCatalog catalog,
            TimeProvider timeProvider,
            ILogger<RemoveExpiredEffectsCommandHandler> logger)
        {
            _effectRepository = effectRepository;
            _catalog = catalog;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(RemoveExpiredEffectsCommand request, CancellationToken cancellationToken)
        {
            var before = _timeProvider.GetUtcNow().UtcDateTime.AddHours(-_catalog.Config.Retention.ExpiredEffectHours);

            var removed = await _effectRepository.RemoveExpiredAsync(before, cancellationToken);

            if (removed > 0)
                _logger.LogInformation("Removed {Count} expired effects", removed);
        }
    }
}
