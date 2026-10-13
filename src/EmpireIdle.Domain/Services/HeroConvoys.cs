using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Конвої героя (GDD §6.1, рішення 08.10.2026): кожен герой веде юнітів лише своєї ролі
    /// (воїн — піхоту, лицар — кінноту, лучник — лучників), а рівень героя визначає, скільки:
    /// конвої за кривою × фіксований розмір конвою.
    /// </summary>
    public class HeroConvoys
    {
        private readonly HeroesConfig _config;

        public HeroConvoys(HeroesConfig config)
        {
            _config = config;
        }

        /// <summary>Тип юніта, якого веде герой цієї ролі; null — роль без юнітів у конфігу.</summary>
        public string? UnitOf(HeroConfig? hero)
            => hero is not null && _config.RoleUnits.TryGetValue(hero.Class, out var unit) ? unit : null;

        /// <summary>Скільки конвоїв веде герой цього рівня — останній крок кривої, не вищий за рівень.</summary>
        public int ConvoysAt(int heroLevel)
            => _config.ConvoysByLevel
                .Where(step => step.Level <= heroLevel)
                .Select(step => step.Convoys)
                .DefaultIfEmpty(0)
                .Max();

        /// <summary>Скільки юнітів герой може повести: рівень — з табором, як і для статів.</summary>
        public int Capacity(Hero hero) => ConvoysAt(hero.EffectiveLevel) * _config.ConvoySize;

        /// <summary>Скільки героїв може вести один марш (GDD §6.1, рішення 08.10.2026): по одному на роль.</summary>
        public const int MaxHeroesPerMarch = 3;

        /// <summary>
        /// Чи можуть ці герої повести саме цих юнітів (GDD §6.1, рішення 08.10.2026): героїв різних ролей,
        /// кожен тип юнітів — під героєм своєї ролі й не більше за його конвої.
        /// Без ролей у конфігу перевіряється лише сума проти всіх конвоїв, без кривої — нічого.
        /// </summary>
        public void EnsureCanLead(IReadOnlyCollection<(Hero Hero, HeroConfig? Config)> leaders,
            IReadOnlyDictionary<UnitStackKey, int> units)
        {
            if (leaders.Count > MaxHeroesPerMarch)
                throw new RequirementNotMetException(RefusalReasons.MarchTooManyHeroes,
                    $"A march takes at most {MaxHeroesPerMarch} heroes.", MaxHeroesPerMarch);

            var sent = units.Where(u => u.Value > 0).GroupBy(u => u.Key.UnitType).ToDictionary(g => g.Key, g => g.Sum(u => u.Value));
            var limited = _config.ConvoysByLevel.Count > 0;

            if (_config.RoleUnits.Count == 0)
            {
                var total = sent.Values.Sum();
                var capacity = leaders.Sum(l => Capacity(l.Hero));

                if (limited && total > capacity)
                    throw new RequirementNotMetException(RefusalReasons.MarchOverCapacity,
                        $"The heroes lead up to {capacity} units, {total} were sent.", capacity, total, Names(leaders));

                return;
            }

            // Двоє героїв однієї ролі поділили б одні й ті самі конвої — у марші по одному на роль
            var twin = leaders.GroupBy(l => l.Config?.Class).FirstOrDefault(g => g.Count() > 1);

            if (twin is not null)
                throw new RequirementNotMetException(RefusalReasons.MarchSameRole,
                    $"Two heroes of class '{twin.Key}' in one march.", string.Join(", ", twin.Select(l => Name(l))));

            foreach (var (unit, count) in sent)
            {
                var leader = leaders.FirstOrDefault(l => UnitOf(l.Config) == unit);

                if (leader.Hero is null)
                    throw new RequirementNotMetException(RefusalReasons.MarchWrongUnits,
                        $"No hero in the march leads '{unit}'.", unit, Names(leaders));

                if (!limited)
                    continue;

                var capacity = Capacity(leader.Hero);

                if (count > capacity)
                    throw new RequirementNotMetException(RefusalReasons.MarchOverCapacity,
                        $"Hero '{leader.Hero.HeroKey}' leads up to {capacity} units, {count} were sent.", capacity, count, Name(leader));
            }
        }

        private static string Name((Hero Hero, HeroConfig? Config) leader) => leader.Config?.DisplayName ?? leader.Hero.HeroKey;

        private static string Names(IEnumerable<(Hero Hero, HeroConfig? Config)> leaders) => string.Join(", ", leaders.Select(Name));
    }
}
