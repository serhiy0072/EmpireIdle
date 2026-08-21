namespace EmpireIdle.Domain.Exceptions
{
    /// <summary>
    /// Порушення доменного правила — очікувана ситуація, а не баг.
    /// Мапиться на 400 і її Message безпечно віддавати клієнту.
    /// Усе, що лишається InvalidOperationException, вважається помилкою
    /// коду й після завершення міграції поїде в 500.
    /// </summary>
    public abstract class DomainException : Exception
    {
        protected DomainException(string message) : base(message) { }
    }

    /// <summary>Не вистачає ресурсу для операції.</summary>
    public sealed class NotEnoughResourcesException : DomainException
    {
        public NotEnoughResourcesException(string resource, long need, long have)
            : base($"Not enough {resource}: need {need}, have {have}.") { }
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
            : base($"'{key}' already exists: {what}.") { }
    }

    /// <summary>Не виконана передумова: рівень, розблокування, вільний слот.</summary>
    public sealed class RequirementNotMetException : DomainException
    {
        public RequirementNotMetException(string message) : base(message) { }
    }

    /// <summary>Дія неможлива в поточному стані сутності.</summary>
    public sealed class InvalidStateException : DomainException
    {
        public InvalidStateException(string message) : base(message) { }
    }
}
