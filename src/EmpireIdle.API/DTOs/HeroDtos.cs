namespace EmpireIdle.API.DTOs
{
    public record BuyHeroShardsRequest(string HeroKey, int Count);

    public record SummonHeroRequest(string HeroKey);
}
