using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Будівля в селі гравця. Виробляє ресурси з часом.
    /// </summary>
    public class Building : Entity
    {
        /// <summary>Тип будівлі (з game-config).</summary>
        public string Type { get; private set; } = null!;

        /// <summary>Поточний рівень будівлі.</summary>
        public BuildingLevel Level { get; private set; } = null!;

        /// <summary>Ідентифікатор села якому належить будівля.</summary>
        public Guid VillageId { get; private set; }

        /// <summary>Час останнього збору ресурсів.</summary>
        public DateTime LastCollectedAt { get; private set; }

        /// <summary>Час завершення поточного апгрейду; null — будівля не будується.</summary>
        public DateTime? ConstructionCompletesAt { get; private set; }

        /// <summary>Накопичені в буфері ресурси, що очікують збору.</summary>
        public int StoredAmount { get; private set; }

        /// <summary>Чи триває апгрейд будівлі (виробництво на цей час зупинене).</summary>
        public bool IsUnderConstruction => ConstructionCompletesAt is not null;

        /// <summary>
        /// Дробовий залишок виробництва (0..1), що переноситься на наступний тік.
        /// Без нього ціла частина обрізалась би щотіку і дрібний виробіток губився назавжди.
        /// </summary>
        public double ProductionRemainder { get; private set; }

        public Building(Guid id, Guid villageId, string type) : base(id)
        {
            VillageId = villageId;
            Type = type;
            Level = BuildingLevel.Initial;
            LastCollectedAt = DateTime.UtcNow;
        }

        protected Building() { }// Для EF Core

        /// <summary>
        /// Максимальна місткість буфера для поточного рівня:
        /// BaseStorage × StorageGrowth^(рівень − 1), округлення вниз.
        /// </summary>
        public int GetStorageCap(int baseStorage, double storageGrowth)
            => (int)(baseStorage * Math.Pow(storageGrowth,Level.Value-1));

        /// <summary>
        /// Розраховує кількість ресурсів вироблених за вказаний час.
        /// Базова формула: рівень * 10 одиниць за хвилину.
        /// </summary>
        /// <param name="baseProductionPerMinute">Базова швидкість виробництва з GameConfig.</param>
        /// <param name="baseStorage">Базова місткість буфера з GameConfig.</param>
        /// <param name="storageGrowth">Коефіцієнт росту місткості з GameConfig.</param>
        /// <param name="elapsed">Час що минув від попереднього тіку.</param>
        public void AccumulateProduction(int baseProductionPerMinute, int baseStorage, double storageGrowth, TimeSpan elapsed)
        {
            if (IsUnderConstruction)
                return; // будівля на реконструкції - виробництво зупинене

            if (elapsed <= TimeSpan.Zero)
                return;

            var cap = GetStorageCap(baseStorage, storageGrowth);
            if(StoredAmount >= cap)
                return;// буфер повний — виробництво зупинене, нічого не накопичуємо

            var produced = Level.Value * baseProductionPerMinute * elapsed.TotalMinutes + ProductionRemainder;
            var whole = (int)produced;

            StoredAmount = Math.Min(StoredAmount + whole, cap);
            ProductionRemainder = StoredAmount < cap ? produced - whole : 0; // якщо буфер заповнений, дробову частину не переносимо
        }

        /// <summary>Збирає накопичене з буфера. Повертає зібрану кількість.</summary>
        public int Collect(DateTime utcNow)
        {
            var collected = StoredAmount;
            StoredAmount = 0;
            LastCollectedAt = utcNow;
            return collected;
        }

        /// <summary>
        /// Підвищити рівень будівлі на 1.
        /// Валідація ресурсів відбувається в Village (aggregate root).
        /// </summary>
        public void Upgrade()
        {
            Level = Level.Next();
        }

        /// <summary>
        /// Розпочати апгрейд: будівля переходить у стан будівництва до вказаного часу.
        /// Рівень підніметься лише при завершенні (CompleteConstruction).
        /// </summary>
        public void BeginUpgrade(TimeSpan duration, DateTime utcNow)
        {
            if (IsUnderConstruction)
                throw new InvalidOperationException($"Building {Id} is already under construction.");

            ConstructionCompletesAt = utcNow + duration;
        }

        /// <summary>
        /// Завершити будівництво: підняти рівень і вийти зі стану будівництва.
        /// Викликається сканером, коли настав ConstructionCompletesAt.
        /// </summary>
        public void CompleteConstruction()
        {
            if (!IsUnderConstruction)
                throw new InvalidOperationException($"Building {Id} is not already under construction.");
            Level = Level.Next();
            ConstructionCompletesAt = null;
        }

        /// <summary>Прискорити будівництво (speedup за gems або допомога союзника).</summary>
        public void ReduceConstructionTime(TimeSpan reduction)
        {

            if (!IsUnderConstruction)
                throw new InvalidOperationException($"Building {Id} is not already under construction.");

            ConstructionCompletesAt -= reduction;
        }
    }
}
