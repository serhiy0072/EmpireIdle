
using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using MediatR;

namespace EmpireIdle.Application.Villages.Queries
{
    /// <summary>
    /// Запит на отримання села гравця.
    /// </summary>
    public record GetVillageQuery(Guid PlayerId) : IRequest<Village>, IPlayerScopedRequest;

    /// <summary>
    /// Обробник запиту GetVillageQuery.
    /// </summary>
    public class GetVillageQueryHandler : IRequestHandler<GetVillageQuery, Village>
    {
        private readonly IVillageRepository _villageRepository;
        public GetVillageQueryHandler(IVillageRepository villageRepository)
        {
            _villageRepository = villageRepository;
        }

        public async Task<Village> Handle(GetVillageQuery request, CancellationToken cancellationToken)
        {
            return await _villageRepository.GetByPlayerIdReadOnlyAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village for player with ID {request.PlayerId} not found.");
        }
    }
}
