namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Вартість рівня героя починаючи з певного рівня. Діє та смуга,
    /// у якої FromLevel найбільший серед тих, що не перевищують цільовий рівень.
    ///
    /// Смугами, а не одним списком: із рівнем міняється не лише кількість
    /// ресурсів, а й самий їх набір — пізні рівні можуть вимагати те,
    /// чого на ранніх не існувало.
    /// </summary>
    public class HeroLevelCostBand
    {
        /// <summary>Рівень, з якого діє ця смуга. Найнижча має починатися з 1.</summary>
        public int FromLevel { get; set; }

        /// <summary>Вартість за один рівень. Множиться на цільовий рівень.</summary>
        public List<ResourceCost> Cost { get; set; } = new();
    }
}
