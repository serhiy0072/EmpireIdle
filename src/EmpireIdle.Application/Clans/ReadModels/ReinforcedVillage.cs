namespace EmpireIdle.Application.Clans.ReadModels
{
    /// <summary>Один стек моїх військ у чужому селі.</summary>
    public record DeployedUnits(string UnitType, int Count, DateTime ArrivedAt);

    /// <summary>Мої війська в одному союзному селі.</summary>
    public record ReinforcedVillage(
        Guid VillageId,
        string VillageName,
        Guid HostPlayerId,
        string HostName,
        int X,
        int Y,
        int TotalUnits,
        List<DeployedUnits> Units);
}
