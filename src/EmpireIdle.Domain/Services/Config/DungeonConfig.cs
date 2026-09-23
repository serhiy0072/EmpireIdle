using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Данж: три рівні складності над спільним ростером ворогів і власний
    /// набір артефактів. Рівень задає і силу ворогів, і кількість хвиль,
    /// і рідкість нагороди — щоб вибір рівня був вибором, а не формальністю.
    /// </summary>
    public class DungeonConfig
    {
        public string Key { get; set; } = null!;

        public string DisplayName { get; set; } = null!;

        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Базовий ключ набору артефактів. Повний ключ набору — «{ArtifactSetKey}_{рідкість}»:
        /// рівень забігу обирає, який саме з трьох наборів випаде.
        /// </summary>
        public string ArtifactSetKey { get; set; } = null!;

        /// <summary>Рівень ратуші, з якого данж узагалі доступний.</summary>
        public int RequiresMainBuildingLevel { get; set; }

        /// <summary>Звичайні хвилі; остання хвиля забігу завжди береться з Boss.</summary>
        public List<DungeonEnemyConfig> Waves { get; set; } = new();

        public List<DungeonEnemyConfig> Boss { get; set; } = new();

        /// <summary>Ресурси за успішний забіг рівня 1; вищі рівні множаться конфігом данжів.</summary>
        public List<ResourceCost> Reward { get; set; } = new();
    }

    /// <summary>Один ворог у хвилі. Стати рахуються від рівня забігу, а не зберігаються.</summary>
    public class DungeonEnemyConfig
    {
        public string Key { get; set; } = null!;

        public string DisplayName { get; set; } = null!;

        public BattleLine Line { get; set; }

        public double Attack { get; set; }

        public double Defense { get; set; }

        public double Health { get; set; }

        public double Speed { get; set; }

        /// <summary>Номер хвилі, в якій з'являється цей ворог: 1 — перша сутичка.</summary>
        public int Wave { get; set; } = 1;
    }
}
