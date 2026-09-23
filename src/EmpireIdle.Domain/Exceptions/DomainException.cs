namespace EmpireIdle.Domain.Exceptions
{
    /// <summary>
    /// Порушення доменного правила — очікувана ситуація, а не баг.
    /// Мапиться на 400. Message англійський — для логів і розробника;
    /// гравцю пояснює Reason з Args, текст за ним живе на клієнті.
    /// Усе, що лишається InvalidOperationException, вважається помилкою
    /// коду й після завершення міграції поїде в 500.
    /// </summary>
    public abstract class DomainException : Exception
    {
        protected DomainException(string message) : base(message) { }

        /// <param name="values">Значення параметрів у порядку RefusalReason.ArgNames.</param>
        protected DomainException(RefusalReason reason, string message, object[] values) : base(message)
        {
            // Розбіжність — баг у місці кидка, а не відмова гравцю: клієнт підставив
            // би в текст порожнє місце замість рівня чи назви
            if (values.Length != reason.ArgNames.Count)
                throw new ArgumentException(
                    $"Refusal '{reason.Key}' expects {reason.ArgNames.Count} values, got {values.Length}.", nameof(values));

            Reason = reason.Key;
            Args = reason.ArgNames.Zip(values).ToDictionary(pair => pair.First, pair => pair.Second);
        }

        /// <summary>Ключ причини для тексту гравцю; null — відмова без пояснення (баг клієнта).</summary>
        public string? Reason { get; }

        /// <summary>Параметри тексту: рівні, назви, кількості.</summary>
        public IReadOnlyDictionary<string, object> Args { get; } = new Dictionary<string, object>();
    }

    /// <summary>Не вистачає ресурсу або валюти. Цифри — окремими полями: клієнт показує їх у відмові.</summary>
    public sealed class NotEnoughResourcesException : DomainException
    {
        public string Resource { get; }

        public long Need { get; }

        public long Have { get; }

        public NotEnoughResourcesException(string resource, long need, long have)
            : base($"Not enough {resource}: need {need}, have {have}.")
        {
            Resource = resource;
            Need = need;
            Have = have;
        }
    }

    /// <summary>Об'єкт, на який посилається запит, не належить агрегату або не існує.</summary>
    public sealed class EntityNotFoundException : DomainException
    {
        public EntityNotFoundException(string what, Guid id)
            : base($"{what} {id} not found.") { }

        public EntityNotFoundException(string what, string key)
            : base($"{what} '{key}' not found.") { }
    }

    /// <summary>Дія порушує унікальність усередині агрегату.</summary>
    public sealed class AlreadyExistsException : DomainException
    {
        public AlreadyExistsException(string what, string key)
            : base($"{what} '{key}' already exists.") { }
    }

    /// <summary>Не виконана передумова: рівень, розблокування, вільний слот.</summary>
    public sealed class RequirementNotMetException : DomainException
    {
        public RequirementNotMetException(string message) : base(message) { }

        public RequirementNotMetException(RefusalReason reason, string message, params object[] values)
            : base(reason, message, values) { }
    }

    /// <summary>Дія неможлива в поточному стані сутності.</summary>
    public sealed class InvalidStateException : DomainException
    {
        public InvalidStateException(string message) : base(message) { }

        public InvalidStateException(RefusalReason reason, string message, params object[] values)
            : base(reason, message, values) { }
    }


}
