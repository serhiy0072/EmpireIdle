namespace EmpireIdle.Application.Common.Security
{
    /// <summary>
    /// Запит, що працює з даними конкретного гравця.
    /// Behavior перевіряє, що PlayerId збігається з гравцем у токені.
    /// </summary>
    public interface IPlayerScopedRequest
    {
        Guid PlayerId { get; }
    }
}
