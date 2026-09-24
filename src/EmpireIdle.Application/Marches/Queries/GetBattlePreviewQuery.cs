using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.Services;
using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;
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
        Guid HeroId,
        Dictionary<UnitStackKey, int> Units) : IRequest<BattlePreviewResult>, IPlayerScopedRequest;

    public sealed class GetBattlePreviewQueryHandler : IRequestHandler<GetBattlePreviewQuery, BattlePreviewResult>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IHeroRepository _heroRepository;
        private readonly IServerContext _serverContext;
        private readonly CombatCalculator _combat;
        private readonly TerrainGenerator _terrain;
        private readonly MarchCalculator _calculator;
        private readonly EffectResolver _effectResolver;
        private readonly TimeProvider _timeProvider;
        private readonly MarchTargetResolver _targets;
        private readonly HeroCombatModifiers _heroModifiers;
        private readonly GameCatalog _catalog;
        private readonly HeroProgression _progression;

        public GetBattlePreviewQueryHandler(
            IVillageRepository villageRepository,
            IGarrisonRepository garrisonRepository,
            IHeroRepository heroRepository,
            IServerContext serverContext,
            CombatCalculator combat,
            TerrainGenerator terrain,
            MarchCalculator calculator,
            EffectResolver effectResolver,
            TimeProvider timeProvider,
            MarchTargetResolver targets,
            HeroCombatModifiers heroModifiers,
            GameCatalog catalog,
            HeroProgression progression)
        {
            _villageRepository = villageRepository;
            _garrisonRepository = garrisonRepository;
            _heroRepository = heroRepository;
            _serverContext = serverContext;
            _combat = combat;
            _terrain = terrain;
            _calculator = calculator;
            _effectResolver = effectResolver;
            _timeProvider = timeProvider;
            _targets = targets;
            _heroModifiers = heroModifiers;
            _catalog = catalog;
            _progression = progression;
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
            var available = garrison.Units.ToDictionary(u => new UnitStackKey(u.UnitType, u.Level), u => u.Count);

            var attackerArmy = request.Units
                .Where(u => u.Value > 0)
                .ToDictionary(u => u.Key, u => Math.Min(u.Value, available.GetValueOrDefault(u.Key)));

            var target = await _targets.ResolveAsync(request.TargetType, request.TargetId, village, now, cancellationToken);

            // Те саме, що відмовить у відправленні: прев'ю не має
            // показувати шанси там, куди йти не можна
            _targets.EnsureAttackAllowed(village, target, now);

            var terrain = _terrain.GetTerrainType(_serverContext.ServerId, target.X, target.Y);

            var attackerBonus = await _effectResolver.GetMultiplierAsync(
                request.PlayerId, EffectTarget.Attack, now, cancellationToken);

            // Володіння героєм тут не перевіряється навмисно: прев'ю нічого
            // не міняє, а чужий або неіснуючий HeroId просто дасть null
            // і порахується без пасивок. Відмовить SendMarchCommand
            var attackerHero = await _heroRepository.GetByIdAsync(request.HeroId, cancellationToken);

            // Та сама формула, що й у бою — інакше прев'ю розійдеться з результатом.
            // Герой теж той самий, якого гравець збирається відправити: без його
            // пасивок прев'ю занижувало б силу рівно на їхню величину
            var attackerPower = _combat.CalculatePower(attackerArmy, terrain, isAttacker: true,
                _heroModifiers.For(attackerHero)) * attackerBonus;

            var defenderPower = _combat.CalculateDefencePower(target.Defence, terrain, target.DefenceBuffs)
                * target.DefenceMultiplier;

            // Час у дорозі теж із героєм: повільний герой гальмує колону,
            // і прев'ю мусить показувати той самий час, що потім і буде
            var travelTime = _calculator.CalculateDuration(
                _serverContext.ServerId, village.X, village.Y, target.X, target.Y, attackerArmy,
                attackerHero is null
                    ? null
                    : _progression.MarchSpeed(_catalog.FindHero(attackerHero.HeroKey)));

            return new BattlePreviewResult(
                _combat.EstimateOdds(attackerPower, defenderPower),
                target.Name, target.X, target.Y, terrain, travelTime);
        }
    }
}
