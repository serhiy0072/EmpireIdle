using EmpireIdle.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Power.Commands
{
    /// <summary>
    /// Перерахунок сили від гравця, а не від гарнізону: події героїв і
    /// спорядження знають лише власника. Обробник знаходить домашній
    /// гарнізон і передає естафету RecalculatePowerCommand — рахунок
    /// лишається в одному місці.
    /// </summary>
    public record RecalculatePlayerPowerCommand(Guid PlayerId) : IRequest;

    public sealed class RecalculatePlayerPowerCommandHandler : IRequestHandler<RecalculatePlayerPowerCommand>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IMediator _mediator;
        private readonly ILogger<RecalculatePlayerPowerCommandHandler> _logger;

        public RecalculatePlayerPowerCommandHandler(
            IVillageRepository villageRepository,
            IGarrisonRepository garrisonRepository,
            IMediator mediator,
            ILogger<RecalculatePlayerPowerCommandHandler> logger)
        {
            _villageRepository = villageRepository;
            _garrisonRepository = garrisonRepository;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task Handle(RecalculatePlayerPowerCommand request, CancellationToken cancellationToken)
        {
            var village = await _villageRepository.GetByPlayerIdReadOnlyAsync(request.PlayerId, cancellationToken);
            var garrison = village is null ? null : await _garrisonRepository.GetByVillageIdAsync(village.Id, cancellationToken);

            // Гравець без села (щойно зареєстрований) сили не має — це не помилка
            if (garrison is null)
            {
                _logger.LogDebug("Player {PlayerId} has no garrison yet — power recalculation skipped", request.PlayerId);
                return;
            }

            await _mediator.Send(new RecalculatePowerCommand(garrison.Id), cancellationToken);
        }
    }
}
