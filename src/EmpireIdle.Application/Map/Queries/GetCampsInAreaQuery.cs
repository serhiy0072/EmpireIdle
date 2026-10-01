using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Map.ReadModels;
using MediatR;

namespace EmpireIdle.Application.Map.Queries
{
    /// <summary>Табори у вікні навколо центру — ті самі межі, що й у ділянки мапи.</summary>
    public record GetCampsInAreaQuery(int CenterX, int CenterY, int Radius) : IRequest<List<CampOnMap>>;

    public sealed class GetCampsInAreaQueryHandler : IRequestHandler<GetCampsInAreaQuery, List<CampOnMap>>
    {
        private readonly IMarchRepository _marchRepository;

        public GetCampsInAreaQueryHandler(IMarchRepository marchRepository) => _marchRepository = marchRepository;

        public Task<List<CampOnMap>> Handle(GetCampsInAreaQuery request, CancellationToken cancellationToken)
            => _marchRepository.GetCampsInAreaAsync(
                request.CenterX - request.Radius, request.CenterY - request.Radius,
                request.CenterX + request.Radius, request.CenterY + request.Radius,
                cancellationToken);
    }
}
