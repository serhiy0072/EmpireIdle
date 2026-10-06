
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Криві героя: стеля рівня, множник тіру, підсумкові стати, час черги.
    ///
    /// Окремо від агрегату Hero, бо всі ці числа залежать від конфіга й від
    /// села, а герой не знає ні про перше, ні про друге. Окремо від
    /// ProgressionCurves, бо ті описують будівлі й туди герої лише мешкали б.
    ///
    /// Чиста функція від аргументів: нічого не читає й не зберігає.
    /// </summary>
    public class HeroProgression
    {
        private readonly HeroesConfig _config;

        public HeroProgression(HeroesConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// Стеля рівня героя. Дві незалежні межі, береться нижча:
        /// ратуша обмежує всіх героїв гравця, тір — конкретного героя.
        ///
        /// Саме тому еволюція не є обов'язковою для прогресу: гравець
        /// із ратушею 8 упреться в ратушу, а не в тір.
        /// </summary>
        public int MaxLevel(int townHallLevel, int tier)
            => Math.Min(townHallLevel, tier * _config.LevelsPerTier);

        /// <summary>
        /// Множник статів за тіром (GDD §6.1): TierGrowth^(тір−1) × EvolutionPenalty^(тір − рідний тір).
        /// Рідний T2 — ×1.10, піднятий з T1 до T2 — ×1.045: апи тримають старих героїв у грі,
        /// але нові того самого тіру завжди сильніші.
        /// </summary>
        public double TierMultiplier(int tier, int nativeTier)
        {
            var steps = Math.Max(0, tier - nativeTier);

            return Math.Pow(_config.TierGrowth, Math.Max(0, tier - 1)) * Math.Pow(_config.EvolutionPenalty, steps);
        }

        /// <summary>
        /// Значення стата героя: база плюс приріст за рівень, усе помножене
        /// на множник тіру. Порядок саме такий — множник діє і на приріст,
        /// інакше високий тір знецінювався б з кожним рівнем.
        /// </summary>
        public double StatValue(HeroConfig hero, string statKey, int level, int tier, int nativeTier)
        {
            var basis = hero.BaseStats.GetValueOrDefault(statKey, 0.0);
            var growth = hero.StatGrowth.GetValueOrDefault(statKey, 0.0);

            return (basis + growth * (level - 1)) * TierMultiplier(tier, nativeTier);
        }

        /// <summary>
        /// Швидкість героя в дорозі. Окремо від StatValue, бо тір її не
        /// множить: еволюція робить героя сильнішим, а не прудкішим,
        /// інакше карта стискалася б разом із прогресом.
        ///
        /// Невідомий тип героя не валить похід, а йде базовою швидкістю:
        /// ключ, прибраний із конфіга, не має ламати вже виданих героїв.
        /// </summary>
        public double MarchSpeed(HeroConfig? config)
            => config?.Speed ?? _config.DefaultMarchSpeed;

        /// <summary>
        /// Чи можна підняти тір. Тір прив'язаний до рівня світу: перехід
        /// у другий тір відкривається на сервері 2, у третій — на сервері 3.
        /// Контент відкривається для всіх одночасно (§1.2), не для найшвидших.
        /// </summary>
        public bool CanEvolve(int currentTier, int serverLevel)
            => currentTier < _config.MaxTier && serverLevel >= currentTier + 1;

        /// <summary>
        /// Предмет, потрібний для переходу з поточного тіру в наступний.
        /// null означає, що перехід не описаний конфігом.
        /// </summary>
        public string? EvolutionItemKey(int currentTier)
        {
            var index = currentTier - 1;

            return index >= 0 && index < _config.EvolutionItemKeys.Count
                ? _config.EvolutionItemKeys[index]
                : null;
        }

        /// <summary>
        /// Скільки маршів гравець може вести одночасно.
        ///
        /// Окремого лічильника слотів немає навмисно: один герой веде один марш,
        /// тож межа й так дорівнює кількості героїв. Конфіг лише накриває її
        /// стелею, щоб зібраний ростер не давав необмежену карту.
        /// </summary>
        public int MarchCapacity(int heroCount) => Math.Min(heroCount, _config.MaxMarches);

        /// <summary>
        /// Вартість лікування героя. Лінійна від рівня: сильніший герой
        /// дорожче обходиться після поразки.
        /// </summary>
        public List<ResourceCost> HealCost(int level)
            => _config.HealCostPerLevel
            .Select(c => new ResourceCost { Resource = c.Resource, Amount = c.Amount * level })
            .ToList();

        /// <summary>Скільки триває підняття рівня до targetLevel.</summary>
        public TimeSpan LevelUpDuration(int targetLevel)
            => TimeSpan.FromMinutes(_config.BaseLevelUpMinutes * targetLevel);

        /// <summary>
        /// Вартість переходу на targetLevel. Береться смуга з найбільшим
        /// FromLevel, який не перевищує цільовий рівень.
        ///
        /// Список повертається як є, без множення: множник передається
        /// у ChargeCost, щоб вартість і списання не розходились у двох місцях.
        /// </summary>
        public List<ResourceCost> LevelUpCost(HeroConfig hero, int targetLevel)
        {
            var band = hero.LevelUpCosts
                .Where(b => b.FromLevel <= targetLevel)
                .OrderByDescending(b => b.FromLevel)
                .FirstOrDefault()
                ?? throw new InvalidOperationException(
                    $"Hero '{hero.Key}' has no cost band covering level {targetLevel}.");

            return band.Cost;
        }
    }
}
