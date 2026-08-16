using EmpireIdle.API.DTOs;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Inventory.Queries;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmpireIdle.API.Controllers
{
    /// <summary>Інвентар гравця: розхідники та спорядження.</summary>
    [ApiController]
    [Authorize]
    [Route("api/inventory")]
    public class InventoryController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly GameCatalog _catalog;
        private readonly IActiveEffectRepository _effectRepository;

        public InventoryController(IMediator mediator, GameCatalog catalog, IActiveEffectRepository effectRepository)
        {
            _mediator = mediator;
            _catalog = catalog;
            _effectRepository = effectRepository;
        }

        /// <summary>Вміст інвентаря з описами предметів із конфіга.</summary>
        [HttpGet("{playerId:guid}")]
        [ProducesResponseType(typeof(InventoryResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<InventoryResponse>> GetInventory(Guid playerId, CancellationToken cancellationToken)
        {
            var contents = await _mediator.Send(new GetInventoryQuery(playerId), cancellationToken);

            var items = contents.Items
                .Select(i =>
                {
                    // Опис береться з конфіга; невідомий ключ показуємо як є
                    var config = _catalog.Items.GetValueOrDefault(i.ItemKey);
                    return new InventoryItemResponse(
                        i.ItemKey,
                        config?.DisplayName ?? i.ItemKey,
                        config?.Description ?? string.Empty,
                        config?.Rarity ?? "common",
                        config?.Type ?? "unknown",
                        i.Count);
                })
                .ToList();

            var equipment = contents.Equipment
                .Select(e => new EquipmentResponse(
                    e.Id, e.ItemKey, e.Slot.ToString(), e.Rarity,
                    e.EnhancementLevel, e.EquippedByHeroId,
                    e.Stats.ToDictionary(s => s.StatKey, s => e.GetStatValue(s.StatKey))))
                .ToList();

            var activeEffects = contents.ActiveEffects.Select(e => new ActiveEffectResponse(e.Target.ToString(), e.Multiplier, e.ExpiresAt, e.SourceItemKey)).ToList();

            return Ok(new InventoryResponse(items, equipment, activeEffects));
        }
    }
}
