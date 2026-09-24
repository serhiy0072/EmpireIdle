using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Правила падіння міста (GDD §2.6): які будівлі пошкоджує поразка
    /// і коли серія поразок виселяє. Випадковість — через IRandomSource,
    /// тож вибір відтворюваний у тестах.
    /// </summary>
    public sealed class CityFallRules
    {
        private readonly GameCatalog _catalog;
        private readonly CityFallConfig _config;

        public CityFallRules(GameCatalog catalog)
        {
            _catalog = catalog;
            _config = catalog.Config.Combat.CityFall;
        }

        /// <summary>Чи діє механіка у світі.</summary>
        public bool IsEnabled => _config.Enabled;

        public TimeSpan ShieldDuration => TimeSpan.FromHours(_config.ShieldHours);

        public TimeSpan RepairDuration => TimeSpan.FromHours(_config.RepairHours);

        public double DamagedProductionMultiplier => _config.DamagedProductionMultiplier;

        public double RepairCostShare => _config.RepairCostShare;

        /// <summary>Яка поразка поспіль виселяє — клієнт показує, скільки лишилось.</summary>
        public int DefeatsToEvict => _config.DefeatsToEvict;

        /// <summary>Чи виселяє серія такої довжини.</summary>
        public bool Evicts(int defeatStreak) => _config.Enabled && defeatStreak >= _config.DefeatsToEvict;

        /// <summary>
        /// Будівлі, які пошкоджує поразка. Перша в серії б'є по стінах і кількох
        /// випадкових, наступні — лише по випадкових, і стіни можуть потрапити
        /// під удар знову. Будівлі на будівництві не пошкоджуються: вони й так
        /// не працюють.
        /// </summary>
        public IReadOnlyList<Guid> PickDamaged(Village village, bool firstInStreak, IRandomSource random)
        {
            var standing = village.Buildings.Where(b => !b.IsUnderConstruction).ToList();
            var picked = new List<Guid>();

            if (firstInStreak)
                picked.AddRange(standing.Where(IsFortification).Select(b => b.Id));

            var pool = standing.Where(b => !picked.Contains(b.Id)).ToList();
            var count = firstInStreak ? _config.FirstDefeatRandomBuildings : _config.NextDefeatRandomBuildings;

            for (var i = 0; i < count && pool.Count > 0; i++)
            {
                var index = random.Next(pool.Count);

                picked.Add(pool[index].Id);
                pool.RemoveAt(index);
            }

            return picked;
        }

        private bool IsFortification(Building building)
            => _catalog.Buildings.TryGetValue(building.Type, out var config) && config.DefenceBonusPerLevel > 0;
    }
}
