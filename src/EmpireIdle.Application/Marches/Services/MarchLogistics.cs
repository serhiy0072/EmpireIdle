using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Marches.Services
{
    /// <summary>
    /// Спільна механіка походів: розворот, вантаж, місткість.
    ///
    /// Живе окремо, бо потрібна всім трьом сценаріям — бою з монстром,
    /// бою за село й доставці підкріплення. Розкидана по них, вона
    /// розійшлася б на першій же правці правил вантажопідйомності.
    /// </summary>
    public sealed class MarchLogistics
    {
        private readonly IVillageRepository _villageRepository;
        private readonly GameCatalog _catalog;
        private readonly MarchCalculator _calculator;
        private readonly VillageCapacities _capacities;
        private readonly ILogger<MarchLogistics> _logger;

        public MarchLogistics(
            IVillageRepository villageRepository,
            GameCatalog catalog,
            MarchCalculator calculator,
            VillageCapacities capacities,
            ILogger<MarchLogistics> logger)
        {
            _villageRepository = villageRepository;
            _catalog = catalog;
            _calculator = calculator;
            _capacities = capacities;   
            _logger = logger;
        }

        /// <summary>
        /// Розвертає похід додому — або завершує, якщо повертатись нікому.
        /// Час зворотної дороги рахується по вцілілих: втратив кавалерію,
        /// вертаєшся зі швидкістю піхоти.
        /// </summary>
        public void TurnMarchBack(March march, IReadOnlyDictionary<string, int> survivors, DateTime utcNow)
        {
            if (survivors.Count == 0 || survivors.Values.All(c => c <= 0))
            {
                // Уся армія загинула — повертатись нікому
                march.TurnBack(TimeSpan.Zero, utcNow);
                march.Complete(utcNow);
                return;
            }

            var backDuration = _calculator.CalculateDuration(
                march.ServerId, march.TargetX, march.TargetY, march.OriginX, march.OriginY, survivors);

            march.TurnBack(backDuration, utcNow);
        }

        /// <summary>
        /// Розвантажує здобич на склад. Надлишок понад кап згорає: везти
        /// більше, ніж вміщає сховище, гравець може, зберегти — ні.
        /// </summary>
        public async Task UnloadCargoAsync(March march, Garrison garrison, DateTime utcNow,
            CancellationToken cancellationToken)
        {
            var cargo = march.GetCargo();

            if (cargo.Count == 0)
                return;

            var village = await _villageRepository.GetByIdAsync(garrison.VillageId, cancellationToken)
                ?? throw new InvalidOperationException($"Village {garrison.VillageId} not found for garrison {garrison.Id}.");

            var stored = 0;

            foreach (var (resourceType, amount) in cargo)
                stored += village.GrantResource(
                    resourceType, amount, _capacities.StorageCapFor(village, resourceType), utcNow);

            _logger.LogInformation("March {MarchId} unloaded {Stored} of {Carried} carried resources.",
                march.Id, stored, cargo.Values.Sum());
        }

        /// <summary>
        /// Скільки армія здатна винести: сума CarryCapacity по вцілілих.
        /// Саме по вцілілих — інакше вигідно вести гарматне м'ясо заради місця.
        /// </summary>
        public int CalculateCarryCapacity(IReadOnlyDictionary<string, int> survivors)
            => survivors.Sum(pair => _catalog.Units.TryGetValue(pair.Key, out var config)
                ? (int)(config.Stats.GetValueOrDefault("CarryCapacity", 0) * pair.Value)
                : 0);

        /// <summary>
        /// Обрізає здобич до вантажопідйомності, пропорційно по ресурсах.
        /// Залишок від округлення дістається найбільшій позиції — інакше
        /// сума частин розійшлася б із лімітом.
        /// </summary>
        public Dictionary<string, int> LimitToCarryCapacity(
            IReadOnlyDictionary<string, int> loot, IReadOnlyDictionary<string, int> survivors)
        {
            var total = loot.Values.Sum();
            var capacity = CalculateCarryCapacity(survivors);

            if (total <= capacity)
                return loot.ToDictionary(pair => pair.Key, pair => pair.Value);

            if (capacity <= 0)
                return [];

            var limited = loot.ToDictionary(
                pair => pair.Key,
                pair => (int)Math.Floor((double)pair.Value * capacity / total));

            var shortfall = capacity - limited.Values.Sum();

            if (shortfall > 0)
            {
                var biggest = loot.OrderByDescending(pair => pair.Value).First().Key;
                limited[biggest] += shortfall;
            }

            return limited.Where(pair => pair.Value > 0).ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        /// <summary>
        /// Вільних місць у Госпіталі: сума (рівень × місткість на рівень) мінус уже поранені.
        /// Немає Госпіталю — немає поранених, усі втрати безповоротні.
        /// </summary>
        public int CalculateWoundedCapacity(Village? village, Garrison? garrison)
        {
            if (village is null || garrison is null)
                return 0;

            var buildingConfigs = _catalog.Buildings;

            var total = village.Buildings
                .Where(b => !b.IsUnderConstruction)
                .Sum(b => buildingConfigs.TryGetValue(b.Type, out var cfg)
                    ? cfg.WoundedCapacityPerLevel * b.Level.Value
                    : 0);

            return Math.Max(0, total - garrison.WoundedCount);
        }
    }
}
