namespace EmpireIdle.Domain.ValueObjects
{
    /// <summary>Підсумок «зібрати все».</summary>
    /// <param name="Collected">Ресурс → скільки зараховано на склад.</param>
    public sealed record CollectionSummary(IReadOnlyDictionary<string, int> Collected);
}
