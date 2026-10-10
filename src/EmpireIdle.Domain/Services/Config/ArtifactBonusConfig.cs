namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Бонус заточки (GDD §9.12): кожен ранг кидає ступінь 1–4 і додає його значення, у відсотках.
    ///
    /// Сталі бонуси слота (Attack, Defense, UnitAttack, UnitDefense) підсилюють базу самого
    /// предмета, і їхня сила видна через зрослу базу. Випадкові діють на героя й дають силу
    /// за PowerPerPercent.
    /// </summary>
    public class ArtifactBonusConfig
    {
        /// <summary>Ключ стату: Attack, CritChance, CooldownReduction…</summary>
        public string Stat { get; set; } = null!;

        /// <summary>Значення ступенів 1–4 у відсотках, зростають: 5, 10, 15, 20.</summary>
        public List<double> Steps { get; set; } = new();

        /// <summary>Сила за 1% бонусу. Для сталих бонусів — 0: їх рахує база.</summary>
        public double PowerPerPercent { get; set; }
    }
}
