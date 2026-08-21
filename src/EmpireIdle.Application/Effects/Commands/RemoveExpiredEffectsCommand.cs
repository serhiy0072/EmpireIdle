using EmpireIdle.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Effects.Commands
{
    /// <summary>
    /// Прибирає прострочені ефекти. Лишається bulk-операцією: ExecuteDelete
    /// не вантажить агрегатів, конфліктувати паралелізмом нема з чим.
    /// </summary>
    public record RemoveExpiredEffectsCommand : IRequest;

    public sealed class RemoveExpiredEffectsCommandHandler : IRequestHandler<RemoveExpiredEffectsCommand>
    {
        private readonly IActiveEffectRepository _effectRepository;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<RemoveExpiredEffectsCommandHandler> _logger;

        public RemoveExpiredEffectsCommandHandler(
            IActiveEffectRepository effectRepository,
            TimeProvider timeProvider,
            ILogger<RemoveExpiredEffectsCommandHandler> logger)
        {
            _effectRepository = effectRepository;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(RemoveExpiredEffectsCommand request, CancellationToken cancellationToken)
        {
            var removed = await _effectRepository.RemoveExpiredAsync(
                _timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

            if (removed > 0)
                _logger.LogInformation("Removed {Count} expired effects", removed);
        }
    }
}
