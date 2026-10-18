using EmpireIdle.Domain.Battles;
using EmpireIdle.Domain.Dungeons;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Tower
{
    /// <summary>Герой-тінь на доріжці поверху.</summary>
    public sealed record TowerEnemy(int Lane, HeroConfig Hero, bool Boss);

    /// <summary>
    /// Поверх, зібраний із номера: склад, рівні й сила ворога. Той самий номер завжди дає той самий
    /// поверх — гравець бачить ворога до бою, і лише тоді контргра складом має сенс.
    /// </summary>
    /// <param name="StatMultiplier">Множник статів ворога разом із набором.</param>
    public sealed record TowerFloor(int Number, TowerArchetypeConfig Archetype, bool IsBoss, int HeroLevel, int UnitLevel,
        int SkillLevel, double StatMultiplier, TowerSetStepConfig SetStep, IReadOnlyList<TowerEnemy> Enemies);

    /// <summary>
    /// Будує поверхи «Тисячної вежі» з номера (дизайн §8.3): опорні точки кривої дають рівні й множник,
    /// ступені — набір на ворогах, ротація — склад, а шаблони героїв беруться з heroes.json за рідкістю
    /// поверху. Вороги — герої з базовими статами рівня, без спорядження, з повними конвоями.
    /// </summary>
    public sealed class TowerFloors
    {
        /// <summary>Крок ротації складів: сусідні поверхи отримують різні склади, а не ті самі по колу.</summary>
        private const int ArchetypeStride = 7;

        private readonly GameCatalog _catalog;
        private readonly HeroProgression _progression;
        private readonly ConvoyBuilder _convoys;

        public TowerFloors(GameCatalog catalog)
        {
            _catalog = catalog;
            _progression = new HeroProgression(catalog.Config.HeroSettings);
            _convoys = new ConvoyBuilder(catalog);
        }

        private TowerConfig Config => _catalog.Config.Tower;

        /// <summary>Поверх для екрана підготовки й для бою.</summary>
        public TowerFloor Describe(int floor)
        {
            if (floor < 1 || floor > Config.MaxFloor)
                throw new ArgumentOutOfRangeException(nameof(floor), $"The tower has floors 1–{Config.MaxFloor}.");

            var (heroLevel, unitLevel, skillLevel, curve) = Curve(floor);
            var setStep = Config.SetSteps.Last(s => s.FromFloor <= floor);
            var isBoss = floor % Config.BossEvery == 0;

            var archetype = isBoss
                ? Config.Archetypes.Single(a => a.Key == Config.BossArchetype)
                : Config.Archetypes[floor * ArchetypeStride % Config.Archetypes.Count];

            var rarities = Config.HeroRarities.Last(s => s.FromFloor <= floor).Rarities;

            var enemies = archetype.Classes
                .Select((heroClass, lane) => new TowerEnemy(lane, Template(heroClass, rarities, floor, lane), isBoss && lane == Config.BossLane))
                .ToList();

            return new TowerFloor(floor, archetype, isBoss, heroLevel, unitLevel, skillLevel, curve * setStep.Multiplier, setStep, enemies);
        }

        /// <summary>Загін поверху для рушія бою: стати рівня × множник, повні конвої, відкриті вміння.</summary>
        public IReadOnlyList<SquadMember> Squad(TowerFloor floor)
        {
            var battle = _catalog.Config.RealtimeBattle;
            var caps = _catalog.Config.Dungeons;

            return floor.Enemies.Select(enemy =>
            {
                var hero = enemy.Hero;
                var stats = hero.BaseStats.Keys.ToDictionary(
                    stat => stat,
                    stat => _progression.StatValue(hero, stat, floor.HeroLevel, hero.NativeTier, hero.NativeTier, 0, 0) * floor.StatMultiplier);

                if (enemy.Boss && stats.ContainsKey("Health"))
                    stats["Health"] *= Config.BossHealthScale;

                var convoys = _convoys.BuildFull(new ConvoyLeader(hero.Class, floor.HeroLevel), floor.UnitLevel)
                    .Select(c => c with { Led = Scale(c.Led, floor.StatMultiplier), Leaderless = Scale(c.Leaderless, floor.StatMultiplier) })
                    .ToList();

                return new SquadMember(
                    enemy.Lane,
                    hero.Class,
                    _catalog.Config.HeroSettings.RoleUnits.GetValueOrDefault(hero.Class),
                    HeroFighterStats.From(stats, hero.Class, battle, caps),
                    convoys,
                    Skills(hero, floor.HeroLevel, floor.SkillLevel));
            }).ToList();
        }

        /// <summary>
        /// Рівні й множник поверху між двома опорними точками: рівні — лінійно, множник — геометрично,
        /// щоб кожен поверх був сильнішим за попередній на ту саму частку.
        /// </summary>
        private (int HeroLevel, int UnitLevel, int SkillLevel, double Multiplier) Curve(int floor)
        {
            var anchors = Config.Anchors;
            var upper = anchors.First(a => a.Floor >= floor);
            var lower = anchors.Last(a => a.Floor <= floor);

            if (upper.Floor == lower.Floor)
                return (lower.HeroLevel, lower.UnitLevel, lower.SkillLevel, lower.StatMultiplier);

            var t = (floor - lower.Floor) / (double)(upper.Floor - lower.Floor);

            return (
                Lerp(lower.HeroLevel, upper.HeroLevel, t),
                Lerp(lower.UnitLevel, upper.UnitLevel, t),
                Lerp(lower.SkillLevel, upper.SkillLevel, t),
                lower.StatMultiplier * Math.Pow(upper.StatMultiplier / lower.StatMultiplier, t));
        }

        private static int Lerp(int from, int to, double t) => (int)Math.Round(from + (to - from) * t, MidpointRounding.AwayFromZero);

        /// <summary>Шаблон героя класу: сталий для поверху й доріжки, але різний від поверху до поверху.</summary>
        private HeroConfig Template(string heroClass, List<Rarity> rarities, int floor, int lane)
        {
            var templates = _catalog.Config.Heroes
                .Where(h => h.Class == heroClass && rarities.Contains(h.Rank))
                .OrderBy(h => h.Key, StringComparer.Ordinal)
                .ToList();

            return templates[(floor * 31 + lane * 7) % templates.Count];
        }

        /// <summary>Вміння тіні — ті самі, що й у героя гравця, відкриті рівнем, на рівні вмінь поверху.</summary>
        private static List<CombatSkill> Skills(HeroConfig hero, int heroLevel, int skillLevel)
            => hero.Skills
                .Where(s => s.Battle is not null && s.Kind is SkillKind.Active or SkillKind.Periodic && s.UnlockLevel <= heroLevel)
                .Select(s => CombatSkill.Prepare(s.Key, s.Kind, s.Battle!, HeroSkills.At(s.Battle!.LevelScale, skillLevel)))
                .ToList();

        private static FighterStats Scale(FighterStats stats, double multiplier)
            => stats with { Attack = stats.Attack * multiplier, Defense = stats.Defense * multiplier, MaxHealth = stats.MaxHealth * multiplier };
    }
}
