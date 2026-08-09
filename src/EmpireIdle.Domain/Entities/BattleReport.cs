namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Звіт про бій — що гравець бачить після повернення армії.
    /// Незмінний після створення (історичний запис).
    /// </summary>
    public class BattleReport : Entity
    {
        private readonly List<BattleReportLine> _lines = new();

        public Guid PlayerId { get; private set; }
        public Guid MarchId { get; private set; }

        public int X { get; private set; }
        public int Y { get; private set; }
        public string TerrainType { get; private set; } = null!;

        /// <summary>Тип і рівень цілі (для монстра).</summary>
        public string TargetName { get; private set; } = null!;
        public int TargetLevel { get; private set; }

        public bool Won { get; private set; }
        public double AttackerPower { get; private set; }
        public double DefenderPower { get; private set; }

        public DateTime FoughtAt { get; private set; }

        /// <summary>Чи прочитано гравцем.</summary>
        public bool IsRead { get; private set; }

        /// <summary>Деталі по типах юнітів.</summary>
        public IReadOnlyCollection<BattleReportLine> Lines => _lines.AsReadOnly();

        public BattleReport(Guid id, Guid playerId, Guid marchId,
            int x, int y, string terrainType,
            string targetName, int targetLevel,
            bool won, double attackerPower, double defenderPower, DateTime utcNow) : base(id)
        {
            PlayerId = playerId;
            MarchId = marchId;
            X = x;
            Y = y;
            TerrainType = terrainType;
            TargetName = targetName;
            TargetLevel = targetLevel;
            Won = won;
            AttackerPower = attackerPower;
            DefenderPower = defenderPower;
            FoughtAt = utcNow;
        }

        protected BattleReport() { } // Для EF Core

        /// <summary>Додає рядок звіту по типу юніта.</summary>
        public void AddLine(string unitType, int sent, int wounded, int recoverable, int dead)
            => _lines.Add(new BattleReportLine(Guid.NewGuid(), Id, unitType, sent, wounded, recoverable, dead));

        /// <summary>Позначає звіт прочитаним.</summary>
        public void MarkAsRead() => IsRead = true;
    }

    public class BattleReportLine : Entity
    {
        public Guid BattleReportId { get; private set; }
        public string UnitType { get; private set; } = null!;

        /// <summary>Скільки вирушило в похід.</summary>
        public int Sent { get; private set; }

        public int Wounded { get; private set; }    
        public int Recoverable { get; private set; }
        public int Dead { get; private set; }

        /// <summary>Скільки вціліло (без втрат).</summary>
        public int Survived => Sent - Wounded - Recoverable - Dead;

        public BattleReportLine(Guid id, Guid battleReportId, string unitType, int sent, int wounded, int recoverable, int dead) : base(id)
        {
            BattleReportId = battleReportId;
            UnitType = unitType;
            Sent = sent;
            Wounded = wounded;
            Recoverable = recoverable;
            Dead = dead;
        }

        protected BattleReportLine() { } // Для EF Core
    }
}
