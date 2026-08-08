namespace EmpireIdle.API.DTOs
{
    /// <summary>Інвентар гравця.</summary>
    public record InventoryResponse(List<InventoryItemResponse> Items, List<EquipmentResponse> Equipment, List<ActiveEffectResponse> ActiveEffects);

    /// <summary>Стаковий предмет із описом із конфіга.</summary>
    public record InventoryItemResponse(
        string ItemKey, string DisplayName, string Description,
        string Rarity, string Type, int Count);

    /// <summary>Екземпляр спорядження.</summary>
    public record EquipmentResponse(
        Guid Id, string ItemKey, string Slot, string Rarity,
        int EnhancementLevel, Guid? EquippedByHeroId,
        Dictionary<string, double> Stats);

    /// <summary>Діючий буст.</summary>
    public record ActiveEffectResponse(string Target, double Multiplier, DateTime ExpiresAt, string SourceItemKey);
}