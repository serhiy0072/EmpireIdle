namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Бонус порогу набору артефактів (рішення 15.10.2026): 2 предмети дають один бонус,
    /// 4 — ще один. Предмети рахуються по родині будь-якої рідкості, а числа порогу N
    /// беруться з рідкості N-го предмета від найрідкіснішого — тож у кожної рідкості
    /// свій запис на кожен поріг.
    /// </summary>
    public class SetBonusConfig
    {
        /// <summary>Ключ набору однієї рідкості, що збігається з SetKey предметів: <c>{родина}_{рідкість}</c>.</summary>
        public string SetKey { get; set; } = null!;

        /// <summary>Поріг: 2 або 4 предмети родини.</summary>
        public int RequiredPieces { get; set; } = 4;

        /// <summary>Що дає поріг. Додається так само, як стати предметів.</summary>
        public Dictionary<string, double> Stats { get; set; } = new();
    }
}
