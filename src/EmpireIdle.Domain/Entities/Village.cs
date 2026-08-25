using EmpireIdle.Domain.Events;
using EmpireIdle.Domain.Exceptions;
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
        private readonly List<VillageResource> _resources = new();

        /// <summary>Назва села.</summary>
        public string Name { get; private set; } = null!;

        /// <summary>Ідентифікатор ігрового сервера, на якому живе цей гравець.</summary>
        public int ServerId { get; private set; }

        /// <summary>Ідентифікатор власника.</summary>
        public Guid PlayerId { get; private set; }

        /// <summary>Будівлі села (тільки для читання).</summary>
        public IReadOnlyCollection<Building> Buildings => _buildings.AsReadOnly();

        /// <summary>Всі ресурси села. Ключ — тип ресурсу.</summary>
        public IReadOnlyCollection<VillageResource> Resources => _resources.AsReadOnly();

        /// <summary>Координата X на карті світу.</summary>
        public int X { get; private set; }

        /// <summary>Координата Y на карті світу.</summary>
        public int Y { get; private set; }

        /// <summary>
        /// Момент останньої мутації агрегату. Змінюється навіть тоді, коли
        /// правились лише дочірні рядки — інакше токен паралелізму на корені
        /// не спрацював би, бо EF не оновив би рядок кореня.
        /// </summary>
        public DateTime UpdatedAt { get; private set; }

        /// <summary>
        /// Створює нове село зі стартовим набором ресурсів (по нулю кожного).
        /// Перелік ресурсів приходить із конфіга — домен не знає конкретних назв.
        /// </summary>
        /// <param name="resourceKeys">Ключі ресурсів гри (з GameConfig.Resources).</param>
        public Village(Guid id, Guid playerId, string name, IEnumerable<string> resourceKeys, int x, int y, int serverId = 1) : base(id)
        {
            PlayerId = playerId;
            Name = name;
            ServerId = serverId;
            X = x;
            Y = y;

            foreach (var key in resourceKeys)
                _resources.Add(new VillageResource(id, key));
        }

        protected Village() { } // Для EF Core

        /// <summary>
        /// Переносить накопичене з буфера будівлі у сховище села.
        /// Буфер спорожнюється повністю; те, що не вмістилось у сховище, згорає.
        /// </summary>
        /// <param name="buildingId">Ідентифікатор будівлі.</param>
        /// <param name="buildingConfigs">Конфігурації будівель з GameConfig.</param>
        /// <param name="utcNow">Момент збору.</param>
        /// <param name="boost">Вікно дії буста виробництва.</param>
        /// <exception cref="EntityNotFoundException">Будівлі з таким Id у селі немає.</exception>
        /// <exception cref="InvalidOperationException">Тип збудованої будівлі зник із конфіга — поломка розгортання.</exception>
        public void CollectFromBuilding(Guid buildingId, IReadOnlyDictionary<string, BuildingConfig> buildingConfigs,
            DateTime utcNow, ProductionBoost boost)
        {
            var building = _buildings.FirstOrDefault(b => b.Id == buildingId)
                ?? throw new EntityNotFoundException("Building", buildingId);

            // Не доменне правило: будівля вже стоїть, а конфіг її типу зник —
            // це битий конфіг, і має бути 500, а не 400
            if (!buildingConfigs.TryGetValue(building.Type, out var config))
                throw new InvalidOperationException($"No config found for building type '{building.Type}'.");

            if (config.ProducesResource is null)
                return;

            var collected = building.Collect(config, utcNow, boost);
            if (collected == 0)
                return; // порожній буфер — не подія і не зміна стану

            var resource = _resources.FirstOrDefault(r => r.ResourceType == config.ProducesResource);
            if (resource is null)
            {
                resource = new VillageResource(Id, config.ProducesResource);
                _resources.Add(resource);
            }

            // Склад приймає скільки влізе, решта згорає
            var cap = StorageCapFor(config.ProducesResource, buildingConfigs);
            var accepted = resource.AddUpTo(collected, cap);

            RaiseDomainEvent(new Events.BuildingCollected(
                Id, PlayerId, building.Id, config.ProducesResource, accepted, resource.Amount, utcNow));
            Touch(utcNow);
        }

        /// <summary>
        /// Ставить будівлю 1 рівня. Системна операція, не дія гравця:
        /// селище створюється повним, а нові типи розкочуються на всі села одразу.
        /// Вартості немає — гравець платить лише за апгрейди.
        /// </summary>
        /// <returns>Id створеної будівлі.</returns>
        /// <exception cref="EntityNotFoundException">Невідомий тип будівлі.</exception>
        /// <exception cref="AlreadyExistsException">Будівля цього типу вже стоїть.</exception>
        public Guid AddBuilding(string buildingType, IReadOnlyDictionary<string, BuildingConfig> buildingConfigs, DateTime utcNow)
        {
            if (!buildingConfigs.ContainsKey(buildingType))
                throw new EntityNotFoundException("Building type", buildingType);

            if (_buildings.Any(b => b.Type == buildingType))
                throw new AlreadyExistsException("Building", buildingType);

            var building = new Building(Guid.NewGuid(), Id, buildingType);
            _buildings.Add(building);

            Touch(utcNow);
            return building.Id;
        }

        /// <summary>
        /// Списує вартість із ресурсів села: спершу перевіряє всі позиції,
        /// потім списує (все або нічого — без часткового списання).
        /// </summary>
        /// <param name="cost">Позиції вартості (ресурс → кількість за одиницю).</param>
        /// <param name="utcNow">Час операції — фіксує момент мутації агрегату.</param>
        /// <param name="multiplier">Множник (кількість юнітів, рівень будівлі тощо).</param>
        /// <exception cref="NotEnoughResourcesException">Не вистачає ресурсів.</exception>
        public void ChargeCost(List<ResourceCost> cost, DateTime utcNow, int multiplier = 1)
        {
            foreach (var line in cost)
            {
                var need = line.Amount * multiplier;
                var res = _resources.FirstOrDefault(r => r.ResourceType == line.Resource)
                    ?? throw new InvalidOperationException($"Resource '{line.Resource}' not found in village {Id}.");

                if (res.Amount < need)
                    throw new NotEnoughResourcesException(line.Resource, need, res.Amount);
            }

            foreach (var line in cost)
                _resources.First(r => r.ResourceType == line.Resource).Subtract(line.Amount * multiplier);

            Touch(utcNow);
        }

        /// <summary>
        /// Розпочати апгрейд будівлі: перевіряє ліміт будівельників і вартість,
        /// списує ресурси та ставить будівлю в стан будівництва.
        /// Рівень підніметься при завершенні (CompleteDueConstructions).
        /// </summary>
        /// <exception cref="RequirementNotMetException">Усі будівельники зайняті.</exception>
        /// <exception cref="EntityNotFoundException">Будівлі з таким Id у селі немає.</exception>
        /// <exception cref="NotEnoughResourcesException">Не вистачає ресурсів.</exception>
        public void BeginBuildingUpgrade(Guid buildingId, IReadOnlyDictionary<string, BuildingConfig> buildingConfigs,
            DateTime utcNow, ProductionBoost boost, string mainBuildingKey, int serverLevel, int levelsPerTier,
            int builderCount = 1)
        {
            if (_buildings.Count(b => b.IsUnderConstruction) >= builderCount)
                throw new RequirementNotMetException("All builders are busy.");

            var building = _buildings.FirstOrDefault(b => b.Id == buildingId) ??
                throw new EntityNotFoundException("Building", buildingId);

            if (!buildingConfigs.TryGetValue(building.Type, out var config))
                throw new InvalidOperationException($"No config found for building type '{building.Type}'.");

            EnsureTierAllows(building, config, buildingConfigs, mainBuildingKey, serverLevel, levelsPerTier);

            // Перевіряємо, що вистачає КОЖНОГО ресурсу (перш ніж списувати хоч щось)
            foreach (var line in config.Cost)
            {
                var need = ProgressionCurves.UpgradeCost(line.Amount, building.Level.Value, config.UpgradeCostGrowth);
                var res = _resources.FirstOrDefault(r => r.ResourceType == line.Resource)
                    ?? throw new InvalidOperationException($"Resource '{line.Resource}' not found in village {Id}.");

                if (res.Amount < need)
                    throw new NotEnoughResourcesException(line.Resource, need, res.Amount);
            }

            // Усе перевірено — тепер списуємо (жодного часткового списання при нестачі)
            foreach (var line in config.Cost)
            {
                _resources.First(r => r.ResourceType == line.Resource)
                    .Subtract(ProgressionCurves.UpgradeCost(line.Amount, building.Level.Value, config.UpgradeCostGrowth));
            }

            var buildMinutes = config.BaseBuildMinutes * Math.Pow(config.BuildTimeGrowth, building.Level.Value - 1);
            building.BeginUpgrade(config, TimeSpan.FromMinutes(buildMinutes), utcNow, boost);

            RaiseDomainEvent(new Events.BuildingUpgradeStarted(Id, PlayerId, building.Id,
                building.Type, ConstructionCompletesAt: building.ConstructionCompletesAt!.Value, utcNow));
            Touch(utcNow);
        }

        /// <summary>
        /// Завершує всі будівництва, чий час настав. Викликається сканером.
        /// Повертає кількість завершених (для логування).
        /// </summary>
        public int CompleteDueConstructions(DateTime utcNow, IReadOnlyDictionary<string, BuildingConfig> buildingConfigs)
        {
            var due = _buildings
                .Where(b => b.IsUnderConstruction && b.ConstructionCompletesAt <= utcNow)
                .ToList();

            foreach (var building in due)
            {
                building.CompleteConstruction(utcNow);
                RaiseDomainEvent(new Events.BuildingUpgradeCompleted(Id, PlayerId, building.Id, building.Type, building.Level, utcNow));
            }

            Touch(utcNow);
            return due.Count;
        }

        /// <summary>
        /// Нараховує ресурси в село (нагорода за бій, подарунок тощо).
        /// Невідомі ресурси створюються на льоту.
        /// </summary>
        public void GrantResources(List<ResourceCost> rewards, DateTime utcNow)
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
            Touch(utcNow);
        }

        /// <summary>Чи є в селі готова (не в процесі будівництва) будівля вказаного типу.</summary>
        public bool HasBuilding(string buildingType)
            => _buildings.Any(b => b.Type == buildingType && !b.IsUnderConstruction);

        /// <summary>Нараховує стартові ресурси при заснуванні поселення.</summary>
        public void GrantStartingResources(IReadOnlyDictionary<string, int> amounts, DateTime utcNow)
        {
            foreach (var (key, amount) in amounts)
            {
                // Ключ приходить із конфіга, не від гравця — розбіжність означає битий конфіг
                var resource = _resources.FirstOrDefault(r => r.ResourceType == key)
                    ?? throw new InvalidOperationException($"Village has no '{key}' resource.");

                resource.Add(amount);
            }
            Touch(utcNow);
        }

        /// <summary>
        /// Фіксує буфери всіх виробничих будівель на поточний момент.
        /// Викликається перед зміною множника: інакше вироблене за старим
        /// бустом порахувалося б за новим (або без нього).
        /// </summary>
        public void MaterializeProduction(IReadOnlyDictionary<string, BuildingConfig> buildingConfigs,
            DateTime utcNow, ProductionBoost boost)
        {
            foreach (var building in _buildings)
            {
                if (buildingConfigs.TryGetValue(building.Type, out var config) && config.ProducesResource is not null)
                    building.Materialize(config, utcNow, boost);
            }
            Touch(utcNow);
        }

        /// <summary>
        /// Нараховує ресурс від нагороди. Повертає, скільки реально зараховано:
        /// надлишок понад сумарний кап складів згорає.
        /// </summary>
        public int GrantResource(string resourceKey, int amount, IReadOnlyDictionary<string, BuildingConfig> buildingConfigs, DateTime utcNow)
        {
            if (amount <= 0)
                return 0;

            var resource = _resources.FirstOrDefault(r => r.ResourceType == resourceKey)
                ?? throw new InvalidOperationException($"Village has no '{resourceKey}' resource.");

            var cap = StorageCapFor(resourceKey, buildingConfigs);
            var granted = Math.Max(0, Math.Min(amount, cap - resource.Amount));

            if (granted > 0)
                resource.Add(granted);

            Touch(utcNow);
            return granted;
        }

        /// <summary>
        /// Місткість сховища для ресурсу. Золото зберігається в банку,
        /// решта — на складі: два різні сховища, два різні рівні.
        /// Будівля під будівництвом місткості не дає.
        /// </summary>
        public int StorageCapFor(string resourceKey, IReadOnlyDictionary<string, BuildingConfig> buildingConfigs)
        {
            var storageKey = buildingConfigs.Values
                .FirstOrDefault(c => c.StoresResources?.Contains(resourceKey) == true)?.Key;

            if (storageKey is null)
                return int.MaxValue;

            var storage = _buildings.FirstOrDefault(b => b.Type == storageKey && !b.IsUnderConstruction);

            if (storage is null || !buildingConfigs.TryGetValue(storageKey, out var storageConfig))
                return 0;

            return storageConfig.BaseStorage * storage.Level.Value;
        }

        private void Touch(DateTime utcNow) => UpdatedAt = utcNow;

        /// <summary>
        /// Перевіряє три незалежні умови апгрейду.
        ///
        /// A. Стеля від рівня сервера — контент відкривається для всіх одночасно.
        /// B. Темп усередині тіру (тільки ратуша) — за межу тіру не пускаємо,
        ///    поки решта селища не підтягнулась. Будівлі під туманом не рахуються:
        ///    інакше гравець мусив би прокачати те, чого ще не бачить.
        /// C. Рівномірність — жодна будівля не переростає ратушу.
        ///
        /// Будівля в процесі апгрейду рахується за ПОТОЧНИМ рівнем: інакше
        /// можна запустити десять апгрейдів одночасно й обійти умову B.
        /// </summary>
        private void EnsureTierAllows(Building building, BuildingConfig config,
            IReadOnlyDictionary<string, BuildingConfig> buildingConfigs,
            string mainBuildingKey, int serverLevel, int levelsPerTier)
        {
            var targetLevel = building.Level.Value + 1;
            var isMainBuilding = building.Type == mainBuildingKey;

            // A
            var ceiling = serverLevel * levelsPerTier;
            if (targetLevel > ceiling)
                throw new RequirementNotMetException(
                    $"Server level {serverLevel} allows buildings up to level {ceiling}.");

            var townhall = _buildings.FirstOrDefault(b => b.Type == mainBuildingKey)
                ?? throw new InvalidOperationException($"Village {Id} has no '{mainBuildingKey}'.");

            // C
            if (!isMainBuilding && targetLevel > townhall.Level.Value)
                throw new RequirementNotMetException(
                    $"'{building.Type}' cannot exceed main building level {townhall.Level.Value}.");

            // B — лише на межі тіру
            if (!isMainBuilding || building.Level.Value % levelsPerTier != 0)
                return;

            var required = building.Level.Value;

            var lagging = _buildings
                .Where(b => b.Type != mainBuildingKey
                            && buildingConfigs.TryGetValue(b.Type, out var c)
                            && c.RequiresMainBuildingLevel <= townhall.Level.Value
                            && b.Level.Value < required)
                .Select(b => b.Type)
                .ToList();

            if (lagging.Count > 0)
                throw new RequirementNotMetException(
                    $"Raise the whole village to level {required} first: {string.Join(", ", lagging)}.");
        }

        /// <summary>
        /// Чи відкрита будівля гравцю. Під туманом вона фізично існує й може
        /// навіть будуватись, але гравець її не бачить і не взаємодіє.
        ///
        /// Стан не зберігається: це функція від рівня ратуші й конфіга.
        /// Зберігати означало б тримати похідне значення, яке розсинхронізується
        /// з конфігом при першому ж ребалансі порогів.
        /// </summary>
        public bool IsUnlocked(string buildingType, IReadOnlyDictionary<string, BuildingConfig> buildingConfigs,
            string mainBuildingKey)
        {
            if (!buildingConfigs.TryGetValue(buildingType, out var config))
                return false;

            var townhall = _buildings.FirstOrDefault(b => b.Type == mainBuildingKey);

            return townhall is not null && config.RequiresMainBuildingLevel <= townhall.Level.Value;
        }

        /// <summary>
        /// Множник до сили оборони від укріплень. 1.0 — стін немає.
        /// Рахується від селища, а не з гарнізону: стіни належать місту,
        /// і підкріплення клану ними теж прикриті.
        /// </summary>
        public double DefenceMultiplier(IReadOnlyDictionary<string, BuildingConfig> buildingConfigs)
        {
            var bonus = _buildings
                .Where(b => !b.IsUnderConstruction
                            && buildingConfigs.TryGetValue(b.Type, out var c)
                            && c.DefenceBonusPerLevel > 0)
                .Sum(b => buildingConfigs[b.Type].DefenceBonusPerLevel * b.Level.Value);

            return 1.0 + bonus;
        }
    }
}
