using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Marches.Queries
{
    /// <summary>
    /// Оцінка бою до відправки армії. Склад армії передається явно:
    /// гравець крутить повзунки на екрані відправки й бачить, як
    /// змінюється смуга.
    /// </summary>
    public record GetBattlePreviewQuery(
        Guid PlayerId,
        MarchTargetType TargetType,
        Guid TargetId,
        Dictionary<string, int> Units) : IRequest<BattlePreviewResult>, IPlayerScopedRequest;

    public sealed class GetBattlePreviewQueryHandler : IRequestHandler<GetBattlePreviewQuery, BattlePreviewResult>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IMonsterRepository _monsterRepository;
        private readonly IServerContext _serverContext;
        private readonly GameCatalog _catalog;
        private readonly CombatCalculator _combat;
        private readonly MonsterArmyBuilder _armyBuilder;
        private readonly TerrainGenerator _terrain;
        private readonly MarchCalculator _calculator;
        private readonly EffectResolver _effectResolver;
        private readonly TimeProvider _timeProvider;

        public GetBattlePreviewQueryHandler(
            IVillageRepository villageRepository,
            IGarrisonRepository garrisonRepository,
            IMonsterRepository monsterRepository,
            IServerContext serverContext,
            GameCatalog catalog,
            CombatCalculator combat,
            MonsterArmyBuilder armyBuilder,
            TerrainGenerator terrain,
            MarchCalculator calculator,
            EffectResolver effectResolver,
            TimeProvider timeProvider)
        {
            _villageRepository = villageRepository;
            _garrisonRepository = garrisonRepository;
            _monsterRepository = monsterRepository;
            _serverContext = serverContext;
            _catalog = catalog;
            _combat = combat;
            _armyBuilder = armyBuilder;
            _terrain = terrain;
            _calculator = calculator;
            _effectResolver = effectResolver;
            _timeProvider = timeProvider;
        }

        public async Task<BattlePreviewResult> Handle(GetBattlePreviewQuery request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new EntityNotFoundException("Village for player", request.PlayerId);

            var garrison = await _garrisonRepository.GetByVillageIdAsync(village.Id, cancellationToken)
                ?? throw new EntityNotFoundException("Garrison for village", village.Id);

            // Прев'ю не обіцяє того, чого гравець відправити не може:
            // рахуємо по фактично доступних юнітах, а не по запиту
            var available = garrison.Units.ToDictionary(u => u.UnitType, u => u.Count);

            var attackerArmy = request.Units
                .Where(u => u.Value > 0)
                .ToDictionary(u => u.Key, u => Math.Min(u.Value, available.GetValueOrDefault(u.Key)));

            var (targetX, targetY, targetName, defenderArmy, defenderVillage) =
                await ResolveTargetAsync(request, cancellationToken);

            var terrain = _terrain.GetTerrainType(_serverContext.ServerId, targetX, targetY);

            var attackerBonus = await _effectResolver.GetMultiplierAsync(
                request.PlayerId, EffectTarget.Attack, now, cancellationToken);

            var defenderBonus = defenderVillage?.DefenceMultiplier(_catalog.Buildings) ?? 1.0;

            // Та сама формула, що й у бою — інакше прев'ю розійдеться з результатом
            var attackerPower = _combat.CalculatePower(request.Units, terrain, isAttacker: true) * attackerBonus;
            var defenderPower = _combat.CalculatePower(defenderArmy, terrain, isAttacker: false) * defenderBonus;

            var travelTime = _calculator.CalculateDuration(
                _serverContext.ServerId, village.X, village.Y, targetX, targetY, request.Units);

            return new BattlePreviewResult(
                _combat.EstimateOdds(attackerPower, defenderPower),
                targetName, targetX, targetY, terrain, travelTime);
        }

        /// <summary>
        /// Резолвить ціль так само, як SendMarchCommand: прев'ю має падати
        /// на тому самому, на чому впала б відправка.
        /// </summary>
        private async Task<(int X, int Y, string Name, Dictionary<string, int> Army, Village? Village)>
            ResolveTargetAsync(GetBattlePreviewQuery request, CancellationToken cancellationToken)
        {
            switch (request.TargetType)
            {
                case MarchTargetType.Monster:
                    var monster = await _monsterRepository.GetByIdAsync(request.TargetId, cancellationToken)
                        ?? throw new EntityNotFoundException("Monster", request.TargetId);

                    return (monster.X, monster.Y,
                        $"{monster.Type} (lvl {monster.Level})",
                        _armyBuilder.BuildArmy(monster.Type, monster.Level),
                        null);

                case MarchTargetType.Village:
                    var target = await _villageRepository.GetByIdAsync(request.TargetId, cancellationToken)
                        ?? throw new EntityNotFoundException("Village", request.TargetId);

                    var targetGarrison = await _garrisonRepository.GetByVillageIdAsync(target.Id, cancellationToken);

                    return (target.X, target.Y, target.Name,
                        targetGarrison?.Units.ToDictionary(u => u.UnitType, u => u.Count)
                        ?? new Dictionary<string, int>(),
                        target);

                default:
                    throw new RequirementNotMetException($"Unsupported target type '{request.TargetType}'.");
            }
        }
    }
}
