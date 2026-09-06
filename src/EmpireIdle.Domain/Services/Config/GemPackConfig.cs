namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>Пакет gems.</summary>
    public class GemPackConfig
    {
        public string Key { get; set; } = null!;
        public string DisplayName { get; set; } = null!;

        /// <summary>Скільки gems отримає гравець (з урахуванням бонусу).</summary>
        public int Gems { get; set; }

        /// <summary>Ціна в центах USD (Stripe працює в мінімальних одиницях).</summary>
        public int PriceCents { get; set; }

        /// <summary>Бонус понад базовий курс — лише для відображення «вигідно».</summary>
        public int BonusPercent { get; set; }
    }
}
