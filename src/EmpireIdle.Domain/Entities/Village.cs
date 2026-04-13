using EmpireIdle.Domain.Events;
using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Головна ігрова сутність гравця — його село.
    /// Є Aggregate Root: всі зміни ресурсів і будівель відбуваються тільки через Village.
    /// </summary>
    public class Village : Entity
    {
        private readonly List<Building> _buildings = new();
        private readonly List<IDomainEvent> _domainEvents = new();
        private readonly Dictionary<string, ResourceAmount> _resources = new();

        /// <summary>Назва села.</summary>
        public string Name { get; private set; } = null!;

        /// <summary>Ідентифікатор власника.</summary>
        public Guid PlayerId { get; private set; }


        /// <summary>Час останнього нарахування ресурсів.</summary>
        public DateTime LastTickAt { get; private set; }

        /// <summary>Будівлі села (тільки для читання).</summary>
        public IReadOnlyCollection<Building> Buildings => _buildings.AsReadOnly();

        /// <summary>Доменні події що очікують публікації.</summary>
        public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        /// <summary>Всі ресурси села. Ключ — тип ресурсу.</summary>
        public IReadOnlyDictionary<string, ResourceAmount> Resources => _resources;

        public Village(Guid id, Guid playerId, string name) : base(id)
        {
            PlayerId = playerId;
            Name = name;
            _resources[ResourceType.Gold] = ResourceAmount.Zero;
            _resources[ResourceType.Wood] = ResourceAmount.Zero;
        }

        protected Village() { } // Для EF Core

        /// <summary>
        /// Нараховує ресурси на основі будівель і часу що минув з останнього тіку.
        /// </summary>
        public void CollectResources()
        {
            var elapsed = DateTime.UtcNow - LastTickAt;

            foreach (var building in _buildings)
            {
                var prodused = building.CalculateProduction(elapsed);
                foreach (var (type, amount) in prodused)
                {
                    if (!_resources.ContainsKey(type))
                    {
                        _resources[type] = ResourceAmount.Zero;
                    }
                    _resources[type] = _resources[type].Add(amount);
                }
            }

            LastTickAt = DateTime.UtcNow;
            _domainEvents.Add(new ResourcesCollected(Id, _resources));
        }


        /// <summary>Додає нову будівлю до села.</summary>
        public void AddBuilding(Building building)
        {
            _buildings.Add(building);
        }

        /// <summary>Очищує список доменних подій після їх публікації.</summary>
        public void ClearDomainEvents() => _domainEvents.Clear();
    }
}

