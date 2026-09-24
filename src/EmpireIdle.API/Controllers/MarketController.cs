using EmpireIdle.API.DTOs;
using EmpireIdle.Application.Market.Commands;
using EmpireIdle.Application.Market.Contracts;
using EmpireIdle.Application.Market.Queries;
using EmpireIdle.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmpireIdle.API.Controllers
{
    /// <summary>Ринок гравців: вітрина, власні лоти, виставлення, купівля й зняття (GDD §8.8).</summary>
    [ApiController]
    [Authorize]
    [Route("api/market")]
    public class MarketController : ControllerBase
    {
        private readonly IMediator _mediator;

        public MarketController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>Сторінка вітрини: активні лоти, найдешевші за одиницю першими.</summary>
        [HttpGet("{playerId:guid}")]
        [ProducesResponseType(typeof(MarketPageView), StatusCodes.Status200OK)]
        public async Task<ActionResult<MarketPageView>> Browse(Guid playerId, [FromQuery] MarketListingKind? kind,
            [FromQuery] string? itemKey, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default)
            => Ok(await _mediator.Send(new GetMarketListingsQuery(playerId, kind, itemKey, page, pageSize), cancellationToken));

        /// <summary>Чи відкритий ринок гравцю, ліміт лотів і власні лоти.</summary>
        [HttpGet("{playerId:guid}/mine")]
        [ProducesResponseType(typeof(MyMarketView), StatusCodes.Status200OK)]
        public async Task<ActionResult<MyMarketView>> Mine(Guid playerId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetMyMarketQuery(playerId), cancellationToken));

        /// <summary>Дозволений діапазон ціни для товару — перед виставленням.</summary>
        [HttpPost("{playerId:guid}/quote")]
        [ProducesResponseType(typeof(MarketQuoteView), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<MarketQuoteView>> Quote(Guid playerId, [FromBody] MarketGoodsRequest request,
            CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetMarketQuoteQuery(playerId, request.Kind, request.EquipmentId, request.HeroId,
                request.ItemKey, request.Quantity), cancellationToken));

        /// <summary>Виставити товар. Ідемпотентна за заголовком Idempotency-Key.</summary>
        [HttpPost("{playerId:guid}/listings")]
        [ProducesResponseType(typeof(MarketListingCreated), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<MarketListingCreated>> List(Guid playerId, [FromBody] ListOnMarketRequest request,
            CancellationToken cancellationToken)
        {
            var id = await _mediator.Send(new ListOnMarketCommand(playerId, request.Kind, request.EquipmentId, request.HeroId,
                request.ItemKey, request.Quantity, request.PriceGold), cancellationToken);

            return Ok(new MarketListingCreated(id));
        }

        /// <summary>
        /// Купити лот. Ідемпотентна. Уже проданий чи знятий лот — 400 з причиною
        /// market.listingClosed; дві одночасні покупки — 409 ConcurrencyConflict для другої.
        /// </summary>
        [HttpPost("{playerId:guid}/listings/{listingId:guid}/buy")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Buy(Guid playerId, Guid listingId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new BuyMarketListingCommand(playerId, listingId), cancellationToken);
            return NoContent();
        }

        /// <summary>Зняти свій лот. Товар повертається, податок — ні. Ідемпотентна.</summary>
        [HttpPost("{playerId:guid}/listings/{listingId:guid}/cancel")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Cancel(Guid playerId, Guid listingId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new CancelMarketListingCommand(playerId, listingId), cancellationToken);
            return NoContent();
        }
    }
}
