
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

        /// <summary>Село, якому належить гарнізон.</summary>
        public Guid VillageId { get; private set; }

        public IReadOnlyCollection<VillageUnit> Units => _units.AsReadOnly();
        public IReadOnlyCollection<UnitTrainingOrder> TrainingOrders => _trainingOrders.AsReadOnly();

        public Garrison(Guid id, Guid villageId) : base(id)
        {
            VillageId = villageId;
        }

        protected Garrison() { } // Для EF Core

        /// <summary>
        /// Ставить партію юнітів у чергу тренування.
        /// Інваріанти гарнізону: розмір партії 1–5, одне активне замовлення.
        /// </summary>
        public void TrainUnits(string unitType, int count, TimeSpan trainDuration)
        {
            if(count < 1 || count > 5)
                throw new InvalidOperationException("Training batch size must be between 1 and 5.");

            if (_trainingOrders.Any())
                throw new InvalidOperationException("Barracks are already training a batch.");

            _trainingOrders.Add(new UnitTrainingOrder(
                Guid.NewGuid(), Id, unitType, count, DateTime.UtcNow.Add(trainDuration)));
        }

        /// <summary>Завершує дозрілі замовлення: юніти йдуть у гарнізон.</summary>
        public int CompleteDueTraining(DateTime utcNow)
        {
            var due = _trainingOrders.Where(o => o.CompletesAt <= utcNow).ToList();

            foreach(var order in due)
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
    }
}
