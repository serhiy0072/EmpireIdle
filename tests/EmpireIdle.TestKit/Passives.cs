using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.TestKit;

/// <summary>Пасивки героїв для тестів бою.</summary>
public static class Passives
{
    public static HeroPassiveConfig Defence(double percent, string target = TestKeys.Infantry,
        int unlockConstellation = 0, double perConstellation = 0) => new()
        {
            Key = $"defence_{target}_{unlockConstellation}",
            Target = target,
            Stat = "Defense",
            UnlockConstellation = unlockConstellation,
            BasePercent = percent,
            PercentPerConstellation = perConstellation
        };

    public static HeroPassiveConfig Attack(double percent, string target = TestKeys.Infantry,
        int unlockConstellation = 0, double perConstellation = 0) => new()
        {
            Key = $"attack_{target}_{unlockConstellation}",
            Target = target,
            Stat = "Attack",
            UnlockConstellation = unlockConstellation,
            BasePercent = percent,
            PercentPerConstellation = perConstellation
        };

    /// <summary>
    /// Множник від заданих пасивок — тим самим шляхом, що й бій: від конфіга
    /// через HeroCombatModifiers. Складати StackBuff вручну тест не може
    /// й не має, інакше перевірятиме власну арифметику.
    /// </summary>
    public static StackBuff Buff(int constellation = 0, params HeroPassiveConfig[] passives)
    {
        var catalog = new GameConfigBuilder()
            .WithUnits()
            .WithHeroes(passives: passives)
            .BuildCatalog();

        var hero = Entities.Hero(TestKeys.CommonHero, constellation: constellation);

        return new HeroCombatModifiers(catalog).For(hero);
    }
}
