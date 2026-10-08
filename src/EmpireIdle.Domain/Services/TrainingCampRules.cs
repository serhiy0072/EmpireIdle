using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Правила навчального табору (GDD §6.1, рішення 07.10.2026). Чиста функція від конфіга
    /// й ростеру гравця: рівень табору, доступність і кількість слотів.
    /// </summary>
    public class TrainingCampRules
    {
        private readonly TrainingCampConfig _config;

        public TrainingCampRules(TrainingCampConfig config)
        {
            _config = config;
        }

        public int ReferenceSize => _config.ReferenceSize;

        /// <summary>
        /// Рівень табору — найменший рівень в опорній п'ятірці, тобто серед ReferenceSize найсильніших
        /// героїв поза табором. Поза табором менше п'яти — табір недоступний (null): качаємо п'ятьох,
        /// і найслабший із них задає рівень усім у таборі.
        /// </summary>
        public int? CampLevel(IEnumerable<Hero> heroes)
        {
            var reference = ReferenceHeroes(heroes);

            return reference.Count < _config.ReferenceSize ? null : reference.Min(h => h.Level);
        }

        /// <summary>
        /// Опорна п'ятірка: найсильніші герої поза табором. За рівного рівня — хто прийшов раніше:
        /// сталий порядок, щоб п'ятірка на екрані не перескакувала між однаковими героями.
        /// </summary>
        public List<Hero> ReferenceHeroes(IEnumerable<Hero> heroes)
            => heroes
                .Where(h => h.CampSlot is null)
                .OrderByDescending(h => h.Level)
                .ThenBy(h => h.AcquiredAt)
                .ThenBy(h => h.Id)
                .Take(_config.ReferenceSize)
                .ToList();

        /// <summary>Скільки безкоштовних слотів відкриває ратуша цього рівня.</summary>
        public int FreeSlots(int townHallLevel) => _config.FreeSlotTownHallLevels.Count(level => level <= townHallLevel);

        public int MaxPurchasableSlots => _config.ExtraSlotPricesGems.Count;

        /// <summary>Ціна наступного слота за gems; null — усі вже куплені.</summary>
        public int? NextSlotPrice(int purchasedSlots)
            => purchasedSlots < _config.ExtraSlotPricesGems.Count ? _config.ExtraSlotPricesGems[purchasedSlots] : null;

        public int TotalSlots(int townHallLevel, int purchasedSlots) => FreeSlots(townHallLevel) + purchasedSlots;

        public TimeSpan SlotCooldown => TimeSpan.FromHours(_config.SlotCooldownHours);

        public int SkipCooldownGems => _config.SkipCooldownGems;
    }
}
