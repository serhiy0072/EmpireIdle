using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Конфігурація одного типу героя.
    ///
    /// Клас — рядковий ключ, а не enum, з тієї самої причини, що й тип юніта
    /// (§5.1): бій це множення чисел, поведінки, специфічної для класу, немає.
    /// Клас вирішує лише, яку зброю герой носить, і це список ключів, а не код.
    /// Новий клас має бути рядком у JSON, а не міграцією.
    /// </summary>
    public class HeroConfig
    {
        /// <summary>Унікальний ключ героя (наприклад "archer_lyra").</summary>
        public string Key { get; set; } = null!;

        public string DisplayName { get; set; } = null!;

        /// <summary>Ключ класу з HeroesConfig.Classes. Визначає придатну зброю.</summary>
        public string Class { get; set; } = null!;

        /// <summary>
        /// Ранг. Незмінний для типу героя, тому в БД не дублюється:
        /// звичайні купуються за золото, решта падає з банерів.
        /// </summary>
        public Rarity Rank { get; set; } = Rarity.Common;

        /// <summary>
        /// Стати на першому рівні першого тіру: ключ → значення.
        /// Config-driven: додав стат у JSON — код не змінюється.
        /// </summary>
        public Dictionary<string, double> BaseStats { get; set; } = new();

        /// <summary>Приріст стата за рівень, у тих самих ключах, що й BaseStats.</summary>
        public Dictionary<string, double> StatGrowth { get; set; } = new();

        /// <summary>
        /// Скільки уламків потрібно на призов. Має сенс лише для звичайних:
        /// решта приходить із банерів цілими.
        /// </summary>
        public int SummonShards { get; set; }

        /// <summary>Ціна одного уламка в золоті. Основний щоденний стік золота.</summary>
        public int ShardPriceGold { get; set; }
    }
}
