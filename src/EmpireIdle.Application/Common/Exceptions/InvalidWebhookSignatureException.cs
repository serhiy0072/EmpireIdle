namespace EmpireIdle.Application.Common.Exceptions
{
    /// <summary>
    /// Підпис вебхука не пройшов перевірку. Окремий тип, щоб API-шар відрізняв
    /// підробку від внутрішнього збою, не знаючи про конкретного провайдера.
    /// </summary>
    public class InvalidWebhookSignatureException : Exception
    {
        public InvalidWebhookSignatureException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
