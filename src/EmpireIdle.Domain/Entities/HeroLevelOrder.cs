namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Активне замовлення на підняття рівня героя в залі героїв.
    ///
    /// Окрема сутність, а не поле в Hero, з тієї самої причини, що й
    /// UnitTrainingOrder у гарнізоні: черга має власний строк, її сканує
    /// фоновий таймер, і вона живе незалежно від того, чи герой у поході.
    ///
    /// Одне активне замовлення на гравця. Це частковий унікальний індекс
    /// у БД, а не перевірка в хендлері: перевірку можна забути.
    /// </summary>
    public class HeroLevelOrder : Entity
    {
        public Guid HeroId { get; private set; }

        /// <summary>Власник. Саме на нього діє обмеження «одна черга».</summary>
        public Guid PlayerId { get; private set; }

        /// <summary>Світ — для query-фільтра й фонових джобів по кожному серверу.</summary>
        public int ServerId { get; private set; }

        /// <summary>Рівень, який герой отримає після завершення.</summary>
        public int TargetLevel { get; private set; }

        public DateTime CompletesAt { get; private set; }

        public HeroLevelOrder(Guid id, Guid heroId, Guid playerId, int serverId,
            int targetLevel, DateTime completesAt) : base(id)
        {
            HeroId = heroId;
            PlayerId = playerId;
            ServerId = serverId;
            TargetLevel = targetLevel;
            CompletesAt = completesAt;
        }

        protected HeroLevelOrder() { } // Для EF Core

        /// <summary>Зменшує час до завершення (speedup за gems або кланова допомога).</summary>
        public void Reduce(TimeSpan reduction) => CompletesAt -= reduction;
    }
}
