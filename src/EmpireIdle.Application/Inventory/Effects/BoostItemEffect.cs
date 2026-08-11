using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Inventory.Effects
{
    /// <summary>Буст: створює або продовжує активний ефект гравця.</summary>
    public class BoostItemEffect : IItemEffect
    {
        public string ItemType => "boost";

        private readonly IActiveEffectRepository _repository;

        public BoostItemEffect(IActiveEffectRepository repository)
        {
            _repository = repository;
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
                await _repository.AddAsync(
                    new ActiveEffect(Guid.NewGuid(), context.PlayerId, target,config.Multiplier, context.UtcNow + duration, config.Key),
                    cancellationToken);
                return;
            }

            if (!existing.IsActive(context.UtcNow))
            {
                // Прострочений слот переиспользовуємо: множники не стакуються, бо запис один на ціль
                existing.Restart(config.Multiplier, context.UtcNow + duration, config.Key);
                return;
            }

            if (existing.IsFrom(config.Key))
            {
                // Той самий буст — просто довше
                existing.Extend(duration);
                return;
            }

            if (config.Multiplier <= existing.Multiplier)
                throw new InvalidOperationException(
                    $"A stronger {target} boost (×{existing.Multiplier}) is already active until {existing.ExpiresAt:u}.");

            // Сильніший буст витісняє слабший; залишок часу слабкого згорає
            existing.Restart(config.Multiplier, context.UtcNow + duration, config.Key);
        }
    }
}