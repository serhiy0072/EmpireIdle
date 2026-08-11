using EmpireIdle.Domain.Events;

namespace EmpireIdle.Domain.Entities;

/// <summary>
/// Базовий клас для всіх доменних сутностей.
/// Накопичує доменні події, які публікуються після збереження змін.
/// </summary>
public abstract class Entity
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public Guid Id { get; protected set; }

    protected Entity(Guid id) => Id = id;
    protected Entity() { }

    /// <summary>
    /// Доменні події, що очікують публікації (тільки для читання).
    /// </summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Зафіксувати доменну подію. Викликається з домену під час зміни стану.
    /// </summary>
    protected void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    /// <summary>
    /// Очищує список подій після публікації. Викликається діспатчером
    /// .</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}
