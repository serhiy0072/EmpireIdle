using EmpireIdle.Application.Shop.Commands;
using EmpireIdle.Application.Shop.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace EmpireIdle.API.Controllers
{
    /// <summary>Крамниця: пакети gems за гроші (оплата — в PaymentsController) і предмети за gems.</summary>
    [ApiController]
    [Authorize]
    [Route("api/shop")]
    public class ShopController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ShopController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>Асортимент: спільний для всіх, береться з конфіга.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(ShopView), StatusCodes.Status200OK)]
        public async Task<ActionResult<ShopView>> GetShop(CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetShopQuery(), cancellationToken));

        /// <summary>Купити предмет за gems. Повертає залишок gems. Ідемпотентна за заголовком Idempotency-Key.</summary>
        [HttpPost("{playerId:guid}/items/{itemKey}")]
        [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<int>> BuyItem(
            Guid playerId, string itemKey, [FromQuery][Range(1, 100)] int count = 1, CancellationToken cancellationToken = default)
            => Ok(await _mediator.Send(new BuyShopItemCommand(playerId, itemKey, count), cancellationToken));
    }
}
