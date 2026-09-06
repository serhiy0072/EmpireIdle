namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>Склад загону: тип юніта і кількість.</summary>
    public class UnitStack
    {
        public string UnitType { get; set; } = null!;
        public int Count { get; set; }
    }
}
