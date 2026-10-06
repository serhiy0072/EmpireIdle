namespace EmpireIdle.API.DTOs
{
    public record ConvertUniversalShardsRequest(string HeroKey, int Count);

    public record UpgradeUniversalShardsRequest(EmpireIdle.Domain.Enums.Rarity From, int Count);

    public record SummonHeroRequest(string HeroKey);
}
