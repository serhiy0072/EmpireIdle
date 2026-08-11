namespace EmpireIdle.Application.Common.Security
{
    /// <summary>
    /// Позначає команду, яка має виконатись не більше одного разу
    /// на ключ із заголовка Idempotency-Key.
    /// </summary>
    public interface IIdempotentRequest
    {
    }
}