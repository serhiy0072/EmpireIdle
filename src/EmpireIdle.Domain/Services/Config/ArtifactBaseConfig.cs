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

        /// <summary>
        /// Здоров'я героя на рівні 0. Без нього атака й захист від спорядження росли б, а здоров'я —
        /// ні, і поріг рани героя сповзав би з прогресом (рішення 15.10.2026: 6 × база героя).
        /// </summary>
        public double HeroHealth { get; set; }

        /// <summary>Приріст здоров'я героя за рівень.</summary>
        public double HeroHealthPerLevel { get; set; }

        /// <summary>Атака й захист юнітів на рівні 0.</summary>
        public double Unit { get; set; }

        /// <summary>Приріст атаки й захисту юнітів за рівень.</summary>
        public double UnitPerLevel { get; set; }
    }
}
