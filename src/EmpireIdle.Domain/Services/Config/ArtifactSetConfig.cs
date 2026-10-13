namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Родина наборів артефактів — одна на данж, у трьох рідкостях
    /// (SetKey предметів — <c>{Key}_{рідкість}</c>).
    ///
    /// Рівень робить артефакти з важчого данжу сильнішими, характер
    /// розводить два набори одного рівня: без нього вони відрізнялись би
    /// лише назвою, і вибір між данжами пари не мав би сенсу.
    /// </summary>
    public class ArtifactSetConfig
    {
        /// <summary>Ключ родини; збігається з ArtifactSetKey данжу.</summary>
        public string Key { get; set; } = null!;

        /// <summary>Назва для гравця: «Набір Світанку».</summary>
        public string DisplayName { get; set; } = null!;

        /// <summary>Рівень набору від 1; індекс у ArtifactTierMultipliers — Tier − 1.</summary>
        public int Tier { get; set; } = 1;

        /// <summary>
        /// Характер набору для гравця: атакувальний чи захисний. На стати предметів
        /// не впливає — їх задають база й заточка; бонус повного набору — у SetBonuses.
        /// </summary>
        public List<string> FocusStats { get; set; } = new();
    }
}
