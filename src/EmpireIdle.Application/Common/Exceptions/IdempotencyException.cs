namespace EmpireIdle.Application.Common.Exceptions
{
    /// <summary>
    /// Той самий Idempotency-Key вже використали для іншої операції.
    /// Ретрай не допоможе — клієнт має взяти новий ключ.
    /// </summary>
    public sealed class IdempotencyKeyReusedException : Exception
    {
        public IdempotencyKeyReusedException(string key)
            : base($"Idempotency key '{key}' was already used for a different operation.") { }
    }

    /// <summary>
    /// Операція з цим ключем виконується просто зараз: резерв є, результату ще немає.
    /// Стан тимчасовий — клієнту варто повторити за кілька секунд.
    /// </summary>
    public sealed class OperationInProgressException : Exception
    {
        public OperationInProgressException(string key)
            : base($"Operation for idempotency key '{key}' is still in progress. Retry shortly.") { }
    }
}
