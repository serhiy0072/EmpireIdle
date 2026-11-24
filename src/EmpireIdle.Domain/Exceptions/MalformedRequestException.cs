namespace EmpireIdle.Domain.Exceptions
{
    /// <summary>
    /// Запит клієнта не розбирається: кривий ключ стеку, неможливий склад загону.
    /// Чесна гра сюди не веде, але й баг це не наш — тому 400, а не 500.
    /// Окремий тип, щоб звичайний <see cref="ArgumentException"/> лишився захисною
    /// перевіркою коду й ішов у 500 з LogError.
    /// </summary>
    public sealed class MalformedRequestException : Exception
    {
        public MalformedRequestException(string message) : base(message) { }
    }
}
