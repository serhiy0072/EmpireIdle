using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Звіт розвідки: знімок цілі на момент прибуття розвідників. Лише для розвідника —
    /// ціль дізнається про розвідку з маршу й тривоги, не зі звіту.
    /// Назва цілі — знімок, як у звітах боїв.
    /// </summary>
    public class ScoutReport : Entity
    {
        private readonly List<ScoutReportResource> _resources = new();

        public int ServerId { get; private set; }

        /// <summary>Хто розвідував.</summary>
        public Guid PlayerId { get; private set; }

        public Guid MarchId { get; private set; }

        public MarchTargetType TargetType { get; private set; }

        public Guid TargetId { get; private set; }

        public string TargetName { get; private set; } = null!;

        public int X { get; private set; }

        public int Y { get; private set; }

        public ScoutOutcome Outcome { get; private set; }

        /// <summary>Сила оборони з усіма бонусами (бусти, територія клану, лідери); null — розвідка не вдалась.</summary>
        public double? DefencePower { get; private set; }

        public DateTime CreatedAt { get; private set; }

        /// <summary>Що можна винести: буфери будівель і склад понад захищений запас. Порожньо для споруди й невдачі.</summary>
        public IReadOnlyCollection<ScoutReportResource> Resources => _resources.AsReadOnly();

        private ScoutReport(Guid id, int serverId, Guid playerId, March march, string targetName,
            ScoutOutcome outcome, double? defencePower, DateTime utcNow) : base(id)
        {
            ServerId = serverId;
            PlayerId = playerId;
            MarchId = march.Id;
            TargetType = march.TargetType;
            TargetId = march.TargetId;
            TargetName = targetName;
            X = march.TargetX;
            Y = march.TargetY;
            Outcome = outcome;
            DefencePower = defencePower;
            CreatedAt = utcNow;
        }

        protected ScoutReport() { } // Для EF Core

        /// <summary>Вдала розвідка: сила оборони й здобич, яку можна винести.</summary>
        public static ScoutReport Success(Guid id, int serverId, Guid playerId, March march, string targetName,
            double defencePower, IReadOnlyDictionary<string, int> lootable, DateTime utcNow)
        {
            var report = new ScoutReport(id, serverId, playerId, march, targetName, ScoutOutcome.Success, defencePower, utcNow);

            foreach (var (resource, amount) in lootable.Where(r => r.Value > 0))
                report._resources.Add(new ScoutReportResource(Guid.NewGuid(), id, resource, amount));

            return report;
        }

        /// <summary>Невдала розвідка — лише причина, жодних даних про ціль.</summary>
        public static ScoutReport Failed(Guid id, int serverId, Guid playerId, March march, string targetName,
            ScoutOutcome outcome, DateTime utcNow)
        {
            if (outcome == ScoutOutcome.Success)
                throw new ArgumentException("A failed report cannot have the Success outcome.", nameof(outcome));

            return new ScoutReport(id, serverId, playerId, march, targetName, outcome, null, utcNow);
        }
    }

    /// <summary>Один ресурс, який можна винести з розвіданого села.</summary>
    public class ScoutReportResource : Entity
    {
        public Guid ScoutReportId { get; private set; }

        public string ResourceType { get; private set; } = null!;

        public int Amount { get; private set; }

        public ScoutReportResource(Guid id, Guid scoutReportId, string resourceType, int amount) : base(id)
        {
            ScoutReportId = scoutReportId;
            ResourceType = resourceType;
            Amount = amount;
        }

        protected ScoutReportResource() { } // Для EF Core
    }
}
