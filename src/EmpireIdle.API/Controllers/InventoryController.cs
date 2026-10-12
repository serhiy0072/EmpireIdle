using EmpireIdle.API.DTOs;
using EmpireIdle.Application.Heroes.Commands;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Inventory.Commands;
using EmpireIdle.Application.Inventory.Queries;
using EmpireIdle.Application.Lootboxes.Commands;
using EmpireIdle.Application.Lootboxes.Contracts;
using EmpireIdle.Application.Lootboxes.Queries;
using EmpireIdle.Application.Speedups.Commands;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

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
                    e.IsBroken, e.IsOnMarket, e.ResaleLockedUntil, e.Power))
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
        /// Відкрити скрині з рюкзака (GDD §9.12): повертає, що випало. Ідемпотентна операція — потрібен
        /// заголовок Idempotency-Key, інакше повтор запиту відкрив би ще раз.
        /// </summary>
        [HttpPost("{playerId:guid}/lootboxes/{itemKey}/open")]
        [ProducesResponseType(typeof(LootboxOpening), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LootboxOpening>> OpenLootbox(Guid playerId, string itemKey,
            [FromQuery][Range(1, OpenLootboxCommand.MaxCount)] int count = 1, CancellationToken cancellationToken = default)
            => Ok(await _mediator.Send(new OpenLootboxCommand(playerId, itemKey, count), cancellationToken));

        /// <summary>
        /// Відкрити ключову скриню: free=true — одне безкоштовне відкриття, коли готове; інакше count скринь
        /// ключами, по ключу за скриню. Ідемпотентна — потрібен заголовок Idempotency-Key.
        /// </summary>
        [HttpPost("{playerId:guid}/key-chests/{chestKey}/open")]
        [ProducesResponseType(typeof(LootboxOpening), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LootboxOpening>> OpenKeyChest(Guid playerId, string chestKey, [FromQuery] bool free = false,
            [FromQuery][Range(1, OpenKeyChestCommand.MaxCount)] int count = 1, CancellationToken cancellationToken = default)
            => Ok(await _mediator.Send(new OpenKeyChestCommand(playerId, chestKey, free, count), cancellationToken));

        /// <summary>Ключові скрині гравця: ключі в рюкзаку, залишок безкоштовних відкриттів і коли наступне.</summary>
        [HttpGet("{playerId:guid}/key-chests")]
        [ProducesResponseType(typeof(IReadOnlyList<KeyChestView>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IReadOnlyList<KeyChestView>>> GetKeyChests(Guid playerId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetKeyChestsQuery(playerId), cancellationToken));

        /// <summary>Лічильники гарантій скринь: скільки відкриттів поспіль без позиції гарантії.</summary>
        [HttpGet("{playerId:guid}/lootboxes/pity")]
        [ProducesResponseType(typeof(IReadOnlyList<LootboxPityView>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IReadOnlyList<LootboxPityView>>> GetLootboxPity(Guid playerId,
            CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetLootboxPityQuery(playerId), cancellationToken));

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
        /// Заточка коваля: спроба за золото. Ідемпотентна операція — потрібен заголовок Idempotency-Key.
        /// </summary>
        [HttpPost("{playerId:guid}/equipment/{equipmentId:guid}/mastery")]
        [ProducesResponseType(typeof(ArtifactMasteryResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ArtifactMasteryResponse>> RaiseMastery(Guid playerId, Guid equipmentId,
            CancellationToken cancellationToken)
        {
            var outcome = await _mediator.Send(new RaiseArtifactMasteryCommand(playerId, equipmentId), cancellationToken);
            return Ok(new ArtifactMasteryResponse(outcome == MasteryOutcome.Success, outcome == MasteryOutcome.Broken));
        }

        /// <summary>
        /// Ремонт зламаного артефакта за gems чи ремкомплектом. Ідемпотентна операція — потрібен
        /// заголовок Idempotency-Key, інакше повтор запиту заплатив би вдруге.
        /// </summary>
        [HttpPost("{playerId:guid}/equipment/{equipmentId:guid}/repair")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Repair(Guid playerId, Guid equipmentId, [FromBody] RepairArtifactRequest request,
            CancellationToken cancellationToken)
        {
            await _mediator.Send(new RepairArtifactCommand(playerId, equipmentId, request.UseKit), cancellationToken);
            return NoContent();
        }
    }
}
