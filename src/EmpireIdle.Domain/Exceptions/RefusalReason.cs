namespace EmpireIdle.Domain.Exceptions
{
    /// <summary>
    /// Причина відмови, яку гравець може отримати чесною грою: стабільний ключ
    /// і назви параметрів. Текст для гравця живе на клієнті за цим ключем —
    /// Message винятку лишається англійським і йде лише в логи.
    ///
    /// Не кожна відмова має причину: та, до якої веде лише баг клієнта
    /// (невідомий ключ предмета, чужий id), обходиться без неї, і гравець
    /// бачить нейтральний текст.
    /// </summary>
    public sealed class RefusalReason
    {
        /// <summary>Ключ у форматі «модуль.суть», напр. «dungeon.levelLocked». Контракт із клієнтом.</summary>
        public string Key { get; }

        /// <summary>Назви параметрів у тому порядку, в якому їх передає виняток.</summary>
        public IReadOnlyList<string> ArgNames { get; }

        public RefusalReason(string key, params string[] argNames)
        {
            Key = key;
            ArgNames = argNames;
        }

        public override string ToString() => Key;
    }

    /// <summary>
    /// Реєстр причин відмов. Контрактний тест вивантажує його в refusals/reasons.json,
    /// а клієнт не збереться, доки кожен ключ звідти не має українського тексту.
    /// </summary>
    public static class RefusalReasons
    {
        // ---------- Спільні ----------

        /// <summary>Дія вимагає добудованої будівлі; building — її назва для гравця.</summary>
        public static readonly RefusalReason BuildingRequired = new("common.buildingRequired", "building");

        // ---------- Акаунт ----------

        /// <summary>Identity відхилив реєстрацію; codes — його коди через кому, текст за кожним дає клієнт.</summary>
        public static readonly RefusalReason AuthRegistrationRejected = new("auth.registrationRejected", "codes");

        // ---------- Село й будівлі ----------

        /// <summary>resource — ключ ресурсу: назву в потрібному відмінку підставляє клієнт.</summary>
        public static readonly RefusalReason VillageStorageFull = new("village.storageFull", "resource");

        public static readonly RefusalReason VillageAlreadyThere = new("village.alreadyThere");

        /// <summary>Правило A: стеля будівель від рівня світу.</summary>
        public static readonly RefusalReason BuildingServerCeiling = new("building.serverCeiling", "serverLevel", "ceiling");

        /// <summary>Правило C: будівля не переростає ратушу.</summary>
        public static readonly RefusalReason BuildingTownHallCeiling = new("building.townHallCeiling", "building", "level");

        /// <summary>Правило B: ратуша не переходить тір, поки відкриті будівлі відстають; buildings — їхні назви через кому.</summary>
        public static readonly RefusalReason BuildingVillageLagging = new("building.villageLagging", "level", "buildings");

        public static readonly RefusalReason BuildingUnderConstruction = new("building.underConstruction");

        /// <summary>Прискорення прийшло, коли таймер уже добіг кінця.</summary>
        public static readonly RefusalReason BuildingAlreadyCompleted = new("building.alreadyCompleted");

        // ---------- Герої ----------

        public static readonly RefusalReason HeroOnTheMove = new("hero.onTheMove");

        public static readonly RefusalReason HeroLevelCeiling = new("hero.levelCeiling", "hero", "ceiling");

        /// <summary>Зала героїв тренує одного героя за раз.</summary>
        public static readonly RefusalReason HeroTrainingBusy = new("hero.trainingBusy");

        public static readonly RefusalReason HeroWorldLevelRequired = new("hero.worldLevelRequired", "required", "current");

        public static readonly RefusalReason HeroEvolutionItemRequired = new("hero.evolutionItemRequired", "item");

        public static readonly RefusalReason HeroMaxTier = new("hero.maxTier", "tier");

        public static readonly RefusalReason HeroNotEnoughShards = new("hero.notEnoughShards", "hero", "need", "have");

        // ---------- Спорядження ----------

        public static readonly RefusalReason EquipmentMaxEnhancement = new("equipment.maxEnhancement", "max");

        public static readonly RefusalReason EquipmentBroken = new("equipment.broken");

        /// <summary>Такий самий предмет уже вдягнений на цього героя.</summary>
        public static readonly RefusalReason EquipmentAlreadyEquipped = new("equipment.alreadyEquipped", "item");

        public static readonly RefusalReason EquipmentClassMismatch = new("equipment.classMismatch", "weapon");

        // ---------- Данжі ----------

        public static readonly RefusalReason DungeonTownHallRequired = new("dungeon.townHallRequired", "dungeon", "level");

        public static readonly RefusalReason DungeonLevelLocked = new("dungeon.levelLocked", "level", "previous");

        public static readonly RefusalReason DungeonHeroWounded = new("dungeon.heroWounded", "hero");

        /// <summary>Незавершений забіг уже є — найчастіше його почали в іншій вкладці.</summary>
        public static readonly RefusalReason DungeonRunInProgress = new("dungeon.runInProgress");

        /// <summary>Ціль закрита передньою лінією або провокацією.</summary>
        public static readonly RefusalReason DungeonTargetUnreachable = new("dungeon.targetUnreachable");
    }
}
