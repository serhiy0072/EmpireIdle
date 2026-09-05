using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Power.ReadModels;
using MediatR;

namespace EmpireIdle.Application.Power.Queries
{
    public record GetPlayerPowerQuery(Guid PlayerId) : IRequest<PlayerPowerView>, IPlayerScopedRequest;

    public sealed class GetPlayerPowerQueryHandler : IRequestHandler<GetPlayerPowerQuery, PlayerPowerView>
    {
        private readonly IPlayerPowerRepository _powerRepository;
        private readonly TimeProvider _timeProvider;

        public GetPlayerPowerQueryHandler(IPlayerPowerRepository powerRepository, TimeProvider timeProvider)
        {
            _powerRepository = powerRepository;
            _timeProvider = timeProvider;
        }

        public async Task<PlayerPowerView> Handle(GetPlayerPowerQuery request, CancellationToken cancellationToken)
        {
            var power = await _powerRepository.GetByPlayerAsync(request.PlayerId, cancellationToken);

            // Рядка ще немає, поки гравець не зробив нічого, що змінює армію.
            // Нулі, а не 404: сила нуль — це коректна відповідь, а не відсутність гравця
            return power is null
                ? new PlayerPowerView(0, 0, 0, 0, _timeProvider.GetUtcNow().UtcDateTime)
                : new PlayerPowerView(power.TotalPower, power.ArmyPower, power.HeroPower,
                    power.EquipmentPower, power.UpdatedAt);
        }
    }
}
