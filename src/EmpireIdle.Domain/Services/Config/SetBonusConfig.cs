namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Бонус за повний набір артефактів.
    ///
    /// Дається лише за комплект: три з чотирьох не дають нічого. Це й робить
    /// набір метою, а не побічним ефектом — інакше гравець збирав би
    /// найсильніші артефакти поодинці й набору не помічав.
    /// </summary>
    public class SetBonusConfig
    {
        /// <summary>Ключ набору, що збігається з SetKey предметів.</summary>
        public string SetKey { get; set; } = null!;

        /// <summary>Скільки предметів набору треба вдягнути.</summary>
        public int RequiredPieces { get; set; } = 4;

        /// <summary>Що дає комплект. Додається так само, як стати предметів.</summary>
        public Dictionary<string, double> Stats { get; set; } = new();
    }
}
