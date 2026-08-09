
namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Гарнізон села: юніти та черга тренування. Окремий агрегат —
    /// не знає про ресурси й вартість (це відповідальність Village/Application).
    /// </summary>
    public class Garrison : Entity
    {
        private readonly List<VillageUnit> _units = new();
        private readonly List<UnitTrainingOrder> _trainingOrders = new();
        private readonly List<WoundedUnit> _wounded = new();
        private readonly List<RecoverableUnit> _recoverable = new();

        /// <summary>Село, якому належить гарнізон.</summary>
        public Guid VillageId { get; private set; }

        public IReadOnlyCollection<VillageUnit> Units => _units.AsReadOnly();
        public IReadOnlyCollection<UnitTrainingOrder> TrainingOrders => _trainingOrders.AsReadOnly();
        /// <summary>Поранені в Госпіталі (тільки для читання).</summary>
        public IReadOnlyCollection<WoundedUnit> Wounded => _wounded.AsReadOnly();
        /// <summary>Юніти, доступні для викупу за gems (тільки для читання).</summary>
        public IReadOnlyCollection<RecoverableUnit> Recoverable => _recoverable.AsReadOnly();

        public Garrison(Guid id, Guid villageId) : base(id)
        {
            VillageId = villageId;
        }

        protected Garrison() { } // Для EF Core

        /// <summary>
        /// Ставить партію юнітів у чергу тренування.
        /// Інваріанти гарнізону: розмір партії 1–5, одне активне замовлення.
        /// </summary>
        public void TrainUnits(string unitType, int count, TimeSpan trainDuration, DateTime utcNow)
        {
            if (count < 1 || count > 5)
                throw new InvalidOperationException("Training batch size must be between 1 and 5.");

            if (_trainingOrders.Any())
                throw new InvalidOperationException("Barracks are already training a batch.");

            _trainingOrders.Add(new UnitTrainingOrder(
                Guid.NewGuid(), Id, unitType, count, utcNow + trainDuration));
        }

        /// <summary>Завершує дозрілі замовлення: юніти йдуть у гарнізон.</summary>
        public int CompleteDueTraining(DateTime utcNow)
        {
            var due = _trainingOrders.Where(o => o.CompletesAt <= utcNow).ToList();

            foreach (var order in due)
            {
                var unit = _units.FirstOrDefault(u => u.UnitType == order.UnitType);
                if (unit is null)
                {
                    unit = new VillageUnit(Guid.NewGuid(), Id, order.UnitType);
                    _units.Add(unit);
                }
                unit.Add(order.Count);
                _trainingOrders.Remove(order);
            }
            return due.Count;
        }

        /// <summary>
        /// Знімає юнітів із гарнізону для походу.
        /// </summary>
        /// <param name="units">Тип юніта → кількість.</param>
        public void SendUnits(IReadOnlyDictionary<string, int> units)
        {
            if (units.Count == 0)
                throw new InvalidOperationException("Cannot send an empty army.");

            // Спершу перевіряємо ВСІ позиції — щоб не зняти частину і впасти
            foreach (var (unitType, count) in units)
            {
                if (count < 1)
                    throw new InvalidOperationException($"Invalid unit count for '{unitType}'.");

                var unit = _units.FirstOrDefault(u => u.UnitType == unitType)
                    ?? throw new InvalidOperationException($"No '{unitType}' units in garrison.");

                if (unit.Count < count)
                    throw new InvalidOperationException($"Not enough '{unitType}': need {count}, have {unit.Count}.");
            }

            foreach (var (unitType, count) in units)
                _units.First(u => u.UnitType == unitType).Subtract(count);
        }

        /// <summary>Повертає юнітів у гарнізон (після походу).</summary>
        public void ReceiveUnits(IReadOnlyDictionary<string, int> units)
        {
            foreach (var (unitType, count) in units)
            {
                if (count < 1)
                    continue;

                var unit = _units.FirstOrDefault(u => u.UnitType == unitType);
                if (unit is null)
                {
                    unit = new VillageUnit(Guid.NewGuid(), Id, unitType);
                    _units.Add(unit);
                }
                unit.Add(count);
            }
        }

        /// <summary>Скільки поранених зараз лежить у Госпіталі.</summary>
        public int WoundedCount => _wounded.Sum(w => w.Count);

        /// <summary>Приймає поранених після бою (у межах вільної місткості).</summary>
        public void AdmitWounded(IReadOnlyDictionary<string, int> wounded)
        {
            foreach (var (unitType, count) in wounded)
            {
                if (count <= 0)
                    continue;

                var stack = _wounded.FirstOrDefault(w => w.UnitType == unitType);
                if (stack is null)
                {
                    stack = new WoundedUnit(Guid.NewGuid(), Id, unitType, 0);
                    _wounded.Add(stack);
                }
                stack.Add(count);
            }
        }

        /// <summary>
        /// Виліковує поранених: вони повертаються в гарнізон.
        /// </summary>
        public Dictionary<string, int> HealWounded(IReadOnlyDictionary<string, int> toHeal)
        {
            var healed = new Dictionary<string, int>();

            foreach (var (unitType, requested) in toHeal)
            {
                var stack = _wounded.FirstOrDefault(w => w.UnitType == unitType);
                if (stack is null || requested <= 0)
                    continue;

                var count = Math.Min(requested, stack.Count);
                stack.Reduce(count);
                healed[unitType] = count;
            }

            _wounded.RemoveAll(w => w.Count <= 0);

            if (healed.Count > 0)
                ReceiveUnits(healed);

            return healed;
        }

        /// <summary>Прискорює замовлення тренування (speedup за gems).</summary>
        public void ReduceTrainingTime(Guid orderId, TimeSpan reduction)
        {
            var order = _trainingOrders.FirstOrDefault(o => o.Id == orderId)
                ?? throw new InvalidOperationException($"Training order {orderId} not found.");

            order.Reduce(reduction);
        }
        /// <summary>Скільки юнітів зараз доступно для викупу.</summary>
        public int RecoverableCount(DateTime utcNow)
            => _recoverable.Where(r => r.IsActive(utcNow)).Sum(r => r.Count);

        /// <summary>Записує відновлюваних після бою — окремим стеком зі своїм дедлайном.</summary>
        public void AddRecoverable(IReadOnlyDictionary<string, int> units,
            Guid battleReportId, DateTime expiresAt)
        {
            foreach (var (unitType, count) in units)
            {
                if (count <= 0)
                    continue;

                _recoverable.Add(new RecoverableUnit(
                    Guid.NewGuid(), Id, battleReportId, unitType, count, expiresAt));
            }
        }

        /// <summary>
        /// Викуповує юнітів: вони повертаються в гарнізон.
        /// Списує зі стеків у порядку найближчого дедлайну — щоб гравець не втратив те, що згорає першим.
        /// </summary>
        public Dictionary<string, int> RecoverUnits(
            IReadOnlyDictionary<string, int> toRecover, DateTime utcNow)
        {
            var recovered = new Dictionary<string, int>();

            foreach (var (unitType, requested) in toRecover)
            {
                if (requested <= 0)
                    continue;

                var remaining = requested;
                var stacks = _recoverable
                    .Where(r => r.UnitType == unitType && r.IsActive(utcNow))
                    .OrderBy(r => r.ExpiresAt);

                foreach (var stack in stacks)
                {
                    if (remaining <= 0)
                        break;

                    var taken = Math.Min(remaining, stack.Count);
                    stack.Reduce(taken);
                    remaining -= taken;
                }

                var total = requested - remaining;
                if (total > 0)
                    recovered[unitType] = total;
            }

            _recoverable.RemoveAll(r => r.Count <= 0);

            if (recovered.Count > 0)
                ReceiveUnits(recovered);

            return recovered;
        }
    }
}
