using EmpireIdle.API.DTOs;
using EmpireIdle.Application.Heroes.Commands;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Inventory.Commands;
using EmpireIdle.Application.Inventory.Queries;
using EmpireIdle.Application.Speedups.Commands;
using EmpireIdle.Domain.Enums;
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

        public InventoryController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>Вміст інвентаря з описами предметів із конфіга.</summary>
        [HttpGet("{playerId:guid}")]
        [ProducesResponseType(typeof(InventoryResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<InventoryResponse>> GetInventory(Guid playerId, CancellationToken cancellationToken)
        {
            var inventory = await _mediator.Send(new GetInventoryQuery(playerId), cancellationToken);

            var items = inventory.Items
                .Select(i => new InventoryItemResponse(i.ItemKey, i.DisplayName, i.Description,
                    i.Rarity.ToString().ToLowerInvariant(), i.Type, i.Count))
                .ToList();

            var equipment = inventory.Equipment
                .Select(e => new EquipmentResponse(
                    e.Id, e.ItemKey, e.Slot.ToString(), e.Rarity.ToString().ToLowerInvariant(),
                    e.Level, e.Experience, e.ExperienceToNext, e.FeedValue, e.Mastery, e.EquippedByHeroId, e.SlotIndex, e.Stats,
                    e.IsOnMarket, e.ResaleLockedUntil))
                .ToList();

            var activeEffects = inventory.ActiveEffects
                .Select(e => new ActiveEffectResponse(e.Target.ToString(), e.Multiplier, e.ExpiresAt, e.SourceItemKey))
                .ToList();

            return Ok(new InventoryResponse(items, equipment, activeEffects));
        }

        /// <summary>
        /// Використати предмет. Ідемпотентна операція — потрібен заголовок Idempotency-Key.
        /// </summary>
        /// <remarks>
        /// TargetX/TargetY потрібні предметам, що діють на клітину карти (телепорт).
        /// Валідність координат перевіряє ефект: контролер лише транспорт.
        /// </remarks>
        [HttpPost("{playerId:guid}/use")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UseItem(Guid playerId, [FromBody] UseItemRequest request,
            CancellationToken cancellationToken)
        {
            await _mediator.Send(new UseItemCommand(
                playerId, request.ItemKey, request.Count, request.TargetX, request.TargetY), cancellationToken);

            return NoContent();
        }

        /// <summary>
        /// Прискорити таймер предметами-прискореннями (будівництво, тренування, рівень військ, марш).
        /// Ідемпотентна операція — потрібен заголовок Idempotency-Key.
        /// </summary>
        [HttpPost("{playerId:guid}/speedups")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SpeedUpWithItems(Guid playerId, [FromBody] SpeedUpWithItemsRequest request,
            CancellationToken cancellationToken)
        {
            await _mediator.Send(new SpeedUpWithItemsCommand(playerId, request.Timer, request.TargetId, request.Items),
                cancellationToken);

            return NoContent();
        }

        /// <summary>
        /// Подарувати предмет члену свого клану. Ідемпотентна операція — потрібен заголовок Idempotency-Key.
        /// </summary>
        [HttpPost("{playerId:guid}/gift")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GiftItem(Guid playerId, [FromBody] GiftItemRequest request,
            CancellationToken cancellationToken)
        {
            await _mediator.Send(new GiftItemCommand(playerId, request.RecipientId, request.ItemKey, request.Count),
                cancellationToken);

            return NoContent();
        }

        /// <summary>
        /// Посилити артефакт: згодувати йому спорядження й гаєчки. Ідемпотентна операція — потрібен
        /// заголовок Idempotency-Key, інакше повтор запиту згодував би ще раз.
        /// </summary>
        [HttpPost("{playerId:guid}/equipment/{equipmentId:guid}/feed")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Feed(Guid playerId, Guid equipmentId, [FromBody] FeedArtifactRequest request,
            CancellationToken cancellationToken)
        {
            await _mediator.Send(new FeedArtifactCommand(playerId, equipmentId, request.FoodIds, request.Wrenches),
                cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Майстерність коваля: спроба за золото. Ідемпотентна операція — потрібен заголовок Idempotency-Key.
        /// </summary>
        [HttpPost("{playerId:guid}/equipment/{equipmentId:guid}/mastery")]
        [ProducesResponseType(typeof(ArtifactMasteryResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ArtifactMasteryResponse>> RaiseMastery(Guid playerId, Guid equipmentId,
            CancellationToken cancellationToken)
        {
            var success = await _mediator.Send(new RaiseArtifactMasteryCommand(playerId, equipmentId), cancellationToken);
            return Ok(new ArtifactMasteryResponse(success));
        }
    }
}
