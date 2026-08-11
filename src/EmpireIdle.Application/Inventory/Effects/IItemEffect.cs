using EmpireIdle.Domain.Services;

namespace EmpireIdle.Application.Inventory.Effects
{
    /// <summary>Контекст застосування предмета.</summary>
    public record ItemUsageContext(Guid PlayerId, ItemConfig Config, int Count, Guid? TargetId, DateTime UtcNow);

    /// <summary>
    /// Ефект предмета. Кожен тип із конфіга має власну реалізацію;
    /// диспетчер добирає її за <see cref="ItemType"/>.
    /// </summary>
    public interface IItemEffect
    {
        /// <summary>Тип предмета з конфіга, який обробляє цей ефект.</summary>
        string ItemType { get; }

        /// <summary>Застосовує ефект. Кидає виняток, якщо застосувати неможливо.</summary>
        Task ApplyAsync(ItemUsageContext context, CancellationToken cancellationToken);
    }
}