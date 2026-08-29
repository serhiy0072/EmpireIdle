namespace EmpireIdle.Domain.Services
{
    /// <summary>Модифікатор сили типу юніта на певній місцевості.</summary>
    public class TerrainBonus
    {
        public string Terrain { get; set; } = null!;
        public string UnitType { get; set; } = null!;

        /// <summary>Множник сили (1.25 = +25%).</summary>
        public double Modifier { get; set; } = 1.0;
    }
}
