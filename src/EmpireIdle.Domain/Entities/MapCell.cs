using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Зайнята клітина карти. Sparse-модель: порожні клітини в БД не зберігаються,
    /// їхня місцевість обчислюється TerrainGenerator'ом на льоту.
    /// </summary>
    public class MapCell : Entity
    {

        /// <summary>Сервер (світ), якому належить клітина.</summary>
        public int ServerId { get; private set; }

        public int X { get; private set; }
        public int Y { get; private set; }


        /// <summary>Хто займає клітину.</summary>
        public MapOccupantType OccupantType { get; private set; }

        /// <summary>Ідентифікатор окупанта (село, монстр).</summary>
        public Guid OccupantId { get; private set; }

        public MapCell(Guid id, int serverId, int x, int y, MapOccupantType occupantType, Guid occupantId) : base(id)
        {
            ServerId = serverId;
            X = x;
            Y = y;
            OccupantType = occupantType;
            OccupantId = occupantId;
        }

        protected MapCell() { } // Для EF Core
    }
}
