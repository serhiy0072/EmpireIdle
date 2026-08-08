using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using MediatR;

namespace EmpireIdle.Application.Inventory.Queries
{
    /// <summary>Вміст інвентаря: стакові предмети та спорядження.</summary>
    public record InventoryContents(List<PlayerItem> Items, List<EquipmentItem> Equipment);

    /// <summary>Запит на інвентар гравця.</summary>
    public record GetInventoryQuery(Guid PlayerId) : IRequest<InventoryContents>, IPlayerScopedRequest;

    public class GetInventoryQueryHandler : IRequestHandler<GetInventoryQuery, InventoryContents>
    {
        private readonly IInventoryRepository _repository;

        public GetInventoryQueryHandler(IInventoryRepository repository)
        {
            _repository = repository;
        }

        public async Task<InventoryContents> Handle(GetInventoryQuery request, CancellationToken cancellationToken)
        {
            var items = await _repository.GetItemsAsync(request.PlayerId, cancellationToken);
            var equipment = await _repository.GetEquipmentAsync(request.PlayerId, cancellationToken);

            return new InventoryContents(items, equipment);
        }
    }
}