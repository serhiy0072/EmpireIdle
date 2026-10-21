using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Battles;
using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Dungeons;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Application.Battles.Services
{
    /// <summary>
    /// Загін гравця для одномоментного бою: герої з повними статами (рівень, тір, зірки, зброя, спорядження),
    /// відкриті вміння, конвої з юнітів гарнізону вдома та їхні бонуси героя. Бафи міста й звірів —
    /// лише коли режим їх дозволяє: у вежі так, на арені ні (рішення 18.10.2026).
    /// Окремо від рушія, бо рушій не ходить у репозиторії, і окремо від команди — загін потрібен і перегляду.
    /// </summary>
    public sealed class BattleSquadFactory
    {
        private readonly IHeroRepository _heroes;
        private readonly IInventoryRepository _inventory;
        private readonly IVillageRepository _villages;
        private readonly IGarrisonRepository _garrisons;
        private readonly GameCatalog _catalog;
        private readonly HeroStats _heroStats;
        private readonly HeroSkills _skills;
        private readonly HeroCombatModifiers _modifiers;
        private readonly EffectResolver _effects;
        private readonly ConvoyBuilder _convoys;

        public BattleSquadFactory(IHeroRepository heroes, IInventoryRepository inventory, IVillageRepository villages,
            IGarrisonRepository garrisons, GameCatalog catalog, HeroStats heroStats, HeroSkills skills,
            HeroCombatModifiers modifiers, EffectResolver effects)
        {
            _heroes = heroes;
            _inventory = inventory;
            _villages = villages;
            _garrisons = garrisons;
            _catalog = catalog;
            _heroStats = heroStats;
            _skills = skills;
            _modifiers = modifiers;
            _effects = effects;
            _convoys = new ConvoyBuilder(catalog);
        }

        /// <param name="lanes">Герої за доріжками: індекс — доріжка, null — порожня.</param>
        /// <param name="withBuffs">Чи діють бусти крамниці й пасивки звірів на атаку й захист.</param>
        public async Task<IReadOnlyList<SquadMember>> BuildAsync(Guid playerId, IReadOnlyList<Guid?> lanes, bool withBuffs,
            DateTime utcNow, CancellationToken cancellationToken)
        {
            var battle = _catalog.Config.RealtimeBattle;
            var ids = lanes.OfType<Guid>().ToList();

            // До цього веде лише кривий клієнт: екран загону не дає ні порожнього, ні подвійного складу
            if (ids.Count == 0 || lanes.Count > battle.SquadSize || ids.Distinct().Count() != ids.Count)
                throw new ArgumentException($"A squad needs 1–{battle.SquadSize} distinct heroes on at most {battle.SquadSize} lanes.");

            var heroes = (await _heroes.GetByIdsReadOnlyAsync(ids, cancellationToken)).ToDictionary(h => h.Id);
            var pool = await PoolAsync(playerId, cancellationToken);
            var (attack, defense) = withBuffs ? await BuffsAsync(playerId, utcNow, cancellationToken) : (1.0, 1.0);
            var members = new List<(SquadMember Member, ConvoyLeader Leader)>();

            for (var lane = 0; lane < lanes.Count; lane++)
            {
                if (lanes[lane] is not { } id)
                    continue;

                if (!heroes.TryGetValue(id, out var hero) || hero.PlayerId != playerId)
                    throw new EntityNotFoundException("Hero", id.ToString());

                var config = _catalog.FindHero(hero.HeroKey) ?? throw new EntityNotFoundException("Hero config", hero.HeroKey);

                if (hero.State == HeroState.Wounded)
                    throw new RequirementNotMetException(RefusalReasons.SquadHeroWounded,
                        $"Hero {hero.Id} is wounded and cannot fight.", config.DisplayName);

                members.Add(await MemberAsync(hero, config, lane, cancellationToken));
            }

            // Військо ділиться на весь загін разом — по конвою за коло, а не хто перший у списку
            var convoys = _convoys.Build(members.Select(m => m.Leader).ToList(), pool);

            return members
                .Select((m, i) => m.Member with
                {
                    Hero = Buff(m.Member.Hero, attack, defense),
                    Convoys = convoys[i].Select(c => c with { Led = Buff(c.Led, attack, defense), Leaderless = Buff(c.Leaderless, attack, defense) }).ToList()
                })
                .ToList();
        }

        private async Task<(SquadMember Member, ConvoyLeader Leader)> MemberAsync(Hero hero, HeroConfig config, int lane,
            CancellationToken cancellationToken)
        {
            var equipped = await _inventory.GetEquippedAsync(hero.Id, cancellationToken);
            var stats = _heroStats.Compute(hero, config, equipped);
            var unitType = _catalog.Config.HeroSettings.RoleUnits.GetValueOrDefault(config.Class);

            // Бонуси військам — ті самі, що герой дає своїм конвоям у марші: вміння й спорядження
            var buff = _modifiers.ForMarch([hero], new HeroGear(new Dictionary<Guid, IReadOnlyDictionary<string, double>> { [hero.Id] = stats }));

            var leader = new ConvoyLeader(
                config.Class,
                hero.EffectiveLevel,
                unitType is null ? 1 : buff.Attack(unitType),
                unitType is null ? 1 : buff.Defense(unitType),
                stats.GetValueOrDefault(ArtifactStats.UnitAttack),
                stats.GetValueOrDefault(ArtifactStats.UnitDefense));

            var member = new SquadMember(
                lane,
                config.Class,
                unitType,
                HeroFighterStats.From(stats, config.Class, _catalog.Config.RealtimeBattle, _catalog.Config.Dungeons),
                [],
                Skills(hero, config),
                hero.HeroKey);

            return (member, leader);
        }

        /// <summary>Юніти вдома — ті, що не в марші й не в госпіталі; бій їх не витрачає.</summary>
        private async Task<UnitPool> PoolAsync(Guid playerId, CancellationToken cancellationToken)
        {
            var village = await _villages.GetByPlayerIdReadOnlyAsync(playerId, cancellationToken);
            var garrison = village is null ? null : await _garrisons.GetByVillageIdReadOnlyAsync(village.Id, cancellationToken);

            return garrison is null
                ? UnitPool.Empty
                : new UnitPool(garrison.Units
                    .GroupBy(u => new UnitStackKey(u.UnitType, u.Level))
                    .ToDictionary(g => g.Key, g => g.Sum(u => u.Count)));
        }

        private async Task<(double Attack, double Defense)> BuffsAsync(Guid playerId, DateTime utcNow, CancellationToken cancellationToken)
            => (await _effects.GetMultiplierAsync(playerId, EffectTarget.Attack, utcNow, cancellationToken),
                await _effects.GetMultiplierAsync(playerId, EffectTarget.Defense, utcNow, cancellationToken));

        /// <summary>Бафи множать атаку й захист усіх бійців гравця — героїв і конвоїв (дизайн §8.5).</summary>
        private static FighterStats Buff(FighterStats stats, double attack, double defense)
            => stats with { Attack = stats.Attack * attack, Defense = stats.Defense * defense };

        /// <summary>Відкриті вміння з ефектом у бою — активне й періодичні, з рівнем героя.</summary>
        private List<CombatSkill> Skills(Hero hero, HeroConfig config)
            => config.Skills
                .Where(s => s.Battle is not null && s.Kind is SkillKind.Active or SkillKind.Periodic)
                .Select(s => (Skill: s, Level: _skills.LevelOf(hero, s)))
                .Where(x => x.Level > 0)
                .Select(x => CombatSkill.Prepare(x.Skill.Key, x.Skill.Kind, x.Skill.Battle!, HeroSkills.At(x.Skill.Battle!.LevelScale, x.Level)))
                .ToList();
    }
}
