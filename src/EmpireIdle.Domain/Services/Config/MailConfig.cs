namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>Строки зберігання скриньки (GDD §7.4). Числа — заглушки.</summary>
    public class MailConfig
    {
        /// <summary>Скільки діб живе особистий лист.</summary>
        public int LetterRetentionDays { get; set; } = 14;

        /// <summary>Строк оголошення, якщо при публікації свій не задано.</summary>
        public int AnnouncementRetentionDays { get; set; } = 30;

        /// <summary>
        /// Строк листа з нагородою (GDD §7.4) — крім нагород за вхід, що згорають
        /// з початком наступного періоду.
        /// </summary>
        public int RewardRetentionDays { get; set; } = 90;
    }
}
