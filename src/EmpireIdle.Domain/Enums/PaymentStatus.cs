namespace EmpireIdle.Domain.Enums
{
    /// <summary>Стан платежу.</summary>
    public enum PaymentStatus
    {
        /// <summary>Сесію створено, гравець ще не заплатив.</summary>
        Pending = 1,

        /// <summary>Оплату підтверджено вебхуком, gems зараховані.</summary>
        Completed = 2,

        /// <summary>Оплата не відбулась (скасування, помилка, прострочення).</summary>
        Failed = 3
    }
}
