using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;

namespace EmpireIdle.Application.Speedups.Services
{
    /// <summary>
    /// Таймер гравця, який можна прискорити: коли він скінчиться і як зрізати з нього час.
    /// Apply сам доводить до кінця те, що після зрізання вже дозріло, — гравець платив за «зараз».
    /// </summary>
    public sealed record SpeedUpTarget(DateTime CompletesAt, Action<TimeSpan, DateTime> Apply);

    /// <summary>
    /// Знаходить таймер гравця за видом і id. Шукає лише у власному селі й гарнізоні гравця —
    /// так чужий таймер не прискорити, а чужий id не відрізнити від неіснуючого.
    /// </summary>
    public sealed class SpeedUpTargets
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IMarchRepository _marchRepository;
        private readonly GameCatalog _catalog;

        public SpeedUpTargets(IVillageRepository villageRepository, IGarrisonRepository garrisonRepository,
            IMarchRepository marchRepository, GameCatalog catalog)
        {
            _villageRepository = villageRepository;
            _garrisonRepository = garrisonRepository;
            _marchRepository = marchRepository;
            _catalog = catalog;
        }

        public async Task<SpeedUpTarget> FindAsync(Guid playerId, SpeedUpTimer timer, Guid targetId,
            CancellationToken cancellationToken)
        {
            var village = await _villageRepository.GetByPlayerIdAsync(playerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {playerId}.");

            if (timer == SpeedUpTimer.Construction)
            {
                var building = village.Buildings.FirstOrDefault(b => b.Id == targetId)
                    ?? throw new EntityNotFoundException("Building", targetId);

                if (!building.IsUnderConstruction)
                    throw new InvalidStateException(RefusalReasons.BuildingAlreadyCompleted,
                        $"Building {targetId} is not under construction.");

                return new SpeedUpTarget(building.ConstructionCompletesAt!.Value, (cut, now) =>
                {
                    building.ReduceConstructionTime(cut);
                    village.CompleteDueConstructions(now, _catalog.Buildings);
                });
            }

            var garrison = await _garrisonRepository.GetByVillageIdAsync(village.Id, cancellationToken)
                ?? throw new InvalidOperationException($"Garrison not found for village {village.Id}.");

            switch (timer)
            {
                case SpeedUpTimer.Training:
                {
                    var order = garrison.TrainingOrders.FirstOrDefault(o => o.Id == targetId)
                        ?? throw new EntityNotFoundException("Training order", targetId);

                    return new SpeedUpTarget(order.CompletesAt, (cut, now) =>
                    {
                        garrison.ReduceTrainingTime(order.Id, cut, now);
                        garrison.CompleteDueTraining(now);
                    });
                }

                case SpeedUpTimer.UnitLevelUp:
                {
                    var order = garrison.LevelUpOrders.FirstOrDefault(o => o.Id == targetId)
                        ?? throw new EntityNotFoundException("Level-up order", targetId);

                    return new SpeedUpTarget(order.CompletesAt, (cut, now) =>
                    {
                        garrison.ReduceLevelUpTime(order.Id, cut, now);
                        garrison.CompleteDueLevelUps(now);
                    });
                }

                case SpeedUpTimer.March:
                {
                    // Марш завершує сканер: бій чи повернення — подія на мапі, а не наслідок покупки
                    var march = (await _marchRepository.GetActiveByGarrisonAsync(garrison.Id, cancellationToken))
                        .FirstOrDefault(m => m.Id == targetId)
                        ?? throw new EntityNotFoundException("Active march", targetId);

                    return new SpeedUpTarget(march.ArrivesAt, (cut, now) => march.ReduceTravelTime(cut, now));
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(timer), timer, "Unknown speed-up timer.");
            }
        }
    }
}
