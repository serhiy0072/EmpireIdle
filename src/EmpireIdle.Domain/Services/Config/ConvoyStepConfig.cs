namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>Крок кривої конвоїв (GDD §6.1): з якого рівня героя він веде стільки конвоїв.</summary>
    public class ConvoyStepConfig
    {
        public int Level { get; set; }

        public int Convoys { get; set; }
    }
}
