using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    public interface IPlayerPowerRepository
    {
        Task<PlayerPower?> GetByPlayerAsync(Guid playerId, CancellationToken cancellationToken = default);

        Task AddAsync(PlayerPower power, CancellationToken cancellationToken = default);
    }
}
