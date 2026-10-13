namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Як клас героя перерозподіляє базу артефакта: воїн більше захищається, лучник більше б'є.
    /// Діє на атаку й захист і героя, і юнітів.
    /// </summary>
    public class ArtifactClassMultiplierConfig
    {
        public double Attack { get; set; } = 1.0;

        public double Defense { get; set; } = 1.0;
    }
}
