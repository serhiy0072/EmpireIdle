namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Скільки діб тримати історію, яку гра пише на кожну дію, а читає лише свіжою.
    /// Без строку таблиці росли б лінійно з активністю назавжди. Числа — заглушки до Режисера.
    /// </summary>
    public class RetentionConfig
    {
        /// <summary>Звіти боїв: клієнт показує останні десятки, викуп відновлюваних живе добу.</summary>
        public int BattleReportDays { get; set; } = 30;

        /// <summary>Звіти розвідки.</summary>
        public int ScoutReportDays { get; set; } = 30;

        /// <summary>Завершені забіги данжів: результат читають одразу після бою, а jsonb стану важкий.</summary>
        public int FinishedDungeonRunDays { get; set; } = 1;

        /// <summary>Продані, зняті й прострочені лоти: з них рахується медіана, тож довше за її вікно.</summary>
        public int ClosedMarketListingDays { get; set; } = 30;

        /// <summary>Вирішені й прострочені заявки в клан: відмова тримає паузу на повторну заявку.</summary>
        public int ResolvedClanRequestDays { get; set; } = 7;
    }
}
