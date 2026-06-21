using EmpireIdle.Domain.Events;
using EmpireIdle.Domain.Services;
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
        private readonly List<VillageResource> _resources = new();

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
        public IReadOnlyCollection<VillageResource> Resources => _resources;

        public Village(Guid id, Guid playerId, string name) : base(id)
        {
            PlayerId = playerId;
            Name = name;
            LastTickAt = DateTime.UtcNow;
            _resources.Add(new VillageResource { VillageId = id, ResourceType = ResourceType.Gold, Amount = 0 });
            _resources.Add(new VillageResource { VillageId = id, ResourceType = ResourceType.Wood, Amount = 0 });
        }

        protected Village() { } // Для EF Core

        /// <summary>
        /// Нараховує ресурси на основі будівель і часу що минув з останнього тіку.
        /// </summary>
        /// <param name="buildingConfigs">Конфігурації будівель з GameConfig (Key → BuildingConfig).</param>
        public void CollectResources(Dictionary<string, BuildingConfig> buildingConfigs)
        {
            var elapsed = DateTime.UtcNow - LastTickAt;

            foreach (var building in _buildings)
            {
                if (!buildingConfigs.TryGetValue(building.Type, out var config))
                    continue;

                var produced = building.CalculateProduction(config.ProducesResource, config.BaseProductionPerMinute, elapsed);
                foreach (var (type, amount) in produced)
                {
                    var resource = _resources.FirstOrDefault(r => r.ResourceType == type);
                    if (resource is not null)
                    {
                        resource.Amount += amount.Value;
                    }
                    else
                    {
                        _resources.Add(new VillageResource { VillageId = Id, ResourceType = type, Amount = 0 });
                    }
                }
            }

            LastTickAt = DateTime.UtcNow;
            _domainEvents.Add(new ResourcesCollected(Id, _resources.ToDictionary(r => r.ResourceType, r => new ResourceAmount(r.Amount))));
        }

        /// <summary>
        /// Апгрейдити будівлю за ресурси згідно з конфігурацією.
        /// </summary>
        /// <param name="buildingId">Ідентифікатор будівлі для апгрейду.</param>
        /// <param name="buildingConfigs">Конфігурації будівель з GameConfig.</param>
        /// <exception cref="InvalidOperationException">Якщо будівля не знайдена, конфіг відсутній, або недостатньо ресурсів.</exception>
        public void UpgradeBuilding(Guid buildingId, Dictionary<string, BuildingConfig> buildingConfigs)
        {
            var building = _buildings.FirstOrDefault(b => b.Id == buildingId) ??
                throw new InvalidOperationException($"Building {buildingId} not found in village {Id}.");

            if (!buildingConfigs.TryGetValue(building.Type, out var config))
                throw new InvalidOperationException($"No config found for building type '{building.Type}'.");

            var cost = config.BaseCost * building.Level.Value;

            var resource = _resources.FirstOrDefault(r => r.ResourceType == config.CostResource) 
                ?? throw new InvalidOperationException($"Resource '{config.CostResource}' not found in village {Id}.");

            if(resource.Amount < cost)
                throw new InvalidOperationException($"Not enough {config.CostResource}: need {cost}, have {resource.Amount}.");

            resource.Amount -= cost;
            building.Update();

            _domainEvents.Add(new Events.BuildingUpgraded(Id, building.Id, building.Type, building.Level, config.CostResource, cost));
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

