namespace EmpireIdle.Domain.Services
{
    /// <summary>Параметри бойової системи.</summary>
    public class CombatConfig
    {
        /// <summary>Нижня межа випадкового множника.</summary>
        public double RandomMin { get; set; } = 0.7;

        /// <summary>Верхня межа випадкового множника.</summary>
        public double RandomMax { get; set; } = 1.4;

        /// <summary>Розкид нормального розподілу навколо 1.0.</summary>
        public double RandomSigma { get; set; } = 0.15;

        /// <summary>Бонуси типів юнітів на різній місцевості.</summary>
        public List<TerrainBonus> TerrainBonuses { get; set; } = new();

        /// <summary>Скільки годин діє право викупити відновлюваних після бою.</summary>
        public int RecoveryWindowHours { get; set; } = 24;

        /// <summary>Мінімальна частка поранених серед втрат.</summary>
        public double WoundedShareMin { get; set; } = 0.4;

        /// <summary>Максимальна частка поранених серед втрат.</summary>
        public double WoundedShareMax { get; set; } = 0.6;

        /// <summary>Частка втрат, яку можна відновити миттєво (за 200% вартості або gems).</summary>
        public double RecoverableShare { get; set; } = 0.12;

        /// <summary>
        /// Пороги співвідношення сил для прев'ю, від найвищого до найнижчого.
        /// Гравець бачить смугу, не число: точний відсоток розкрив би формулу,
        /// а невизначеність — те, що продає буст перед боєм.
        /// </summary>
        public List<double> PreviewOddsThresholds { get; set; } = new();
    }
}
