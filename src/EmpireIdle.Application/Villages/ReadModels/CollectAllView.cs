namespace EmpireIdle.Application.Villages.ReadModels
{
    /// <summary>
    /// Підсумок «зібрати все»: що зараховано і які склади не прийняли буфер.
    /// Без другого списку клієнт не пояснив би, чому ресурс лишився в будівлі.
    /// </summary>
    /// <param name="FullStorages">Ключі ресурсів, чий склад повний.</param>
    public record CollectAllView(List<CollectedResourceView> Collected, List<string> FullStorages);

    /// <summary>Скільки ресурсу зараховано на склад.</summary>
    public record CollectedResourceView(string ResourceType, int Amount);
}
