using MediatR;

namespace EmpireIdle.Application.Catalog.Queries
{
    /// <summary>Каталог для інтерфейсу. Без прив'язки до гравця: дані однакові для всіх.</summary>
    public record GetCatalogQuery : IRequest<CatalogResponse>;

    internal sealed class GetCatalogQueryHandler : IRequestHandler<GetCatalogQuery, CatalogResponse>
    {
        private readonly GameCatalogProjection _projection;

        public GetCatalogQueryHandler(GameCatalogProjection projection)
        {
            _projection = projection;
        }

        public Task<CatalogResponse> Handle(GetCatalogQuery request, CancellationToken cancellationToken)
            => Task.FromResult(_projection.Response);
    }
}
