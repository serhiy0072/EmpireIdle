using EmpireIdle.API.DTOs;
using EmpireIdle.Application.Clans.Commands;
using EmpireIdle.Application.Clans.Queries;
using EmpireIdle.Application.Clans.ReadModels;
using EmpireIdle.Application.Clans.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmpireIdle.API.Controllers
{
    /// <summary>Кланові технології: підношення, розподіл очок і магазин за кланові монети.</summary>
    [ApiController]
    [Authorize]
    [Route("api/clan-technologies")]
    public class ClanTechnologiesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ClanTechnologiesController(IMediator mediator) => _mediator = mediator;

        /// <summary>
        /// Дерево з поступом, запас очок, стан підношень гравця й денний рейтинг клану.
        /// Гравець без клану отримує порожню відповідь: це нормальний стан екрана, а не помилка.
        /// </summary>
        [HttpGet("{playerId:guid}")]
        [ProducesResponseType(typeof(ClanTechnologiesView), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Get(Guid playerId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetClanTechnologiesQuery(playerId), cancellationToken));

        /// <summary>Підношення ресурсом, який клан приймає зараз. Ідемпотентна за заголовком Idempotency-Key.</summary>
        [HttpPost("{playerId:guid}/offer-resources")]
        [ProducesResponseType(typeof(ClanOfferingResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ClanOfferingResult>> OfferResources(Guid playerId,
            [FromBody] OfferClanResourcesRequest request, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new OfferResourcesCommand(playerId, request.Resource), cancellationToken));

        /// <summary>Підношення за gems за показаною ціною. Ідемпотентна за заголовком Idempotency-Key.</summary>
        [HttpPost("{playerId:guid}/offer-gems")]
        [ProducesResponseType(typeof(ClanOfferingResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ClanOfferingResult>> OfferGems(Guid playerId,
            [FromBody] OfferClanGemsRequest request, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new OfferGemsCommand(playerId, request.ExpectedPrice), cancellationToken));

        /// <summary>Вкласти очки із запасу клану у вузол; потрібен дозвіл ManageTechnologies. Ідемпотентна за заголовком Idempotency-Key.</summary>
        [HttpPost("{playerId:guid}/allocate")]
        [ProducesResponseType(typeof(ClanAllocationResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ClanAllocationResult>> Allocate(Guid playerId,
            [FromBody] AllocateClanPointsRequest request, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new AllocateClanPointsCommand(playerId, request.Tier, request.Node, request.Points), cancellationToken));

        /// <summary>Вітрини за монети й безкоштовна тижнева з особистими лімітами. Доступно й без клану.</summary>
        [HttpGet("{playerId:guid}/shop")]
        [ProducesResponseType(typeof(ClanShopView), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<ClanShopView>> GetShop(Guid playerId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetClanShopQuery(playerId), cancellationToken));

        /// <summary>Купити предмет за кланові монети за показаною ціною. Ідемпотентна за заголовком Idempotency-Key.</summary>
        [HttpPost("{playerId:guid}/shop/buy")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Buy(Guid playerId, [FromBody] BuyClanShopItemRequest request, CancellationToken cancellationToken)
        {
            await _mediator.Send(new BuyClanShopItemCommand(playerId, request.Kind, request.ItemKey, request.Quantity, request.ExpectedPrice), cancellationToken);
            return NoContent();
        }

        /// <summary>Забрати безкоштовний предмет у межах тижневого ліміту. Ідемпотентна за заголовком Idempotency-Key.</summary>
        [HttpPost("{playerId:guid}/shop/claim-free")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ClaimFree(Guid playerId, [FromBody] ClaimClanFreeItemRequest request, CancellationToken cancellationToken)
        {
            await _mediator.Send(new ClaimFreeWeeklyItemCommand(playerId, request.ItemKey, request.Quantity), cancellationToken);
            return NoContent();
        }
    }
}
