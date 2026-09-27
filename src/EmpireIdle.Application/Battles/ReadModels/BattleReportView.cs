namespace EmpireIdle.Application.Battles.ReadModels
{
    /// <summary>Звіт про бій для показу гравцю. Сід бою й власник назовні не йдуть.</summary>
    public record BattleReportView(
        Guid Id, Guid MarchId, int X, int Y, string TerrainType,
        string TargetName, int TargetLevel, bool Won,
        double AttackerPower, double DefenderPower, DateTime FoughtAt, bool IsRead,
        List<BattleReportLineView> Lines);

    /// <summary>Рядок звіту: доля одного типу юнітів. Survived — решта після поранених і загиблих.</summary>
    public record BattleReportLineView(string UnitType, int Sent, int Survived, int Wounded, int Recoverable, int Dead);
}
