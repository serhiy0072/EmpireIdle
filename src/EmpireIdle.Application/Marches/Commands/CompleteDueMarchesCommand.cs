using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Marches.Commands
{
    /// <summary>Обробляє походи, чий час прибуття настав.</summary>
    public record CompleteDueMarchesCommand : IRequest;

    public class CompleteDueMarchesCommandHandler : IRequestHandler<CompleteDueMarchesCommand>
    {
        private const int ServerId = 1;

        private readonly IMarchRepository _marchRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapRepository _mapRepository;
        private readonly IMonsterRepository _monsterRepository;
        private readonly MonsterArmyBuilder _armyBuilder;
        private readonly CombatCalculator _combat;
        private readonly TerrainGenerator _terrain;
        private readonly MarchCalculator _calculator;
        private readonly ILogger<CompleteDueMarchesCommandHandler> _logger;

        public CompleteDueMarchesCommandHandler(
            IMarchRepository marchRepository,
            IGarrisonRepository garrisonRepository,
            IUnitOfWork unitOfWork,
            IMapRepository mapRepository,
            IMonsterRepository monsterRepository,   
            MonsterArmyBuilder armyBuilder,
            CombatCalculator combat,
            TerrainGenerator terrain,
            MarchCalculator calculator,
            ILogger<CompleteDueMarchesCommandHandler> logger)
        {
            _marchRepository = marchRepository;
            _garrisonRepository = garrisonRepository;
            _unitOfWork = unitOfWork;
            _mapRepository = mapRepository;
            _monsterRepository = monsterRepository;
            _armyBuilder = armyBuilder;
            _combat = combat;
            _terrain = terrain;
            _calculator = calculator;
            _logger = logger;
        }

        public async Task Handle(CompleteDueMarchesCommand request, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var due = await _marchRepository.GetDueAsync(now, cancellationToken);

            if (due.Count == 0)
                return;

            var battles = 0;
            var returned = 0;

            foreach (var march in due)
            {
                if (march.State == MarchState.Outbound)
                {
                    await ResolveBattleAsync(march, cancellationToken);
                    battles++;
                }
                else if (march.State == MarchState.Returning)
                {
                    var garrison = await _garrisonRepository.GetByIdAsync(march.GarrisonId, cancellationToken);
                    if (garrison is null)
                    {
                        _logger.LogWarning("Garrison {GarrisonId} not found for march {MarchId}", march.GarrisonId, march.Id);
                        continue;
                    }

                    var survivors = march.GetUnits();
                    if(survivors.Count > 0)
                        garrison.ReceiveUnits(survivors);

                    march.Complete();
                    returned++;
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Marches processed: {Arrived} arrived, {Returned} returned home", battles, returned);
        }

        /// <summary>Проводить бій на місці прибуття армії.</summary>
        private async Task ResolveBattleAsync(March march, CancellationToken cancellationToken)
        {
            var attackerArmy = march.GetUnits();
            var terrain = _terrain.GetTerrainType(ServerId, march.TargetX, march.TargetY);

            if(march.TargetType != MarchTargetType.Monster)
            {
                // PvP — окрема фаза; поки армія просто розвертається
                TurnMarchBack(march, attackerArmy);
                return;
            }

            var monster = await _monsterRepository.GetByIdAsync(march.TargetId, cancellationToken);
            if (monster is null)
            {
                // Ціль уже вбита кимось іншим — повертаємось без бою
                TurnMarchBack(march, attackerArmy);
                return;
            }

            var defenderArmy = _armyBuilder.BuildArmy(monster.Type, monster.Level);
            var result = _combat.Resolve(attackerArmy, defenderArmy, terrain);

            march.ApplyLosses(result.AttackerLosses);

            if (result.AttackerWon)
            {
                // Монстр знищений — прибираємо з карти
                _monsterRepository.Remove(monster);

                var cell = await _mapRepository.GetByOccupantAsync(MapOccupantType.Monster, monster.Id, cancellationToken);
                if (cell is not null)
                    _mapRepository.Remove(cell);

                // TODO (наступний крок): нагороди в село
            }

            _logger.LogInformation(
               "Battle at ({X},{Y}) on {Terrain}: attacker {Outcome} ({AttackerPower:F0} vs {DefenderPower:F0})",
               march.TargetX, march.TargetY, terrain,
               result.AttackerWon ? "won" : "lost",
               result.AttackerPower, result.DefenderPower);

            TurnMarchBack(march, march.GetUnits());
        }

        /// <summary>Розвертає похід додому (або завершує, якщо армія загинула).</summary>
        private void TurnMarchBack(March march, IReadOnlyDictionary<string, int> survivors)
        {
            if (survivors.Count == 0 || survivors.Values.All(c => c <= 0))
            {
                // Уся армія загинула — повертатись нікому
                march.TurnBack(TimeSpan.Zero);
                march.Complete();
                return;
            }

            var backDuration = _calculator.CalculateDuration(
                ServerId, march.TargetX, march.TargetY, march.OriginX, march.OriginY, survivors);

            march.TurnBack(backDuration);
        }
    }
}