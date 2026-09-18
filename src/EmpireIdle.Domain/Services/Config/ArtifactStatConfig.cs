namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Стат, який може випасти на артефакті, і межі його значень.
    ///
    /// Межі окремі для випадання й для прокачки: перший ролл задає предмет,
    /// подальші лише додають потроху, інакше один щасливий кидок на
    /// двадцятому рівні знецінював би всі попередні.
    /// </summary>
    public class ArtifactStatConfig
    {
        /// <summary>Ключ стата: Attack, Defense, Health.</summary>
        public string Stat { get; set; } = null!;

        /// <summary>Нижня межа при випаданні.</summary>
        public double Min { get; set; }

        /// <summary>Верхня межа при випаданні.</summary>
        public double Max { get; set; }

        /// <summary>Нижня межа приросту при прокачці.</summary>
        public double UpgradeMin { get; set; }

        /// <summary>Верхня межа приросту при прокачці.</summary>
        public double UpgradeMax { get; set; }
    }
}
