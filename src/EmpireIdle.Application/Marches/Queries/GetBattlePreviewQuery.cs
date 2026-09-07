using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.Services;
using EmpireIdle.Domain.Enums;
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
        private readonly IServerContext _serverContext;
        private readonly GameCatalog _catalog;
        private readonly CombatCalculator _combat;
        private readonly TerrainGenerator _terrain;
        private readonly MarchCalculator _calculator;
        private readonly EffectResolver _effectResolver;
        private readonly TimeProvider _timeProvider;
        private readonly MarchTargetResolver _targets;

        public GetBattlePreviewQueryHandler(
            IVillageRepository villageRepository,
            IGarrisonRepository garrisonRepository,
            IServerContext serverContext,
            GameCatalog catalog,
            CombatCalculator combat,
            TerrainGenerator terrain,
            MarchCalculator calculator,
            EffectResolver effectResolver,
            TimeProvider timeProvider,
            MarchTargetResolver targets)
        {
            _villageRepository = villageRepository;
            _garrisonRepository = garrisonRepository;
            _serverContext = serverContext;
            _catalog = catalog;
            _combat = combat;
            _terrain = terrain;
            _calculator = calculator;
            _effectResolver = effectResolver;
            _timeProvider = timeProvider;
            _targets = targets;
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

            var target = await _targets.ResolveAsync(request.TargetType, request.TargetId, village, cancellationToken);

            // Те саме, що відмовить у відправленні: прев'ю не має
            // показувати шанси там, куди йти не можна
            _targets.EnsureAttackAllowed(village, target);

            var terrain = _terrain.GetTerrainType(_serverContext.ServerId, target.X, target.Y);

            var attackerBonus = await _effectResolver.GetMultiplierAsync(
                request.PlayerId, EffectTarget.Attack, now, cancellationToken);

            // Та сама формула, що й у бою — інакше прев'ю розійдеться з результатом.
            // Армія береться доступна, а не запитана: прев'ю не обіцяє того,
            // чого гравець відправити не може
            var attackerPower = _combat.CalculatePower(attackerArmy, terrain, isAttacker: true) * attackerBonus;
            var defenderPower = _combat.CalculatePower(target.DefenderArmy, terrain, isAttacker: false)
                * target.DefenceMultiplier;

            var travelTime = _calculator.CalculateDuration(
                _serverContext.ServerId, village.X, village.Y, target.X, target.Y, attackerArmy);

            return new BattlePreviewResult(
                _combat.EstimateOdds(attackerPower, defenderPower),
                target.Name, target.X, target.Y, terrain, travelTime);
        }
    }
}
