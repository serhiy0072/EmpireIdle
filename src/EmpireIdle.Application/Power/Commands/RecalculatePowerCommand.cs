using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Power.Commands
{
    /// <summary>
    /// Перераховує бойову силу гравця, якому належить гарнізон.
    ///
    /// Гарнізон, а не гравець, бо всі події, що змінюють армію, несуть саме
    /// його: тренування, бій, повернення походу.
    /// </summary>
    public record RecalculatePowerCommand(Guid GarrisonId) : IRequest;

    public sealed class RecalculatePowerCommandHandler : IRequestHandler<RecalculatePowerCommand>
    {
        /// <summary>
        /// Місцевість для рейтингової сили. Нейтральна навмисно: Power порівнює
        /// гравців між собою, і бонус за терейн зробив би число залежним від
        /// того, де стоїть село, а не від того, яка в гравця армія.
        /// Прев'ю бою бере справжню місцевість окремо.
        /// </summary>
        private const string NeutralTerrain = "plain";

        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IVillageRepository _villageRepository;
        private readonly IMarchRepository _marchRepository;
        private readonly IPlayerPowerRepository _powerRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly CombatCalculator _combat;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<RecalculatePowerCommandHandler> _logger;

        public RecalculatePowerCommandHandler(
            IGarrisonRepository garrisonRepository,
            IVillageRepository villageRepository,
            IMarchRepository marchRepository,
            IPlayerPowerRepository powerRepository,
            IUnitOfWork unitOfWork,
            CombatCalculator combat,
            TimeProvider timeProvider,
            ILogger<RecalculatePowerCommandHandler> logger)
        {
            _garrisonRepository = garrisonRepository;
            _villageRepository = villageRepository;
            _marchRepository = marchRepository;
            _powerRepository = powerRepository;
            _unitOfWork = unitOfWork;
            _combat = combat;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(RecalculatePowerCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var garrison = await _garrisonRepository.GetByIdAsync(request.GarrisonId, cancellationToken);
            if (garrison is null)
                return;

            var village = await _villageRepository.GetByIdAsync(garrison.VillageId, cancellationToken);
            if (village is null)
                return;

            // Гарнізон плюс армії в поході. Марші рахуються обов'язково —
            // інакше Power падає під час атаки, і гравці тримали б військо
            // вдома заради рейтингу
            var army = garrison.Units.ToDictionary(u => u.UnitType, u => u.Count);

            var marches = await _marchRepository.GetActiveByGarrisonAsync(garrison.Id, cancellationToken);

            foreach (var march in marches)
            {
                foreach (var (unitType, count) in march.GetUnits())
                    army[unitType] = army.GetValueOrDefault(unitType) + count;
            }

            // Поранені й відновлювані не входять: вони не б'ються
            var armyPower = _combat.CalculatePower(army, NeutralTerrain, isAttacker: true);

            var power = await _powerRepository.GetByPlayerAsync(village.PlayerId, cancellationToken);

            if (power is null)
            {
                power = new PlayerPower(Guid.NewGuid(), village.PlayerId, village.ServerId, now);
                await _powerRepository.AddAsync(power, cancellationToken);
            }

            // Абсолютні значення, не дельти: герої й спорядження — фаза 24
            power.Set(armyPower, hero: 0, equipment: 0, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Power recalculated for player {PlayerId}: {Total}", village.PlayerId, power.TotalPower);
        }
    }
}
