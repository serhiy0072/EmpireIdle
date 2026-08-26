using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using MediatR;

namespace EmpireIdle.Application.Inventory.Queries
{
    /// <summary>Вміст інвентаря: стакові предмети, спорядження та діючі бусти.</summary>
    public record InventoryContents(List<PlayerItem> Items, List<EquipmentItem> Equipment, List<ActiveEffect> ActiveEffects);

    /// <summary>Запит на інвентар гравця.</summary>
    public record GetInventoryQuery(Guid PlayerId) : IRequest<InventoryContents>, IPlayerScopedRequest;

    public sealed class GetInventoryQueryHandler : IRequestHandler<GetInventoryQuery, InventoryContents>
    {
        private readonly IInventoryRepository _repository;
        private readonly IActiveEffectRepository _effectRepository;
        private readonly TimeProvider _timeProvider;

        public GetInventoryQueryHandler(IInventoryRepository repository, IActiveEffectRepository effectRepository, TimeProvider timeProvider)
        {
            _repository = repository;
            _effectRepository = effectRepository;
            _timeProvider = timeProvider;
        }

        public async Task<InventoryContents> Handle(GetInventoryQuery request, CancellationToken cancellationToken)
        {
            var items = await _repository.GetItemsAsync(request.PlayerId, cancellationToken);
            var equipment = await _repository.GetEquipmentAsync(request.PlayerId, cancellationToken);

            // Прострочені відсіюємо тут: фонове очищення не гарантує миттєвості
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var effects = (await _effectRepository.GetByPlayerAsync(request.PlayerId, cancellationToken))
                .Where(e => e.IsActive(now))
                .ToList();

            return new InventoryContents(items, equipment, effects);
        }
    }
}
