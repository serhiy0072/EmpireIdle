namespace EmpireIdle.Application.Catalog
{
    /// <summary>
    /// Зріз конфіга для інтерфейсу: назви, ранги, класи й стати.
    ///
    /// Це не дублікат конфіга, а рівно те, що показують екрани. Без нього
    /// клієнт або вигадує назви з ключів, або тримає власну копію JSON,
    /// яка відстане від сервера на першій же правці балансу.
    /// </summary>
    /// <param name="ArtifactSlots">Артефактні слоти героя за типом у порядку номерів — клієнт малює саме їх.</param>
    /// <param name="MaxEnhancement">Стеля заточки — після неї кнопка «Заточити» зникає.</param>
    /// <param name="RepairGemsBase">Ремонт зброї в gems: база плюс RepairGemsPerLevel за кожен рівень заточки.</param>
    /// <param name="MapSize">Сторона світової мапи в клітинах — клієнт малює землю до її краю.</param>
    /// <param name="MainBuildingKey">Ключ головної будівлі: її рівень — «рівень гравця» в шапці.</param>
    /// <param name="Language">Мова назв у цьому каталозі (ISO 639-1).</param>
    /// <param name="Version">Хеш вмісту. Той самий рядок іде в ETag.</param>
    public record CatalogResponse(
        IReadOnlyList<CatalogHero> Heroes,
        IReadOnlyList<CatalogItem> Items,
        IReadOnlyList<CatalogResource> Resources,
        IReadOnlyList<CatalogBuilding> Buildings,
        IReadOnlyList<CatalogUnit> Units,
        IReadOnlyList<CatalogArtifactSet> ArtifactSets,
        IReadOnlyList<string> HeroClasses,
        int MaxConstellation,
        int MaxTier,
        int MaxUnitLevel,
        int HealGemsPerUnit,
        IReadOnlyList<CatalogArtifactSlot> ArtifactSlots,
        int MaxEnhancement,
        int RepairGemsBase,
        int RepairGemsPerLevel,
        int MapSize,
        string MainBuildingKey,
        string Language,
        string Version);

    /// <param name="Rank">Ранг рядком: "Common", "Rare", "Unique".</param>
    public record CatalogHero(
        string Key,
        string DisplayName,
        string Class,
        string Rank,
        string? Description,
        double Speed,
        int SummonShards,
        int ShardPriceGold,
        IReadOnlyDictionary<string, double> BaseStats,
        IReadOnlyDictionary<string, double> StatGrowth,
        IReadOnlyList<CatalogPassive> Passives);

    public record CatalogPassive(
        string Key,
        string DisplayName,
        int UnlockConstellation,
        string Target,
        string Stat,
        double BasePercent,
        double PercentPerConstellation);

    /// <param name="Slot">"Weapon", "Artifact" або null для стакового предмета.</param>
    /// <param name="ArtifactSlot">Тип слота артефакта (ключ з ArtifactSlots); null — не артефакт.</param>
    /// <param name="Tradeable">Стаковий предмет можна виставити на ринок; спорядження торгується завжди.</param>
    public record CatalogItem(
        string Key,
        string DisplayName,
        string Description,
        string Rarity,
        string Type,
        string? Slot,
        IReadOnlyList<string> WeaponClasses,
        IReadOnlyDictionary<string, double> BaseStats,
        string? SetKey,
        string? ArtifactSlot,
        int PriceGold,
        bool Tradeable);

    /// <summary>Тип артефактного слота: намисто, корона, кільце, пояс.</summary>
    public record CatalogArtifactSlot(string Key, string DisplayName);

    /// <summary>
    /// Родина наборів артефактів — одна на данж, у трьох рідкостях.
    /// Екран наборів малює її цілком: звідки падає, що дає й наскільки сильна.
    /// </summary>
    /// <param name="Tier">Рівень набору: вищий — сильніші стати.</param>
    /// <param name="StatMultiplier">Множник статів за рівнем — те, що рівень означає в числах.</param>
    /// <param name="FocusStats">Характерні стати: частіше випадають, і бонус набору в них.</param>
    /// <param name="Dungeon">Данж, з якого падає набір; null — набір поза данжами.</param>
    public record CatalogArtifactSet(
        string Key,
        string DisplayName,
        int Tier,
        double StatMultiplier,
        IReadOnlyList<string> FocusStats,
        CatalogSetSource? Dungeon,
        IReadOnlyList<CatalogSetRarity> Rarities);

    /// <param name="RequiresMainBuildingLevel">Рівень ратуші, з якого данж відкритий.</param>
    public record CatalogSetSource(string Key, string DisplayName, int RequiresMainBuildingLevel);

    /// <summary>Набір однієї рідкості: скільки частин треба вдягнути, що дає і з яких предметів складається.</summary>
    /// <param name="Rarity">"Common", "Rare", "Unique" — як у CatalogItem.</param>
    /// <param name="SetKey">Збігається з SetKey предметів цієї рідкості.</param>
    /// <param name="PieceKeys">Ключі частин; назви — у Items.</param>
    public record CatalogSetRarity(
        string Rarity,
        string SetKey,
        int RequiredPieces,
        IReadOnlyDictionary<string, double> Bonus,
        IReadOnlyList<string> PieceKeys);

    public record CatalogResource(string Key, string DisplayName, string Icon);

    /// <param name="Position">Місце на плані села. null — будівля не малюється на мапі.</param>
    /// <param name="RequiresMainBuildingLevel">Мінімальний рівень ратуші для розблокування (туман війни).</param>
    public record CatalogBuilding(string Key, string DisplayName, string? ProducesResource, CatalogPosition? Position, int RequiresMainBuildingLevel);

    /// <summary>Координати на плані села у відсотках: 0–100 по кожній осі.</summary>
    public record CatalogPosition(double X, double Y);

    /// <summary>Вартість одного юніта в конкретному ресурсі.</summary>
    public record CatalogUnitCost(string Resource, int Amount);

    /// <param name="RequiresBuilding">Будівля, потрібна для тренування; null — без вимог.</param>
    public record CatalogUnit(
        string Key,
        string DisplayName,
        string? RequiresBuilding,
        int RequiresBuildingLevel,
        int BaseTrainMinutes,
        double LevelUpCostGrowth,
        IReadOnlyList<CatalogUnitCost> Cost);
}
