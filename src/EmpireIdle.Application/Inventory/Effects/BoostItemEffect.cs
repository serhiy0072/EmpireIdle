using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Application.Inventory.Effects
{
    /// <summary>Буст: створює або продовжує активний ефект гравця.</summary>
    public class BoostItemEffect : IItemEffect
    {
        public string ItemType => "boost";

        private readonly IActiveEffectRepository _repository;
        private readonly IVillageRepository _villageRepository;
        private readonly GameCatalog _catalog;

        public BoostItemEffect(IActiveEffectRepository repository, IVillageRepository villageRepository, GameCatalog catalog)
        {
            _repository = repository;
            _villageRepository = villageRepository;
            _catalog = catalog;
        }

        public async Task ApplyAsync(ItemUsageContext context, CancellationToken cancellationToken)
        {
            var config = context.Config;

            if (config.BoostTarget is null)
                throw new InvalidOperationException($"Item '{config.Key}' has no boost target configured.");

            if (!Enum.TryParse<EffectTarget>(config.BoostTarget, ignoreCase: true, out var target))
                throw new InvalidOperationException($"Unknown boost target '{config.BoostTarget}'.");

            var duration = TimeSpan.FromHours(config.DurationHours * context.Count);
            var existing = await _repository.GetAsync(context.PlayerId, target, cancellationToken);

            if (existing is null)
            {
                if (target == EffectTarget.Production)
                    await MaterializeProductionAsync(context, null, cancellationToken);

                await _repository.AddAsync(
                    new ActiveEffect(Guid.NewGuid(), context.PlayerId, target, config.Multiplier,
                        context.UtcNow, context.UtcNow + duration, config.Key),
                    cancellationToken);
                return;
            }

            if (!existing.IsActive(context.UtcNow))
            {
                if (target == EffectTarget.Production)
                    await MaterializeProductionAsync(context, existing, cancellationToken);

                existing.Restart(config.Multiplier, context.UtcNow, context.UtcNow + duration, config.Key);
                return;
            }

            if (existing.IsFrom(config.Key))
            {
                // Той самий буст — множник і StartedAt не змінюються, фіксувати нічого
                existing.Extend(duration);
                return;
            }

            if (config.Multiplier <= existing.Multiplier)
                throw new InvalidOperationException(
                    $"A stronger {target} boost (×{existing.Multiplier}) is already active until {existing.ExpiresAt:u}.");

            if (target == EffectTarget.Production)
                await MaterializeProductionAsync(context, existing, cancellationToken);

            // Сильніший буст витісняє слабший; залишок часу слабкого згорає
            existing.Restart(config.Multiplier, context.UtcNow, context.UtcNow + duration, config.Key);
        }

        /// <summary>
        /// Фіксує накопичене за чинним бустом, перш ніж множник зміниться.
        /// Тільки для Production — решта цілей на буфер не впливає.
        /// </summary>
        private async Task MaterializeProductionAsync(ItemUsageContext context, ActiveEffect? current,
            CancellationToken cancellationToken)
        {
            var village = await _villageRepository.GetByPlayerIdAsync(context.PlayerId, cancellationToken);
            if (village is null)
                return;

            var boost = current is null
                ? ProductionBoost.None
                : new ProductionBoost(current.Multiplier, current.StartedAt, current.ExpiresAt);

            village.MaterializeProduction(_catalog.Buildings, context.UtcNow, boost);
        }
    }
}
