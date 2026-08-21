using EmpireIdle.Domain.Exceptions;

namespace EmpireIdle.Application.Inventory.Effects
{
    /// <summary>Добирає й виконує ефект за типом предмета.</summary>
    public class ItemEffectDispatcher
    {
        private readonly Dictionary<string, IItemEffect> _effects;

        public ItemEffectDispatcher(IEnumerable<IItemEffect> effects)
        {
            // DI віддає всі зареєстровані реалізації — індексуємо їх за типом
            _effects = effects.ToDictionary(e => e.ItemType, e => e);
        }

        public Task ApplyAsync(ItemUsageContext context, CancellationToken cancellationToken)
        {
            if (!_effects.TryGetValue(context.Config.Type, out var effect))
                throw new RequirementNotMetException($"Item type '{context.Config.Type}' cannot be used.");

            return effect.ApplyAsync(context, cancellationToken);
        }
    }
}
