namespace EmpireIdle.API.Swagger
{
    /// <summary>
    /// Мутуючий маршрут, що не вимагає Idempotency-Key: запит у тілі POST, вхід, вебхук
    /// або хід данжу (його захищає номер ходу). Лише для специфікації — поведінку визначає
    /// маркер команди IIdempotentRequest.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class WithoutIdempotencyKeyAttribute : Attribute;
}
