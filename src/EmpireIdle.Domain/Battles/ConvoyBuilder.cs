using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Battles
{
    /// <summary>Конвой у бою: одна фігурка — до ConvoySize юнітів ролі героя (рішення 16.10.2026).</summary>
    /// <param name="Led">Стати, поки герой живий: з його бонусами військам.</param>
    /// <param name="Leaderless">Стати після загибелі героя: конвой б'ється далі, але без його бонусів.</param>
    public sealed record Convoy(string UnitType, int Units, FighterStats Led, FighterStats Leaderless);

    /// <summary>Що герой дає своїм конвоям.</summary>
    /// <param name="HeroLevel">Рівень героя з табором — від нього кількість конвоїв.</param>
    /// <param name="AttackMultiplier">Бонус військам на атаку (StackBuff.Attack свого типу): 1.2 — +20%.</param>
    /// <param name="DefenseMultiplier">Бонус військам на захист (StackBuff.Defense свого типу).</param>
    /// <param name="UnitAttack">Пласка атака військам зі спорядження (стат героя UnitAttack).</param>
    /// <param name="UnitDefense">Пласке здоров'я військам зі спорядження (стат героя UnitDefense).</param>
    public sealed record ConvoyLeader(string HeroClass, int HeroLevel,
        double AttackMultiplier = 1, double DefenseMultiplier = 1, double UnitAttack = 0, double UnitDefense = 0);

    /// <summary>
    /// Збирає конвої героя для одномоментного бою (дизайн §3.2): кількість — за рівнем героя,
    /// юніти — з пулу гарнізону, сила — з юнітів, їхнього рівня й бонусів героя військам.
    /// Неповний конвой б'ється слабше, порожній у бій не виходить.
    /// </summary>
    public sealed class ConvoyBuilder
    {
        private readonly GameCatalog _catalog;
        private readonly HeroConvoys _convoys;

        public ConvoyBuilder(GameCatalog catalog)
        {
            _catalog = catalog;
            _convoys = new HeroConvoys(catalog.Config.HeroSettings);
        }

        private RealtimeBattleConfig Battle => _catalog.Config.RealtimeBattle;

        /// <summary>
        /// Конвої загону з юнітів пулу — список на кожного героя в тому ж порядку; пул зменшується на взяте.
        /// Герої однієї ролі набирають по черзі, по конвою за коло: кожен отримує перший конвой,
        /// перш ніж хтось отримає другий, — інакше перший у загоні забирав би все військо.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<Convoy>> Build(IReadOnlyList<ConvoyLeader> leaders, UnitPool pool)
        {
            var slots = leaders.Select(Slots).ToList();
            var convoys = leaders.Select(_ => new List<Convoy>()).ToList();
            var rounds = slots.Select(s => s?.Count ?? 0).DefaultIfEmpty().Max();

            for (var round = 0; round < rounds; round++)
            {
                for (var i = 0; i < leaders.Count; i++)
                {
                    // Порожній конвой означає, що юніти скінчились: далі цей герой не набирає
                    if (slots[i] is not { } slot || round >= slot.Count || convoys[i].Count < round)
                        continue;

                    var units = pool.Take(slot.UnitType, _catalog.Config.HeroSettings.ConvoySize);

                    if (units.Count > 0)
                        convoys[i].Add(Convoy(slot.UnitType, units, leaders[i], slot.Count));
                }
            }

            return convoys;
        }

        /// <summary>
        /// Повні конвої одного рівня юнітів — так стоять вороги вежі й боти арени:
        /// у них немає гарнізону, з якого чогось могло б забракнути.
        /// </summary>
        public IReadOnlyList<Convoy> BuildFull(ConvoyLeader leader, int unitLevel)
        {
            if (Slots(leader) is not { } slots)
                return [];

            var full = new[] { (unitLevel, _catalog.Config.HeroSettings.ConvoySize) };

            return Enumerable.Range(0, slots.Count).Select(_ => Convoy(slots.UnitType, full, leader, slots.Count)).ToList();
        }

        private (string UnitType, int Count)? Slots(ConvoyLeader leader)
        {
            var unitType = _catalog.Config.HeroSettings.RoleUnits.GetValueOrDefault(leader.HeroClass);
            var count = _convoys.ConvoysAt(leader.HeroLevel);

            return unitType is null || count == 0 ? null : (unitType, count);
        }

        private Convoy Convoy(string unitType, IReadOnlyList<(int Level, int Count)> units, ConvoyLeader leader, int convoyCount)
        {
            var config = _catalog.Unit(unitType);
            var size = units.Sum(u => u.Count);

            // Рівень множить кожного юніта; захист — стрій, тож береться середній множник конвою
            var levelWeight = units.Sum(u => u.Count * ProgressionCurves.UnitStatMultiplier(u.Level, _catalog.Config.UnitLevelStatBonus));
            var attack = config.Stats.GetValueOrDefault("Attack") * levelWeight;
            var health = config.Stats.GetValueOrDefault("Health") * levelWeight;
            var defense = config.Stats.GetValueOrDefault("Defense") * levelWeight / size;
            var interval = Battle.UnitAttackIntervals[unitType];

            // Пласкі стати військам зі спорядження діляться порівну між усіма конвоями героя
            var unitStatPower = _catalog.Config.Equipment.UnitStatPower;
            var flatAttack = leader.UnitAttack * unitStatPower / convoyCount;
            var flatHealth = leader.UnitDefense * unitStatPower / convoyCount;

            var led = FighterStats.Plain(
                (attack + flatAttack) * leader.AttackMultiplier * Battle.ConvoyAttackScale,
                defense * leader.DefenseMultiplier * Battle.ConvoyDefenseScale,
                (health + flatHealth) * leader.DefenseMultiplier * Battle.ConvoyHealthScale,
                interval);

            // Здоров'я лишається: загибель героя не лікує й не ранить його конвої
            var leaderless = FighterStats.Plain(
                attack * Battle.ConvoyAttackScale,
                defense * Battle.ConvoyDefenseScale,
                led.MaxHealth,
                interval);

            return new Convoy(unitType, size, led, leaderless);
        }
    }
}
