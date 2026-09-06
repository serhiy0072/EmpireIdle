using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Inventory.ReadModels
{
    /// <summary>Вміст інвентаря: стакові предмети, спорядження та діючі бусти.</summary>
    public record InventoryContents(List<PlayerItem> Items, List<EquipmentItem> Equipment, List<ActiveEffect> ActiveEffects);
}
