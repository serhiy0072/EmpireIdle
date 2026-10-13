namespace EmpireIdle.Application.Villages.ReadModels
{
    /// <summary>Підсумок «зібрати все»: що зараховано на склад.</summary>
    public record CollectAllView(List<CollectedResourceView> Collected);

    /// <summary>Скільки ресурсу зараховано на склад.</summary>
    public record CollectedResourceView(string ResourceType, int Amount);
}
