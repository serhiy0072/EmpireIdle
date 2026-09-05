namespace EmpireIdle.Domain.Entities
{
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
