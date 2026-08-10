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
        private readonly List<VillageZone> _zones = new();

        /// <summary>Зони села (тільки для читання).</summary>
        public IReadOnlyCollection<VillageZone> Zones => _zones.AsReadOnly();

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
        public IReadOnlyCollection<VillageResource> Resources => _resources.AsReadOnly();

        /// <summary>Координата X на карті світу.</summary>
        public int X { get; private set; }

        /// <summary>Координата Y на карті світу.</summary>
        public int Y { get; private set; }

        /// <summary>
        /// Створює нове село зі стартовим набором ресурсів (по нулю кожного).
        /// Перелік ресурсів приходить із конфіга — домен не знає конкретних назв.
        /// </summary>
        /// <param name="resourceKeys">Ключі ресурсів гри (з GameConfig.Resources).</param>
        public Village(Guid id, Guid playerId, string name, IEnumerable<string> resourceKeys,
            IEnumerable<(string Type, int Slots)> zones, int x, int y, int serverId = 1) : base(id)
        {
            PlayerId = playerId;
            Name = name;
            LastTickAt = DateTime.UtcNow;
            ServerId = serverId;
            X = x;
            Y = y;

            foreach (var key in resourceKeys)
                _resources.Add(new VillageResource (id, key));

            foreach (var (type, slots) in zones)
                _zones.Add(new VillageZone(Guid.NewGuid(), id, type, slots));
        }

        protected Village() { } // Для EF Core

        /// <summary>
        /// Тік виробництва: кожна будівля накопичує виробіток у власний буфер (до капу).
        /// Ресурси села змінюються лише при зборі (CollectFromBuilding).
        /// </summary>
        /// <param name="buildingConfigs">Конфігурації будівель з GameConfig (Key → BuildingConfig).</param>
        /// <param name="productionMultiplier">Множник від активних бустів (1.0 — без бусту).</param>
        public void TickProduction(Dictionary<string, BuildingConfig> buildingConfigs, DateTime utcNow, double productionMultiplier = 1.0)
        {
            var elapsed = utcNow - LastTickAt;

            foreach (var building in _buildings)
            {
                if (!buildingConfigs.TryGetValue(building.Type, out var config))
                    continue;
                if (config.ProducesResource is null)
                    continue;

                var rate = (int)Math.Round(config.BaseProductionPerMinute * productionMultiplier);
                building.AccumulateProduction(rate, config.BaseStorage, config.StorageGrowth, elapsed);
            }

            LastTickAt = utcNow;
        }

        /// <summary>
        /// Збирає накопичене з буфера будівлі у ресурси села.
        /// </summary>
        /// <param name="buildingId">Ідентифікатор будівлі.</param>
        /// <param name="buildingConfigs">Конфігурації будівель з GameConfig.</param>
        /// <exception cref="InvalidOperationException">Якщо будівля або її конфіг не знайдені.</exception>
        public void CollectFromBuilding(Guid buildingId, Dictionary<string, BuildingConfig> buildingConfigs, DateTime utcNow)
        {
            var building = _buildings.FirstOrDefault(b => b.Id == buildingId)
                ?? throw new InvalidOperationException($"Building {buildingId} not found in village {Id}.");

            if (!buildingConfigs.TryGetValue(building.Type, out var config))
                throw new InvalidOperationException($"No config found for building type '{building.Type}'.");

            if (config.ProducesResource is null)
                return; // невиробнича будівля — нічого збирати

            var collected = building.Collect(utcNow);
            if (collected == 0)
                return;// порожній буфер — не подія і не зміна стану

            var resource = _resources.FirstOrDefault(r => r.ResourceType == config.ProducesResource);
            if (resource is null)
            {
                resource = new VillageResource (Id, config.ProducesResource);
                _resources.Add(resource);
            }
            resource.Add(collected);


            RaiseDomainEvent(new Events.BuildingCollected(Id, PlayerId, building.Id, config.ProducesResource, collected, resource.Amount));
        }

        /// <summary>
        /// Створює будівлю, перевіривши інваріанти: розблокування Ратушею,
        /// відповідність зоні, вільний слот, вартість (перша будівля типу безкоштовна).
        /// </summary>
        /// <returns>Id створеної будівлі.</returns>
        public Guid AddBuilding(string buildingType, Dictionary<string, BuildingConfig> buildingConfigs)
        {
            if (!buildingConfigs.TryGetValue(buildingType, out var config))
                throw new InvalidOperationException($"Unknown building type '{buildingType}'.");

            // 1. Розблокування за рівнем головної будівлі (яка саме — вирішує конфіг)
            var mainBuildingKey = buildingConfigs.Values.FirstOrDefault(c => c.IsMainBuilding)?.Key;
            var mainBuildingLevel = mainBuildingKey is null
                ? 0
                : _buildings.FirstOrDefault(b => b.Type == mainBuildingKey)?.Level.Value ?? 0;

            if (mainBuildingLevel < config.RequiresMainBuildingLevel)
                throw new InvalidOperationException(
                    $"Building '{buildingType}' requires main building level {config.RequiresMainBuildingLevel}.");

            // 2. Зона: відповідність і вільний слот (null — поза зонами, без ліміту)
            if (config.AllowedZone is not null)
            {
                var zone = _zones.FirstOrDefault(z => z.Type == config.AllowedZone)
                    ?? throw new InvalidOperationException($"Village has no '{config.AllowedZone}' zone.");

                // Зайняті слоти = будівлі, чий тип належить цій зоні (за конфігом)
                var used = _buildings.Count(b =>
                    buildingConfigs.TryGetValue(b.Type, out var c) && c.AllowedZone == config.AllowedZone);

                if (used >= zone.Slots)
                    throw new InvalidOperationException($"No free slots in '{config.AllowedZone}' zone.");
            }

            // 3. Вартість: перша будівля типу безкоштовна, наступні — за конфігом
            if (_buildings.Any(b => b.Type == buildingType))
            {
                foreach (var line in config.Cost)
                {
                    var res = _resources.FirstOrDefault(r => r.ResourceType == line.Resource)
                        ?? throw new InvalidOperationException($"Resource '{line.Resource}' not found in village {Id}.");
                    if (res.Amount < line.Amount)
                        throw new InvalidOperationException($"Not enough {line.Resource}: need {line.Amount}, have {res.Amount}.");
                }
                foreach (var line in config.Cost)
                    _resources.First(r => r.ResourceType == line.Resource).Subtract(line.Amount);
            }

            var building = new Building(Guid.NewGuid(), Id, buildingType);
            _buildings.Add(building);

            if (config.PopulationPerLevel > 0 && config.PopulationResource is not null)
                AddPopulation(config.PopulationResource, config.PopulationPerLevel); //будівля 1-го рівня одразу дає населення

            return building.Id;
        }

        /// <summary>
        /// Списує вартість із ресурсів села: спершу перевіряє всі позиції,
        /// потім списує (все або нічого — без часткового списання).
        /// </summary>
        /// <param name="cost">Позиції вартості (ресурс → кількість за одиницю).</param>
        /// <param name="multiplier">Множник (кількість юнітів, рівень будівлі тощо).</param>
        public void ChargeCost(List<ResourceCost> cost, int multiplier = 1)
        {
            foreach (var line in cost)
            {
                var need = line.Amount * multiplier;
                var res = _resources.FirstOrDefault(r => r.ResourceType == line.Resource)
                    ?? throw new InvalidOperationException($"Resource '{line.Resource}' not found in village {Id}.");

                if (res.Amount < need)
                    throw new InvalidOperationException($"Not enough {line.Resource}: need {need}, have {res.Amount}.");
            }

            foreach (var line in cost)
                _resources.First(r => r.ResourceType == line.Resource).Subtract(line.Amount * multiplier);
        }

        /// <summary>
        /// Розпочати апгрейд будівлі: перевіряє ліміт будівельників і вартість,
        /// списує ресурси та ставить будівлю в стан будівництва.
        /// Рівень підніметься при завершенні (CompleteDueConstructions).
        /// </summary>
        public void BeginBuildingUpgrade(Guid buildingId, Dictionary<string, BuildingConfig> buildingConfigs, DateTime utcNow, int builderCount = 1)
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
                _resources.First(r => r.ResourceType == line.Resource).Subtract(line.Amount * building.Level.Value);
            }

            var buildMinutes = config.BaseBuildMinutes * Math.Pow(config.BuildTimeGrowth, building.Level.Value - 1);
            building.BeginUpgrade(TimeSpan.FromMinutes(buildMinutes), utcNow);

            RaiseDomainEvent(new Events.BuildingUpgradeStarted(Id, PlayerId, building.Id, building.Type, ConstructionCompletesAt: building.ConstructionCompletesAt!.Value));
        }

        /// <summary>
        /// Завершує всі будівництва, чий час настав. Викликається сканером.
        /// Повертає кількість завершених (для логування).
        /// </summary>
        public int CompleteDueConstructions(DateTime utcNow, Dictionary<string, BuildingConfig> buildingConfigs)
        {
            var due = _buildings
                .Where(b => b.IsUnderConstruction && b.ConstructionCompletesAt <= utcNow)
                .ToList();

            foreach (var building in due)
            {
                building.CompleteConstruction();

                if (buildingConfigs.TryGetValue(building.Type, out var config) && config.PopulationPerLevel > 0 && config.PopulationResource is not null)
                    AddPopulation(config.PopulationResource, config.PopulationPerLevel); //апгрейд житлової будівлі додає населення


                RaiseDomainEvent(new Events.BuildingUpgradeCompleted(Id, PlayerId, building.Id, building.Type, building.Level));
            }

            return due.Count;
        }

        /// <summary>Поповнює ресурс-місткість (від будівництва/апгрейду житлової будівлі).</summary>
        private void AddPopulation(string resourceKey, int amount)
        {
            var resource = _resources.FirstOrDefault(r => r.ResourceType == resourceKey);
            if (resource is null)
            {
                resource = new VillageResource(Id, resourceKey);
                _resources.Add(resource);
            }
            resource.Add(amount);
        }

        /// <summary>
        /// Нараховує ресурси в село (нагорода за бій, подарунок тощо).
        /// Невідомі ресурси створюються на льоту.
        /// </summary>
        public void GrantResources(List<ResourceCost> rewards)
        {
            foreach (var line in rewards)
            {
                if (line.Amount <= 0)
                    continue;

                var resource = _resources.FirstOrDefault(r => r.ResourceType == line.Resource);
                if (resource is null)
                {
                    resource = new VillageResource(Id, line.Resource);
                    _resources.Add(resource);
                }

                resource.Add(line.Amount);
            }
        }

        /// <summary>Чи є в селі готова (не в процесі будівництва) будівля вказаного типу.</summary>
        public bool HasBuilding(string buildingType)
            => _buildings.Any(b => b.Type == buildingType && !b.IsUnderConstruction);
    }
}

