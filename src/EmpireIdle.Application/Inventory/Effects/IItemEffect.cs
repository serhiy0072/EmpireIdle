using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Application.Inventory.Effects
{
    /// <summary>
    /// Контекст застосування предмета.
    /// TargetX/TargetY — для предметів, що діють на клітину карти, а не на сутність.
    /// </summary>
    public record ItemUsageContext(Guid PlayerId, ItemConfig Config, int Count, Guid? TargetId, DateTime UtcNow, int? TargetX = null, int? TargetY = null);

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
