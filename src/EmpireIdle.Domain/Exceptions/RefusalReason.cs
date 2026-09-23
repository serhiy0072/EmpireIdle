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
