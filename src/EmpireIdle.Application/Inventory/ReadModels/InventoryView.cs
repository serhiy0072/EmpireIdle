using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Application.Inventory.ReadModels
{
    /// <summary>
    /// Вміст інвентаря для показу: стакові предмети з описами з каталогу, спорядження
    /// з уже порахованими статами й діючі бусти. Сутності домену назовні не йдуть.
    /// </summary>
    public record InventoryView(List<InventoryItemView> Items, List<EquipmentView> Equipment, List<ActiveEffectView> ActiveEffects);

    /// <summary>Стаковий предмет. Невідомий каталогу ключ показується як є — звичайний, тип «unknown».</summary>
    public record InventoryItemView(string ItemKey, string DisplayName, string Description, Rarity Rarity, string Type, int Count);

    /// <summary>
    /// Екземпляр спорядження. Stats — з урахуванням заточки: гравець бачить ті самі числа,
    /// з якими предмет піде в бій; зламаний дає нулі.
    /// </summary>
    public record EquipmentView(Guid Id, string ItemKey, EquipmentSlot Slot, Rarity Rarity, int EnhancementLevel,
        Guid? EquippedByHeroId, int SlotIndex, bool IsBroken, Dictionary<string, double> Stats, bool IsOnMarket,
        DateTime? ResaleLockedUntil);

    /// <summary>Діючий буст.</summary>
    public record ActiveEffectView(EffectTarget Target, double Multiplier, DateTime ExpiresAt, string SourceItemKey);
}
