namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Бойова сила гравця — денормалізований read-model.
    ///
    /// Зберігається, бо потрібне сортування по всіх гравцях світу: рахувати
    /// на льоту означало б підняти гарнізони й марші всього сервера на кожен
    /// запит лідерборда.
    ///
    /// Компоненти окремими колонками: UI показує розклад, а герої зі
    /// спорядженням додадуться колонкою, не переробкою.
    /// </summary>
    public class PlayerPower : Entity
    {
        public Guid PlayerId { get; private set; }

        public int ServerId { get; private set; }

        /// <summary>Сила війська: гарнізон плюс юніти в активних маршах.</summary>
        public double ArmyPower { get; private set; }

        /// <summary>Сила героїв. Нуль до фази героїв.</summary>
        public double HeroPower { get; private set; }

        /// <summary>Сила спорядження. Нуль до фази героїв.</summary>
        public double EquipmentPower { get; private set; }

        /// <summary>Сума компонентів — саме за нею сортують лідерборд.</summary>
        public double TotalPower { get; private set; }

        public DateTime UpdatedAt { get; private set; }

        /// <summary>Concurrency token (PostgreSQL xmin).</summary>
        public uint Version { get; private set; }

        public PlayerPower(Guid id, Guid playerId, int serverId, DateTime utcNow) : base(id)
        {
            PlayerId = playerId;
            ServerId = serverId;
            UpdatedAt = utcNow;
        }

        protected PlayerPower() { } // для EF Core

        /// <summary>
        /// Записує перераховані компоненти. Абсолютні значення, не дельти:
        /// пропущена подія коштує затримки до наступної, а не назавжди
        /// хибного числа.
        /// </summary>
        public void Set(double army, double hero, double equipment, DateTime utcNow)
        {
            ArmyPower = army;
            HeroPower = hero;
            EquipmentPower = equipment;
            TotalPower = army + hero + equipment;
            UpdatedAt = utcNow;
        }
    }
}
