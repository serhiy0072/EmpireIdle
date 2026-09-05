using EmpireIdle.Application.Inventory.Contracts;

namespace EmpireIdle.Application.Inventory.Effects
{
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
