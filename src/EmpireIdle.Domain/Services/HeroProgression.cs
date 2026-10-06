
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
        /// Множник бойової міці від зірок (GDD §6.1): кожна частинка — StarPartBonus до всіх статів.
        /// Множиться з тіром, тож розрив між рідним і піднятим героєм росте разом із зірками.
        /// </summary>
        public double StarMultiplier(int starParts) => 1 + _config.StarPartBonus * Math.Max(0, starParts);

        /// <summary>Скільки повних зірок дають заповнені частинки.</summary>
        public int Stars(int starParts) => _config.PartsPerStar < 1 ? 0 : starParts / _config.PartsPerStar;

        /// <summary>Ціна наступної частинки в осколках; null — усі зірки вже заповнені.</summary>
        public int? NextStarPartCost(int starParts)
        {
            if (starParts >= _config.MaxStarParts)
                return null;

            var star = starParts / _config.PartsPerStar;
            var part = starParts % _config.PartsPerStar;

            return _config.StarPartCosts[star][part];
        }

        /// <summary>Стеля рівня героя — одна для всіх (GDD §6.1).</summary>
        public int MaxLevel => _config.MaxLevel;

        /// <summary>Скільки досвіду коштує перехід із рівня <paramref name="level"/> на наступний.</summary>
        public long ExperienceToNext(int level)
            => (long)Math.Round(_config.ExperienceBase * Math.Pow(Math.Max(1, level), _config.ExperienceExponent));

        /// <summary>Скільки досвіду коштує дорога з рівня <paramref name="from"/> до <paramref name="to"/>.</summary>
        public long ExperienceBetween(int from, int to)
        {
            long total = 0;

            for (var level = from; level < to; level++)
                total += ExperienceToNext(level);

            return total;
        }

        /// <summary>
        /// Скільки досвіду повертає скидання героя з рівня <paramref name="level"/> на перший:
        /// усе вкладене мінус ResetPenalty (GDD §6.1). Округлення вниз — штраф не стає нулем на малих сумах.
        /// </summary>
        public long ResetRefund(int level)
            => (long)Math.Floor(ExperienceBetween(1, level) * (1 - _config.ResetPenalty));

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
        public double StatValue(HeroConfig hero, string statKey, int level, int tier, int nativeTier, int starParts)
        {
            var basis = hero.BaseStats.GetValueOrDefault(statKey, 0.0);
            var growth = hero.StatGrowth.GetValueOrDefault(statKey, 0.0);

            return (basis + growth * (level - 1)) * TierMultiplier(tier, nativeTier) * StarMultiplier(starParts);
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
    }
}
