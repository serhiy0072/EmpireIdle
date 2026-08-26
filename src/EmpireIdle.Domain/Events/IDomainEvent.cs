namespace EmpireIdle.Domain.Events
{
    /// <summary>
    /// Час, коли подія відбулась. Заповнює той, хто публікує подію:
    /// у методі агрегату вже є utcNow операції, і подія має нести саме його.
    /// Читати годинник у конструкторі події не можна — тоді час події
    /// розходиться з часом мутації, яку вона описує.
    /// </summary>
    public interface IDomainEvent
    {
        /// <summary>Час коли подія відбулась.</summary>
        DateTime OccurredAt { get; }
    }
}
