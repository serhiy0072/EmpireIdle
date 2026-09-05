using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Services
{
    /// <summary>Результат відкриття лутбокса.</summary>
    public record LootRollResult(LootDropConfig Drop, bool WasPity);

    /// <summary>
    /// Розігрує вміст лутбокса за вагами з конфіга.
    /// Гарантує легендарний дроп, якщо вичерпано pity-лічильник.
    /// </summary>
    public class LootBoxRoller
    {
        private const string LegendaryRarity = "legendary";

        private readonly ShopConfig _config;
        private readonly IRandomSource _random;

        public LootBoxRoller(ShopConfig config, IRandomSource random)
        {
            _config = config;
            _random = random;
        }

        /// <summary>Знаходить конфіг лутбокса за ключем.</summary>
        public LootBoxConfig GetBox(string boxKey)
            => _config.LootBoxes.FirstOrDefault(b => b.Key == boxKey)
               ?? throw new InvalidOperationException($"Unknown loot box '{boxKey}'.");

        /// <summary>
        /// Розігрує один дроп.
        /// </summary>
        /// <param name="boxKey">Тип лутбокса.</param>
        /// <param name="sinceLastLegendary">Скільки відкриттів без легендарки вже було.</param>
        public LootRollResult Roll(string boxKey, int sinceLastLegendary)
        {
            var box = GetBox(boxKey);

            // Pity: вичерпали ліміт — легендарка гарантована
            if (sinceLastLegendary + 1 >= box.PityCount)
            {
                var legendary = box.Drops.FirstOrDefault(d => d.Rarity == LegendaryRarity)
                    ?? throw new InvalidOperationException($"Loot box '{boxKey}' has no legendary drop configured.");

                return new LootRollResult(legendary, WasPity: true);
            }

            var totalWeight = box.Drops.Sum(d => d.Weight);
            if (totalWeight <= 0)
                throw new InvalidOperationException($"Loot box '{boxKey}' has no drops with positive weight.");

            var roll = _random.Next(totalWeight);
            var cumulative = 0;

            foreach (var drop in box.Drops.OrderBy(d => d.Key))
            {
                cumulative += drop.Weight;
                if (roll < cumulative)
                    return new LootRollResult(drop, WasPity: false);
            }

            return new LootRollResult(box.Drops[^1], WasPity: false); // недосяжно
        }

        /// <summary>Публічні шанси випадіння у відсотках — для відображення в магазині.</summary>
        public Dictionary<string, double> GetOdds(string boxKey)
        {
            var box = GetBox(boxKey);
            var total = box.Drops.Sum(d => d.Weight);

            return box.Drops.ToDictionary(
                d => d.Key,
                d => total > 0 ? Math.Round(d.Weight * 100.0 / total, 2) : 0);
        }
    }
}
