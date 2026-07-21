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

        /// <summary>Ідентифікатор ігрового сервера, на якому живе цей гравець.</summary>
        public int ServerId { get; private set; }

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
        public Village(Guid id, Guid playerId, string name, IEnumerable<string> resourceKeys, int serverId = 1) : base(id)
        {
            PlayerId = playerId;
            Name = name;
            LastTickAt = DateTime.UtcNow;
            ServerId = serverId;

            foreach(var key in resourceKeys)
                _resources.Add(new VillageResource { VillageId=id, ResourceType=key, Amount = 0 }); 
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
        /// Розпочати апгрейд будівлі: перевіряє ліміт будівельників і вартість,
        /// списує ресурси та ставить будівлю в стан будівництва.
        /// Рівень підніметься при завершенні (CompleteDueConstructions).
        /// </summary>
        public void BeginBuildingUpgrade(Guid buildingId, Dictionary<string, BuildingConfig> buildingConfigs, int builderCount = 1)
        {
            if (_buildings.Count(b => b.IsUnderConstruction) >= builderCount)
                throw new InvalidOperationException("All builders are busy");

            var building = _buildings.FirstOrDefault(b => b.Id == buildingId) ??
                throw new InvalidOperationException($"Building {buildingId} not found in village {Id}.");

            if (!buildingConfigs.TryGetValue(building.Type, out var config))
                throw new InvalidOperationException($"No config found for building type '{building.Type}'.");

            // Перевіряємо, що вистачає КОЖНОГО ресурсу (перш ніж списувати хоч щось)
            foreach (var line in config.Cost)
            {
                var need = line.Amount * building.Level.Value;
                var res = _resources.FirstOrDefault(r => r.ResourceType == line.Resource)
                    ?? throw new InvalidOperationException($"Resource '{line.Resource}' not found in village {Id}.");

                if (res.Amount < need)
                    throw new InvalidOperationException($"Not enough {line.Resource}: need {need}, have {res.Amount}.");
            }

            // Усе перевірено — тепер списуємо (жодного часткового списання при нестачі)
            foreach (var line in config.Cost)
            {
                var need = line.Amount * building.Level.Value;
                _resources.First(r => r.ResourceType == line.Resource).Amount -= need;
            }

            var buildMinutes = config.BaseBuildMinutes * Math.Pow(config.BuildTimeGrowth, building.Level.Value - 1);
            building.BeginUpgrade(TimeSpan.FromMinutes(buildMinutes));

            RaiseDomainEvent(new Events.BuildingUpgradeStarted(Id, PlayerId, building.Id, building.Type, ConstructionCompletesAt: building.ConstructionCompletesAt!.Value));
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

            RaiseDomainEvent(new Events.BuildingCollected(Id, PlayerId, building.Id, config.ProducesResource, collected, resource.Amount));
        }

        /// <summary>
        /// Завершує всі будівництва, чий час настав. Викликається сканером.
        /// Повертає кількість завершених (для логування).
        /// </summary>
        public int CompleteDueConstructions(DateTime utcNow)
        {
            var due = _buildings
                .Where(b => b.IsUnderConstruction && b.ConstructionCompletesAt <= utcNow)
                .ToList();

            foreach (var building in due)
            {
                building.CompleteConstruction();
                RaiseDomainEvent(new Events.BuildingUpgradeCompleted(Id, PlayerId, building.Id, building.Type, building.Level));
            }

            return due.Count;
        }

        /// <summary>Додає будівлю, перевіривши всі інваріанти та списавши вартість.</summary>
        /// <remarks>Перша будівля кожного типу безкоштовна.</remarks>
        public void AddBuilding(Building building)
        {
            _buildings.Add(building);
        }

    }
}

