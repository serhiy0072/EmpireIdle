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

        public Building(Guid id, Guid villageId, string type) : base(id)
        {
            VillageId = villageId;
            Type = type;
            Level = BuildingLevel.Initial;
            LastCollectedAt = DateTime.UtcNow;
            LastAccruedAt = DateTime.UtcNow;
        }

        protected Building() { } // Для EF Core

        /// <summary>
        /// Максимальна місткість буфера для поточного рівня:
        /// BaseStorage × StorageGrowth^(рівень − 1), округлення вниз.
        /// Обрізається до int.MaxValue: геометричний ріст переповнює int
        /// близько 65–70 рівня, і кап стає від'ємним — буфер більше не накопичується.
        /// </summary>
        public int GetStorageCap(int baseStorage, double storageGrowth)
        {
            var cap = baseStorage * Math.Pow(storageGrowth, Level.Value - 1);

            return cap >= int.MaxValue ? int.MaxValue : (int)cap;
        }

        /// <summary>
        /// Скільки в буфері на вказаний момент. Чиста функція — стан не змінює.
        /// Інтервал ділиться на «під бустом» і «без буста», бо буст міг
        /// початись або скінчитись усередині періоду.
        /// </summary>
        /// <param name="config">Конфіг будівлі (ставка, місткість, ріст місткості).</param>
        /// <param name="utcNow">Момент, на який рахуємо.</param>
        /// <param name="boost">Вікно дії буста виробництва.</param>
        public int StoredAt(BuildingConfig config, DateTime utcNow, ProductionBoost boost)
        {
            var cap = GetStorageCap(config.BaseStorage, config.StorageGrowth);

            // Під час будівництва виробництво зупинене; невиробнича будівля не накопичує
            if (IsUnderConstruction || config.ProducesResource is null || utcNow <= LastAccruedAt)
                return Math.Min(AccruedAmount, cap);

            var totalMinutes = (utcNow - LastAccruedAt).TotalMinutes;
            var boostedMinutes = boost.OverlapMinutes(LastAccruedAt, utcNow);

            var ratePerMinute = Level.Value * config.BaseProductionPerMinute;
            var produced = ratePerMinute * (boostedMinutes * boost.Multiplier + (totalMinutes - boostedMinutes));

            return Math.Min(AccruedAmount + (int)produced, cap);
        }

        /// <summary>
        /// Згортає обчислений виробіток у збережений буфер.
        /// Викликається перед кожною зміною швидкості: апгрейд, збір.
        /// </summary>
        public void Materialize(BuildingConfig config, DateTime utcNow, ProductionBoost boost)
        {
            AccruedAmount = StoredAt(config, utcNow, boost);
            LastAccruedAt = utcNow;
        }

        /// <summary>Забирає накопичене з буфера. Повертає зібрану кількість.</summary>
        public int Collect(BuildingConfig config, DateTime utcNow, ProductionBoost boost)
        {
            var collected = StoredAt(config, utcNow, boost);

            AccruedAmount = 0;
            LastAccruedAt = utcNow;
            LastCollectedAt = utcNow;

            return collected;
        }

        /// <summary>
        /// Розпочати апгрейд: будівля переходить у стан будівництва до вказаного часу.
        /// Рівень підніметься лише при завершенні (CompleteConstruction).
        /// </summary>
        public void BeginUpgrade(BuildingConfig config, TimeSpan duration, DateTime utcNow, ProductionBoost boost)
        {
            if (IsUnderConstruction)
                throw new InvalidStateException($"Building {Id} is already under construction.");

            // Банкуємо вироблене ДО зупинки: під час будівництва виробництва немає,
            // і без цього накопичене за попередній період загубилось би
            Materialize(config, utcNow, boost);

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
