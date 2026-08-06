using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    public interface IMapRepository
    {

        /// <summary>Чи зайнята клітина.</summary>
        Task<bool> IsOccupiedAsync(int serverId, int x, int y, CancellationToken cancellationToken = default);

        /// <summary>Зайняті клітини у прямокутній ділянці (для перегляду карти).</summary>
        Task<List<MapCell>> GetAreaAsync(int serverId, int minX, int minY, int maxX, int maxY, CancellationToken cancellationToken = default);

        /// <summary>Клітина конкретного окупанта (де стоїть село/монстр).</summary>
        Task<MapCell?> GetByOccupantAsync(MapOccupantType occupantType, Guid occupantId, CancellationToken cancellationToken = default);

        /// <summary>Зайняти клітину.</summary>
        Task AddAsync(MapCell cell, CancellationToken cancellationToken = default);

        /// <summary>Звільнити клітину (окупант зник).</summary>
        void Remove(MapCell cell);
    }
}
