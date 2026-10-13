namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Пласка база артефакта однієї рідкості (GDD §9.12). Атака й захист — одне число,
    /// для героя й для юнітів окремо: на рівні L стат = старт + приріст · L.
    /// </summary>
    public class ArtifactBaseConfig
    {
        /// <summary>Атака й захист героя на рівні 0.</summary>
        public double Hero { get; set; }

        /// <summary>Приріст атаки й захисту героя за рівень.</summary>
        public double HeroPerLevel { get; set; }

        /// <summary>Атака й захист юнітів на рівні 0.</summary>
        public double Unit { get; set; }

        /// <summary>Приріст атаки й захисту юнітів за рівень.</summary>
        public double UnitPerLevel { get; set; }
    }
}
