using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Dungeons
{
    /// <summary>
    /// Данжові властивості героя з його підсумкових статів (рішення 15.10.2026): їх дають бонуси наборів
    /// і заточка, а не окремий стат унікального предмета. Стати героя — у відсотках («CritChance» 4 = +4%),
    /// рушій бою рахує частками, тож тут ділимо на 100; перезарядка — цілі ходи, без ділення.
    /// </summary>
    public static class DungeonStats
    {
        /// <summary>Ціле число ходів, на яке набір скорочує перезарядку активного вміння.</summary>
        public const string CooldownTurns = "CooldownTurns";

        private static readonly Dictionary<string, DungeonStat> Percents = new()
        {
            ["CritChance"] = DungeonStat.CritChance,
            ["Lifesteal"] = DungeonStat.Lifesteal,
            ["DamageReduction"] = DungeonStat.DamageReduction,
            ["CritPower"] = DungeonStat.CritPower,
            ["SkillDamage"] = DungeonStat.SkillDamage,
            ["BlockChance"] = DungeonStat.BlockChance,
            ["BlockPower"] = DungeonStat.BlockPower,
            ["Ward"] = DungeonStat.Ward,
        };

        /// <summary>Лише ненульові властивості: у бою відсутня й нульова означають одне.</summary>
        public static Dictionary<DungeonStat, double> From(IReadOnlyDictionary<string, double> heroStats)
        {
            var result = new Dictionary<DungeonStat, double>();

            foreach (var (stat, dungeonStat) in Percents)
            {
                if (heroStats.GetValueOrDefault(stat) is var value and > 0)
                    result[dungeonStat] = value / 100;
            }

            if (heroStats.GetValueOrDefault(CooldownTurns) is var turns and > 0)
                result[DungeonStat.CooldownReduction] = turns;

            return result;
        }
    }
}
