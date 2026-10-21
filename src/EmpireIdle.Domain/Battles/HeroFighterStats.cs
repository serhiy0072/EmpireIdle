using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Dungeons;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Battles
{
    /// <summary>
    /// Стати героя для одномоментного бою з його підсумкових статів (HeroStats.Compute) — тих самих,
    /// що показує екран героя. Власне перетворення, а не DungeonStats.From: данж читає перезарядку
    /// ходами, а цей бій — часткою, і зміна там зламала б покрокові забіги.
    /// </summary>
    public static class HeroFighterStats
    {
        public const string AttackSpeed = "AttackSpeed";
        public const string CooldownReduction = "CooldownReduction";

        /// <param name="heroStats">Підсумкові стати героя — відсотки, як у конфігу.</param>
        /// <param name="heroClass">Клас героя: від нього базовий темп удару.</param>
        /// <param name="caps">Стелі криту, блоку, вампіризму, зниження шкоди й оберегу — спільні з данжем.</param>
        public static FighterStats From(IReadOnlyDictionary<string, double> heroStats, string heroClass,
            RealtimeBattleConfig battle, DungeonsConfig caps)
        {
            if (!battle.HeroAttackIntervals.TryGetValue(heroClass, out var baseInterval))
                throw new InvalidOperationException($"RealtimeBattle.HeroAttackIntervals has no interval for class '{heroClass}'.");

            var attackSpeed = Math.Min(battle.MaxAttackSpeed, Percent(heroStats, AttackSpeed));

            // Набори tide і hollow скорочують перезарядку ходами — тут кожен хід стає часткою
            var cooldownReduction = Math.Min(battle.MaxCooldownReduction,
                Percent(heroStats, CooldownReduction) + heroStats.GetValueOrDefault(DungeonStats.CooldownTurns) * battle.CooldownReductionPerTurn);

            return new FighterStats(
                Attack: heroStats.GetValueOrDefault("Attack"),
                Defense: heroStats.GetValueOrDefault("Defense"),
                MaxHealth: heroStats.GetValueOrDefault("Health") * battle.HeroHealthScale,
                AttackInterval: baseInterval / (1 + attackSpeed),
                CritChance: HeroCrit.Chance(heroStats, caps),
                CritMultiplier: HeroCrit.Multiplier(heroStats, caps),
                BlockChance: Math.Min(caps.MaxBlockChance, Percent(heroStats, "BlockChance")),
                BlockPower: Math.Min(caps.MaxBlockPower, caps.BaseBlockPower + Percent(heroStats, "BlockPower")),
                DamageReduction: Math.Min(caps.MaxDamageReduction, Percent(heroStats, "DamageReduction")),
                Lifesteal: Math.Min(caps.MaxLifesteal, Percent(heroStats, "Lifesteal")),
                Ward: Math.Min(caps.MaxWard, Percent(heroStats, "Ward")),
                SkillDamage: Percent(heroStats, "SkillDamage"),
                CooldownMultiplier: 1 - cooldownReduction);
        }

        private static double Percent(IReadOnlyDictionary<string, double> heroStats, string stat)
            => Math.Max(0, heroStats.GetValueOrDefault(stat)) / 100;
    }
}
