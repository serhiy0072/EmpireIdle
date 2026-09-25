using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Репозиторій кланових споруд.</summary>
    public interface IClanStructureRepository
    {
        Task<ClanStructure?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Усі споруди клану — їх не більше десятка, тож без пагінації.</summary>
        Task<List<ClanStructure>> GetByClanAsync(Guid clanId, CancellationToken cancellationToken = default);

        /// <summary>Споруди всіх кланів у прямокутнику — для карти.</summary>
        Task<List<ClanStructure>> GetInAreaAsync(int minX, int minY, int maxX, int maxY,
            CancellationToken cancellationToken = default);

        Task AddAsync(ClanStructure structure, CancellationToken cancellationToken = default);

        void Remove(ClanStructure structure);
    }
}
