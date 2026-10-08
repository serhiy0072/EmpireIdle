using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.TestKit;

/// <summary>Бойові пасивки героїв для тестів бою: бонус війську, що росте з рівнем вміння.</summary>
public static class Passives
{
    /// <summary>Стеля рівня вміння в тестових конфігах — та сама, що й типова в HeroesConfig.</summary>
    public const int Levels = 6;

    /// <summary>Пасивка захисту: <paramref name="percent"/> на першому рівні вміння, кратно — на наступних.</summary>
    public static HeroSkillConfig Defence(double percent, string target = TestKeys.Infantry, int unlockLevel = 1)
        => Passive("Defense", percent, target, unlockLevel);

    /// <summary>Пасивка атаки: <paramref name="percent"/> на першому рівні вміння, кратно — на наступних.</summary>
    public static HeroSkillConfig Attack(double percent, string target = TestKeys.Infantry, int unlockLevel = 1)
        => Passive("Attack", percent, target, unlockLevel);

    /// <summary>
    /// Множник від заданих пасивок — тим самим шляхом, що й бій: від конфіга
    /// через HeroCombatModifiers. Складати StackBuff вручну тест не може
    /// й не має, інакше перевірятиме власну арифметику.
    /// </summary>
    public static StackBuff Buff(int heroLevel = 1, params HeroSkillConfig[] passives)
    {
        var catalog = new GameConfigBuilder()
            .WithUnits()
            .WithHeroes(passives: passives)
            .BuildCatalog();

        var hero = Entities.Hero(TestKeys.CommonHero, level: heroLevel);

        return new HeroCombatModifiers(catalog).For(hero);
    }

    private static HeroSkillConfig Passive(string stat, double percent, string target, int unlockLevel) => new()
    {
        Key = $"{stat.ToLowerInvariant()}_{target}_{unlockLevel}",
        DisplayName = $"{stat} {target}",
        Half = stat == "Attack" ? SkillHalf.Attack : SkillHalf.Defense,
        Kind = SkillKind.Passive,
        UnlockLevel = unlockLevel,
        Troops = new SkillTroopBonusConfig
        {
            Target = target,
            Stat = stat,
            Percents = [.. Enumerable.Range(1, Levels).Select(level => percent * level)]
        }
    };
}
