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

        /// <summary>
        /// Чи може герой повести саме цих юнітів: лише своєї ролі й не більше за свої конвої.
        /// Без ролей чи кривої в конфігу відповідне обмеження не діє.
        /// </summary>
        public void EnsureCanLead(Hero hero, HeroConfig? config, IReadOnlyDictionary<UnitStackKey, int> units)
        {
            var name = config?.DisplayName ?? hero.HeroKey;

            if (_config.RoleUnits.Count > 0)
            {
                var own = UnitOf(config);
                var foreign = units.Where(u => u.Value > 0 && u.Key.UnitType != own).Select(u => u.Key.UnitType).FirstOrDefault();

                if (foreign is not null)
                    throw new RequirementNotMetException(RefusalReasons.MarchWrongUnits,
                        $"Hero '{hero.HeroKey}' leads only '{own}', not '{foreign}'.", foreign, name);
            }

            if (_config.ConvoysByLevel.Count == 0)
                return;

            var capacity = Capacity(hero);
            var sent = units.Values.Sum();

            if (sent > capacity)
                throw new RequirementNotMetException(RefusalReasons.MarchOverCapacity,
                    $"Hero '{hero.HeroKey}' leads up to {capacity} units, {sent} were sent.", capacity, sent);
        }
    }
}
