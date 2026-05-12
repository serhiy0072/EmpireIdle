using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Services
{
    /// <summary>
    /// Отримати стан села гравця для відображення на фронтенді.
    /// </summary>
    public class GetVillageService
    {
        private readonly IVillageRepository _villageRepository;

        public GetVillageService(IVillageRepository villageRepository)
        {
            _villageRepository = villageRepository;
        }

        /// <summary>
        /// Отримати село за ідентифікатором гравця.
        /// </summary>
        public async Task<Village> GetByPlayerId(Guid playerId, CancellationToken cancellation = default)
        {
            var village = await _villageRepository.GetByPlayerIdAsync(playerId, cancellation) 
                ?? throw new InvalidOperationException($"Village not found for player {playerId}.");

            return village;
        }
    }
}
