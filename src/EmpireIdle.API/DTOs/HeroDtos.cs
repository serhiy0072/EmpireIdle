namespace EmpireIdle.API.DTOs
{
    public record ConvertUniversalShardsRequest(string HeroKey, int Count);

    public record UpgradeUniversalShardsRequest(EmpireIdle.Domain.Enums.Rarity From, int Count);

    public record SummonHeroRequest(string HeroKey);

    /// <summary>Скільки предметів вдягнуло «швидке використання»; 0 — кращого вільного немає.</summary>
    public record EquipBestResponse(int Equipped);

    /// <summary>Відкрити скрині зброї для героя: яка скриня й скільки штук (GDD §6.4).</summary>
    public record OpenWeaponChestsRequest(string ItemKey, int Count);
}
