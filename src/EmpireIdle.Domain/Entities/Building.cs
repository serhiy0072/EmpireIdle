using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Будівля в селі гравця. Виробляє ресурси з часом.
    /// Буфер не зберігається щохвилини, а обчислюється від <see cref="LastAccruedAt"/>.
    /// </summary>
    public class Building : Entity
    {
        /// <summary>Тип будівлі (з game-config).</summary>
        public string Type { get; private set; } = null!;

        /// <summary>Поточний рівень будівлі.</summary>
        public BuildingLevel Level { get; private set; } = null!;

        /// <summary>Ідентифікатор села якому належить будівля.</summary>
        public Guid VillageId { get; private set; }

        /// <summary>Час останнього збору ресурсів (ігрова інформація для UI).</summary>
        public DateTime LastCollectedAt { get; private set; }

        /// <summary>Час завершення поточного апгрейду; null — будівля не будується.</summary>
        public DateTime? ConstructionCompletesAt { get; private set; }

        /// <summary>Матеріалізований буфер: усе, що накопичено до <see cref="LastAccruedAt"/>.</summary>
        public int AccruedAmount { get; private set; }

        /// <summary>
        /// Момент останньої матеріалізації. Виробіток після нього не зберігається,
        /// а обчислюється — тому фоновий тік більше не потрібен.
        /// </summary>
        public DateTime LastAccruedAt { get; private set; }

        /// <summary>Чи триває апгрейд будівлі (виробництво на цей час зупинене).</summary>
        public bool IsUnderConstruction => ConstructionCompletesAt is not null;

        public Building(Guid id, Guid villageId, string type, DateTime utcNow) : base(id)
        {
            VillageId = villageId;
            Type = type;
            Level = BuildingLevel.Initial;
            LastCollectedAt = utcNow;
            LastAccruedAt = utcNow;
        }

        protected Building() { } // Для EF Core

        /// <summary>
        /// Місткість буфера для поточного рівня. Лінійна від рівня —
        /// разом із лінійним виробітком це тримає буфер сталим у годинах.
        /// </summary>
        public int GetStorageCap(int baseStorage) => ProgressionCurves.BufferCap(baseStorage, Level.Value);

        /// <summary>
        /// Скільки в буфері на вказаний момент. Чиста функція — стан не змінює.
        /// Інтервал ділиться на «під бустом» і «без буста», бо буст міг
        /// початись або скінчитись усередині періоду.
        /// </summary>
        /// <param name="config">Конфіг будівлі (ставка виробництва, базова місткість).</param>
        /// <param name="utcNow">Момент, на який рахуємо.</param>
        /// <param name="boost">Вікно дії буста виробництва.</param>
        /// <param name="locationMultiplier">
        /// Множник від кільця карти. Скаляр, а не вікно як буст: при зміні рівня
        /// сервера виробництво всіх сіл матеріалізується, тому в межах періоду
        /// між матеріалізаціями кільце змінитись не може.
        /// </param>
        public int StoredAt(BuildingConfig config, DateTime utcNow, ProductionBoost boost, double locationMultiplier)
        {
            var cap = GetStorageCap(config.BaseStorage);

            // Під час будівництва виробництво зупинене; невиробнича будівля не накопичує
            if (IsUnderConstruction || config.ProducesResource is null || utcNow <= LastAccruedAt)
                return Math.Min(AccruedAmount, cap);

            var totalMinutes = (utcNow - LastAccruedAt).TotalMinutes;
            var boostedMinutes = boost.OverlapMinutes(LastAccruedAt, utcNow);

            var ratePerMinute = Level.Value * config.BaseProductionPerMinute * locationMultiplier;
            var produced = ratePerMinute * (boostedMinutes * boost.Multiplier + (totalMinutes - boostedMinutes));

            return Math.Min(AccruedAmount + (int)produced, cap);
        }

        /// <summary>
        /// Фіксує накопичене на вказаний момент. Викликається перед зміною
        /// множника — буста або кільця карти — щоб вироблене за старим
        /// не порахувалось за новим.
        /// </summary>
        public void Materialize(BuildingConfig config, DateTime utcNow, ProductionBoost boost, double locationMultiplier)
        {
            AccruedAmount = StoredAt(config, utcNow, boost, locationMultiplier);
            LastAccruedAt = utcNow;
        }

        /// <summary>Забирає накопичене з буфера. Повертає зібрану кількість.</summary>
        public int Collect(BuildingConfig config, DateTime utcNow, ProductionBoost boost, double locationMultiplier)
        {
            var collected = StoredAt(config, utcNow, boost, locationMultiplier);

            AccruedAmount = 0;
            LastAccruedAt = utcNow;
            LastCollectedAt = utcNow;

            return collected;
        }


        /// <summary>
        /// Розпочати апгрейд: будівля переходить у стан будівництва до вказаного часу.
        /// Рівень підніметься лише при завершенні (CompleteConstruction).
        /// </summary>
        public void BeginUpgrade(BuildingConfig config, TimeSpan duration, DateTime utcNow, ProductionBoost boost,
            double locationMultiplier)
        {
            if (IsUnderConstruction)
                throw new InvalidStateException($"Building {Id} is already under construction.");

            // Банкуємо вироблене ДО зупинки: під час будівництва виробництва немає,
            // і без цього накопичене за попередній період загубилось би
            Materialize(config, utcNow, boost, locationMultiplier);

            ConstructionCompletesAt = utcNow + duration;
        }

        /// <summary>
        /// Завершити будівництво: підняти рівень і вийти зі стану будівництва.
        /// Викликається сканером, коли настав ConstructionCompletesAt.
        /// </summary>
        /// <param name="utcNow">
        /// Поточний час. Зсуває мітку накопичення — інакше період будівництва
        /// порахувався б як виробіток, ще й за новою (вищою) ставкою.
        /// </param>
        public void CompleteConstruction(DateTime utcNow)
        {
            if (!IsUnderConstruction)
                throw new InvalidStateException($"Building {Id} is not under construction.");

            Level = Level.Next();
            ConstructionCompletesAt = null;
            LastAccruedAt = utcNow;
        }

        /// <summary>Прискорити будівництво (speedup за gems або допомога союзника).</summary>
        public void ReduceConstructionTime(TimeSpan reduction)
        {
            if (!IsUnderConstruction)
                throw new InvalidStateException($"Building {Id} is not under construction.");

            ConstructionCompletesAt -= reduction;
        }
    }
}
