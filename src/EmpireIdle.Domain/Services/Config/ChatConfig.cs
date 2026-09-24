namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>Правила чату (GDD §7.3). Числа — заглушки до Режисера.</summary>
    public class ChatConfig
    {
        /// <summary>Найдовше повідомлення в символах.</summary>
        public int MaxLength { get; set; } = 300;

        /// <summary>Скільки повідомлень гравець може надіслати за вікно антиспаму.</summary>
        public int RateLimitCount { get; set; } = 5;

        /// <summary>Вікно антиспаму в секундах.</summary>
        public int RateLimitWindowSeconds { get; set; } = 10;

        /// <summary>Скільки діб зберігається історія; старше прибирає джоб.</summary>
        public int RetentionDays { get; set; } = 30;
    }
}
