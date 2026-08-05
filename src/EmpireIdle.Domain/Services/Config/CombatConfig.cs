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
    }

    /// <summary>Модифікатор сили типу юніта на певній місцевості.</summary>
    public class TerrainBonus
    {
        public string Terrain { get; set; } = null!;
        public string UnitType { get; set; } = null!;

        /// <summary>Множник сили (1.25 = +25%).</summary>
        public double Modifier { get; set; } = 1.0;
    }
}
