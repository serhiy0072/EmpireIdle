using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;
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
        private readonly IHeroRepository _heroRepository;
        private readonly GameCatalog _catalog;
        private readonly MarchCalculator _calculator;
        private readonly HeroProgression _progression;
        private readonly ILogger<MarchLogistics> _logger;

        public MarchLogistics(
            IVillageRepository villageRepository,
            IHeroRepository heroRepository,
            GameCatalog catalog,
            MarchCalculator calculator,
            HeroProgression progression,
            ILogger<MarchLogistics> logger)
        {
            _villageRepository = villageRepository;
            _heroRepository = heroRepository;
            _catalog = catalog;
            _calculator = calculator;
            _progression = progression;
            _logger = logger;
        }

        /// <summary>
        /// Розвертає похід додому — або завершує, якщо повертатись нікому.
        /// Час зворотної дороги рахується по вцілілих разом із героєм: втратив
        /// кавалерію, вертаєшся зі швидкістю піхоти.
        ///
        /// Герой у дорозі повертається завжди, навіть коли юнітів не лишилось:
        /// завершити такий похід одразу означало б лишити героя без гарнізону
        /// назавжди — прибуття додому єдине, що ставить його назад.
        /// </summary>
        public async Task TurnMarchBackAsync(March march, IReadOnlyDictionary<UnitStackKey, int> survivors,
            DateTime utcNow, CancellationToken cancellationToken)
        {
            // Герой, що вже став у чужий гарнізон, з маршу знятий (Arrive) — додому з колоною не йде
            var hero = (await _heroRepository.GetByMarchAsync(march.Id, cancellationToken)).SingleOrDefault();

            if (hero is null && (survivors.Count == 0 || survivors.Values.All(c => c <= 0)))
            {
                // Нікого не лишилось — повертатись нікому
                march.TurnBack(TimeSpan.Zero, utcNow);
                march.Complete(utcNow);
                return;
            }

            // Швидкість від пасивки звіра зафіксована на марші при виході — і назад іде так само
            var backDuration = _calculator.CalculateDuration(
                march.ServerId, march.TargetX, march.TargetY, march.OriginX, march.OriginY, survivors,
                hero is null ? null : _progression.MarchSpeed(_catalog.FindHero(hero.HeroKey))) / march.SpeedMultiplier;

            march.TurnBack(backDuration, utcNow);
        }

        /// <summary>Розвантажує здобич на склад. Стелі складу немає (GDD §4.1) — лягає все.</summary>
        public async Task UnloadCargoAsync(March march, Garrison garrison, DateTime utcNow,
            CancellationToken cancellationToken)
        {
            var cargo = march.GetCargo();

            if (cargo.Count == 0)
                return;

            var village = await _villageRepository.GetByIdAsync(garrison.VillageId, cancellationToken)
                ?? throw new InvalidOperationException($"Village {garrison.VillageId} not found for garrison {garrison.Id}.");

            foreach (var (resourceType, amount) in cargo)
                village.GrantResource(resourceType, amount, utcNow);

            _logger.LogInformation("March {MarchId} unloaded {Carried} carried resources.",
                march.Id, cargo.Values.Sum());
        }

        /// <summary>
        /// Скільки армія здатна винести: сума CarryCapacity по вцілілих.
        /// Саме по вцілілих — інакше вигідно вести гарматне м'ясо заради місця.
        /// </summary>
        /// <param name="multiplier">Бонус гравця до вантажу (пасивка звіра, GDD §5.10).</param>
        public int CalculateCarryCapacity(IReadOnlyDictionary<UnitStackKey, int> survivors, double multiplier = 1.0)
            => (int)(survivors.Sum(pair => _catalog.Units.TryGetValue(pair.Key.UnitType, out var config)
                ? config.Stats.GetValueOrDefault("CarryCapacity", 0) * pair.Value
                : 0) * multiplier);

        /// <summary>
        /// Обрізає здобич до вантажопідйомності, пропорційно по ресурсах.
        /// Залишок від округлення дістається найбільшій позиції — інакше
        /// сума частин розійшлася б із лімітом.
        /// </summary>
        public Dictionary<string, int> LimitToCarryCapacity(
            IReadOnlyDictionary<string, int> loot, IReadOnlyDictionary<UnitStackKey, int> survivors, double multiplier = 1.0)
        {
            var total = loot.Values.Sum();
            var capacity = CalculateCarryCapacity(survivors, multiplier);

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
