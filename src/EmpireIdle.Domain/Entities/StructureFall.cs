using EmpireIdle.Domain.Events;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Запис про зруйновану кланову споруду: де стояла, хто зруйнував і коли.
    /// Сама споруда видаляється разом із боєм, а лист учасникам клану посилається
    /// на цей запис. Назва нападника — знімок на момент бою, як у звітах.
    /// </summary>
    public class StructureFall : Entity
    {
        public int ServerId { get; private set; }

        public Guid ClanId { get; private set; }

        public Guid StructureId { get; private set; }

        public int X { get; private set; }

        public int Y { get; private set; }

        public Guid AttackerPlayerId { get; private set; }

        public string AttackerVillageName { get; private set; } = null!;

        public DateTime OccurredAt { get; private set; }

        public StructureFall(Guid id, int serverId, ClanStructure structure, Guid attackerPlayerId,
            string attackerVillageName, DateTime utcNow) : base(id)
        {
            ServerId = serverId;
            ClanId = structure.ClanId;
            StructureId = structure.Id;
            X = structure.X;
            Y = structure.Y;
            AttackerPlayerId = attackerPlayerId;
            AttackerVillageName = attackerVillageName;
            OccurredAt = utcNow;

            RaiseDomainEvent(new ClanStructureFell(id, ClanId, utcNow));
        }

        protected StructureFall() { } // Для EF Core
    }
}
