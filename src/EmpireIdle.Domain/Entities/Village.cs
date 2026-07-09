using EmpireIdle.Domain.Events;
using EmpireIdle.Domain.Services;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Головна ігрова сутність гравця — його село.
    /// Є Aggregate Root: всі зміни ресурсів і будівель відбуваються тільки через Village.
    /// </summary>
    public class Village : Entity
    {
        private readonly List<Building> _buildings = new();
        private readonly List<VillageResource> _resources = new();

        /// <summary>Назва села.</summary>
        public string Name { get; private set; } = null!;

        /// <summary>Ідентифікатор власника.</summary>
        public Guid PlayerId { get; private set; }


        /// <summary>Час останнього нарахування ресурсів.</summary>
        public DateTime LastTickAt { get; private set; }

        /// <summary>Будівлі села (тільки для читання).</summary>
        public IReadOnlyCollection<Building> Buildings => _buildings.AsReadOnly();

        /// <summary>Всі ресурси села. Ключ — тип ресурсу.</summary>
        public IReadOnlyCollection<VillageResource> Resources => _resources;

        /// <summary>
        /// Створює нове село зі стартовим набором ресурсів (по нулю кожного).
        /// Перелік ресурсів приходить із конфіга — домен не знає конкретних назв.
        /// </summary>
        /// <param name="resourceKeys">Ключі ресурсів гри (з GameConfig.Resources).</param>
        public Village(Guid id, Guid playerId, string name, IEnumerable<string> resourceKeys) : base(id)
        {
            PlayerId = playerId;
            Name = name;
            LastTickAt = DateTime.UtcNow;

            foreach(var key in resourceKeys)
                _resources.Add(new VillageResource { VillageId=id, ResourceType=key, Amount = 0 }); ;
        }

        protected Village() { } // Для EF Core

        /// <summary>
        /// Тік виробництва: кожна будівля накопичує виробіток у власний буфер (до капу).
        /// Ресурси села змінюються лише при зборі (CollectFromBuilding).
        /// </summary>
        /// <param name="buildingConfigs">Конфігурації будівель з GameConfig (Key → BuildingConfig).</param>
        public void TickProduction(Dictionary<string, BuildingConfig> buildingConfigs)
        {
            var elapsed = DateTime.UtcNow - LastTickAt;

            foreach (var building in _buildings)
            {
                if (!buildingConfigs.TryGetValue(building.Type, out var config))
                    continue;

                building.AccumulateProduction(config.BaseProductionPerMinute, config.BaseStorage, config.StorageGrowth, elapsed);
            }

            LastTickAt = DateTime.UtcNow;
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

            if (resource.Amount < cost)
                throw new InvalidOperationException($"Not enough {config.CostResource}: need {cost}, have {resource.Amount}.");

            resource.Amount -= cost;
            building.Upgrade();

            RaiseDomainEvent(new Events.BuildingUpgraded(Id, PlayerId, building.Id, building.Type, building.Level, config.CostResource, cost));
        }

        /// <summary>
        /// Збирає накопичене з буфера будівлі у ресурси села.
        /// </summary>
        /// <param name="buildingId">Ідентифікатор будівлі.</param>
        /// <param name="buildingConfigs">Конфігурації будівель з GameConfig.</param>
        /// <exception cref="InvalidOperationException">Якщо будівля або її конфіг не знайдені.</exception>
        public void CollectFromBuilding(Guid buildingId, Dictionary<string, BuildingConfig> buildingConfigs)
        {
            var building = _buildings.FirstOrDefault(b => b.Id == buildingId)
                ?? throw new InvalidOperationException($"Building {buildingId} not found in village {Id}.");

            if (!buildingConfigs.TryGetValue(building.Type, out var config))
                throw new InvalidOperationException($"No config found for building type '{building.Type}'.");

            var collected = building.Collect();
            if (collected == 0)
                return;// порожній буфер — не подія і не зміна стану

            var resource = _resources.FirstOrDefault(r=> r.ResourceType == config.ProducesResource);
            if(resource is null)
            {
                resource = new VillageResource { VillageId = Id, ResourceType = config.ProducesResource, Amount = 0 };
                _resources.Add(resource);
            }
            resource.Amount += collected;

            RaiseDomainEvent(new BuildingCollected(Id, PlayerId, building.Id, config.ProducesResource, collected, resource.Amount));
        }

        /// <summary>Додає нову будівлю до села.</summary>
        public void AddBuilding(Building building)
        {
            _buildings.Add(building);
        }

    }
}

