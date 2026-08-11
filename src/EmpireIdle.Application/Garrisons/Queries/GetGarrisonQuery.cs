using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using MediatR;

namespace EmpireIdle.Application.Garrisons.Queries
{
    /// <summary>Запит на отримання гарнізону гравця.</summary>
    public record GetGarrisonQuery(Guid PlayerId) : IRequest<Garrison>, IPlayerScopedRequest;

    public class GetGarrisonQueryHandler : IRequestHandler<GetGarrisonQuery, Garrison>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IGarrisonRepository _garrisonRepository;

        public GetGarrisonQueryHandler(IVillageRepository villageRepository, IGarrisonRepository garrisonRepository)
        {
            _villageRepository = villageRepository;
            _garrisonRepository = garrisonRepository;
        }

        public async Task<Garrison> Handle(GetGarrisonQuery request, CancellationToken cancellationToken)
        {
            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
               ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            var garrison = await _garrisonRepository.GetByVillageIdReadOnlyAsync(village.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Garrison not found for village {village.Id}.");

            return garrison;
        }
    }
}
