namespace EmpireIdle.Domain.ValueObjects
{
    /// <summary>
    /// Підсумок «зібрати все». Повний склад не зупиняє збір решти —
    /// він лише потрапляє в <see cref="FullStorages"/>, щоб гравець знав,
    /// чому буфер цього ресурсу лишився в будівлях.
    /// </summary>
    /// <param name="Collected">Ресурс → скільки зараховано на склад.</param>
    /// <param name="FullStorages">Ресурси, чий буфер не вліз: склад повний.</param>
    public sealed record CollectionSummary(
        IReadOnlyDictionary<string, int> Collected,
        IReadOnlyList<string> FullStorages);
}
