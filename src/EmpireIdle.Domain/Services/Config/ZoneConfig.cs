namespace EmpireIdle.Domain.Services
{

    /// <summary>Конфігурація типу зони села.</summary>
    public class ZoneConfig
    {
        /// <summary>Тип зони (plain, forest, mountain, water).</summary>
        public string Type { get; set; } = null!;

        /// <summary>Кількість слотів під будівлі у цій зоні.</summary>
        public int Slots { get; set; }
    }

}
