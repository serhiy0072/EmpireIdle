namespace EmpireIdle.Domain.Entities;

/// <summary>
/// Базовий клас для всіх доменних сутностей.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; }

    protected Entity(Guid id) => Id = id;
    protected Entity() { }
}
