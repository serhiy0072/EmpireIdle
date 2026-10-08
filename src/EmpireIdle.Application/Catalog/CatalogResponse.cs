namespace EmpireIdle.Application.Catalog
{
    /// <summary>
    /// Зріз конфіга для інтерфейсу: назви, ранги, класи й стати.
    ///
    /// Це не дублікат конфіга, а рівно те, що показують екрани. Без нього
    /// клієнт або вигадує назви з ключів, або тримає власну копію JSON,
    /// яка відстане від сервера на першій же правці балансу.
    /// </summary>
    /// <param name="SkillBooks">Яка книга (предмет) піднімає вміння героя якої ролі, рідкості й половини.</param>
    /// <param name="MaxSkillLevel">Стеля рівня вміння; доступний рівень — зірки + 1, не вище за неї (GDD §6.1).</param>
    /// <param name="ArtifactSlots">Артефактні слоти героя за типом у порядку номерів — клієнт малює саме їх.</param>
    /// <param name="MaxEnhancement">Стеля заточки — після неї кнопка «Заточити» зникає.</param>
    /// <param name="RepairGemsBase">Ремонт зброї в gems: база плюс RepairGemsPerLevel за кожен рівень заточки.</param>
    /// <param name="SpeedUp">
    /// Ціна прискорення таймерів. Клієнт перераховує її щосекунди разом із відліком —
    /// знімок із запиту застарівав і показував більше, ніж спише сервер.
    /// </param>
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
        IReadOnlyList<CatalogBeast> Beasts,
        IReadOnlyList<CatalogArtifactSet> ArtifactSets,
        IReadOnlyList<string> HeroClasses,
        int MaxStars,
        int PartsPerStar,
        int MaxSkillLevel,
        IReadOnlyList<CatalogSkillBook> SkillBooks,
        double StarPartBonus,
        int SummonShards,
        int MaxTier,
        int MaxUnitLevel,
        int HealGemsPerUnit,
        IReadOnlyList<CatalogArtifactSlot> ArtifactSlots,
        int MaxEnhancement,
        int RepairGemsBase,
        int RepairGemsPerLevel,
        CatalogSpeedUp SpeedUp,
        int MapSize,
        string MainBuildingKey,
        string Language,
        string Version);

    /// <summary>
    /// Формула SpeedUpCalculator: ceil(Factor × хвилини^Exponent) за зрізану частину — усе понад
    /// FloorSeconds свого таймера (Construction, Training, UnitLevelUp, March); щонайменше 1 gem.
    /// Таймера немає в FloorSeconds — межа нуль.
    /// </summary>
    public record CatalogSpeedUp(IReadOnlyDictionary<string, int> FloorSeconds, double Factor, double Exponent);

    /// <summary>Тип звіра (GDD §5.10): з якого монстра приручається й на що діє пасивка.</summary>
    /// <param name="Effect">Production, Attack, Defense, MarchSpeed або Carry.</param>
    public record CatalogBeast(string Key, string DisplayName, string MonsterKey, string Effect);

    /// <param name="Rank">Ранг рядком: "Common", "Rare", "Unique".</param>
    /// <param name="NativeTier">Рідний тір: герой приходить у ньому й з'являється лише зі світу цього рівня (GDD §6.1).</param>
    /// <param name="Lore">Історія героя для кодексу й картки; коротший підсумок — у Description.</param>
    public record CatalogHero(
        string Key,
        string DisplayName,
        string Class,
        string Rank,
        int NativeTier,
        string? Description,
        string? Lore,
        double Speed,
        IReadOnlyDictionary<string, double> BaseStats,
        IReadOnlyDictionary<string, double> StatGrowth,
        IReadOnlyList<CatalogSkill> Skills);

    /// <summary>Вміння героя (GDD §6.1): рівень героя відкриває, зірки стелять рівень.</summary>
    /// <param name="Half">"Attack" або "Defense" — яка книга його піднімає.</param>
    /// <param name="Kind">"Active", "Passive", "Periodic" або "Utility".</param>
    /// <param name="Troops">Бонус війську в маршах і обороні; null у небойових.</param>
    /// <param name="Utility">Небойовий бонус; null у бойових.</param>
    /// <param name="Battle">Ефект у данжі; null у пасивок і небойових.</param>
    public record CatalogSkill(
        string Key,
        string DisplayName,
        string Description,
        string Half,
        string Kind,
        int UnlockLevel,
        CatalogSkillTroops? Troops,
        CatalogSkillUtility? Utility,
        CatalogSkillBattle? Battle);

    /// <summary>Книга вмінь (GDD §6.1): предмет, що піднімає вміння своєї половини в героя цієї ролі й рідкості.</summary>
    /// <param name="Rarity">"Common", "Rare" або "Unique".</param>
    /// <param name="Half">"Attack" або "Defense".</param>
    public record CatalogSkillBook(string ItemKey, string Class, string Rarity, string Half);

    /// <param name="Target">Ключ типу юніта або "all".</param>
    /// <param name="Percents">Бонус у відсотках на кожному рівні вміння, від першого.</param>
    public record CatalogSkillTroops(string Target, string Stat, IReadOnlyList<double> Percents);

    /// <param name="Effect">Ефект рядком: "MarchSpeed".</param>
    public record CatalogSkillUtility(string Effect, IReadOnlyList<double> Percents);

    /// <param name="Cooldown">Раз на скільки власних ходів героя вміння готове.</param>
    /// <param name="LevelScale">Множник шкоди, лікування й щита на кожному рівні вміння.</param>
    public record CatalogSkillBattle(
        string Target,
        int Cooldown,
        double DamageMultiplier,
        double HealPercent,
        double ShieldPercent,
        string? Status,
        bool IgnoresLine,
        IReadOnlyList<double> LevelScale);

    /// <param name="Slot">"Weapon", "Artifact" або null для стакового предмета.</param>
    /// <param name="ArtifactSlot">Тип слота артефакта (ключ з ArtifactSlots); null — не артефакт.</param>
    /// <param name="Tradeable">Стаковий предмет можна виставити на ринок; спорядження торгується завжди.</param>
    /// <param name="TeleportScope">Для телепорта — Exact, Nearby, ClanTerritory, Random або ClanLeader (GDD §8.9); null для решти.</param>
    /// <param name="TeleportRange">Радіус ближнього телепорта в клітинах (Чебишев); null для решти.</param>
    /// <param name="SpeedUpMinutes">Для прискорення — скільки хвилин зрізає з таймера; null для решти.</param>
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
        bool Tradeable,
        bool Giftable,
        string? TeleportScope,
        int? TeleportRange,
        int? SpeedUpMinutes);

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
    /// <param name="Upgradable">false — функціональна будівля без рівнів (GDD §3.1): кнопки апгрейду немає.</param>
    public record CatalogBuilding(string Key, string DisplayName, string? ProducesResource, CatalogPosition? Position,
        int RequiresMainBuildingLevel, bool Upgradable);

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
