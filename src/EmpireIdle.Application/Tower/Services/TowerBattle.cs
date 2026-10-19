using EmpireIdle.Application.Battles.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Battles;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Tower;

namespace EmpireIdle.Application.Tower.Services
{
    /// <summary>
    /// Бій на поверсі вежі — спільний для штурму й перегляду ручного бою: та сама перевірка доступу,
    /// той самий загін і сід, тож перегляд із тими ж наказами дає рівно той бій, який потім зарахує штурм.
    /// </summary>
    public sealed class TowerBattle
    {
        private readonly IVillageRepository _villages;
        private readonly BattleSquadFactory _squads;
        private readonly TowerFloors _floors;
        private readonly RealtimeBattleEngine _engine;
        private readonly GameCatalog _catalog;

        public TowerBattle(IVillageRepository villages, BattleSquadFactory squads, TowerFloors floors, RealtimeBattleEngine engine,
            GameCatalog catalog)
        {
            _villages = villages;
            _squads = squads;
            _floors = floors;
            _engine = engine;
            _catalog = catalog;
        }

        /// <summary>Вежа відкривається з ратушею RequiresMainBuildingLevel.</summary>
        public async Task EnsureUnlockedAsync(Guid playerId, CancellationToken cancellationToken)
        {
            var settings = _catalog.Config.Tower;

            var village = await _villages.GetByPlayerIdReadOnlyAsync(playerId, cancellationToken)
                ?? throw new EntityNotFoundException("Village for player", playerId);

            var mainLevel = village.Buildings.FirstOrDefault(b => b.Type == _catalog.MainBuildingKey)?.Level.Value ?? 0;

            if (mainLevel < settings.RequiresMainBuildingLevel)
                throw new RequirementNotMetException(RefusalReasons.TowerTownHallRequired,
                    $"The tower opens at town hall {settings.RequiresMainBuildingLevel}.", settings.RequiresMainBuildingLevel);
        }

        /// <param name="casts">Накази ручного бою; null — авто.</param>
        public async Task<BattleResult> FightAsync(Guid playerId, int floor, IReadOnlyList<Guid?> lanes, IReadOnlyList<ManualCast>? casts,
            DateTime utcNow, CancellationToken cancellationToken)
        {
            var squad = await _squads.BuildAsync(playerId, lanes, withBuffs: true, utcNow, cancellationToken);
            var enemy = _floors.Squad(_floors.Describe(floor));

            return _engine.Run(new BattleSetup(squad, enemy, casts), TowerSeed.For(playerId, floor));
        }
    }
}
