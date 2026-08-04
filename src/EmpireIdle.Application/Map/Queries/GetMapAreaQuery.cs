using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using MediatR;

namespace EmpireIdle.Application.Map.Queries
{
    /// <summary>Зайняті клітини у вікні навколо центру.</summary>
    public record GetMapAreaQuery(int ServerId, int CenterX, int CenterY, int Radius) : IRequest<List<MapCell>>;

    public class GetMapAreaQueryHandler : IRequestHandler<GetMapAreaQuery, List<MapCell>>
    {
        private const int MaxRadius = 25; // 51×51 клітин — щоб не вивантажити пів світу

        private readonly IMapRepository _mapRepository;

        public GetMapAreaQueryHandler(IMapRepository mapRepository)
        {
            _mapRepository = mapRepository;
        }

        public Task<List<MapCell>> Handle(GetMapAreaQuery request, CancellationToken cancellationToken)
        {
            if (request.Radius < 0 || request.Radius > MaxRadius)
                throw new ArgumentOutOfRangeException(nameof(request.Radius), $"Radius must be between 0 and {MaxRadius}.");

            return _mapRepository.GetAreaAsync(
                request.ServerId,
                request.CenterX - request.Radius, request.CenterY - request.Radius,
                request.CenterX + request.Radius, request.CenterY + request.Radius,
                cancellationToken);
        }
    }
}
