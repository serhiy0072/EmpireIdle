using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.TestKit;

/// <summary>Пасивки героїв для тестів бою.</summary>
public static class Passives
{
    public static HeroPassiveConfig Defence(double percent, string target = TestKeys.Infantry,
        int unlockStars = 0, double perStar = 0) => new()
        {
            Key = $"defence_{target}_{unlockStars}",
            Target = target,
            Stat = "Defense",
            UnlockStars = unlockStars,
            BasePercent = percent,
            PercentPerStar = perStar
        };

    public static HeroPassiveConfig Attack(double percent, string target = TestKeys.Infantry,
        int unlockStars = 0, double perStar = 0) => new()
        {
            Key = $"attack_{target}_{unlockStars}",
            Target = target,
            Stat = "Attack",
            UnlockStars = unlockStars,
            BasePercent = percent,
            PercentPerStar = perStar
        };

    /// <summary>
    /// Множник від заданих пасивок — тим самим шляхом, що й бій: від конфіга
    /// через HeroCombatModifiers. Складати StackBuff вручну тест не може
    /// й не має, інакше перевірятиме власну арифметику.
    /// </summary>
    public static StackBuff Buff(int stars = 0, params HeroPassiveConfig[] passives)
    {
        var catalog = new GameConfigBuilder()
            .WithUnits()
            .WithHeroes(passives: passives)
            .BuildCatalog();

        var hero = Entities.Hero(TestKeys.CommonHero, stars: stars);

        return new HeroCombatModifiers(catalog).For(hero);
    }
}
