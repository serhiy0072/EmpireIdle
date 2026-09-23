using EmpireIdle.Application.Banners.Commands;
using EmpireIdle.Application.Banners.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace EmpireIdle.API.Controllers
{
    /// <summary>Банери: вітрина з шансами й ролл за gems.</summary>
    [ApiController]
    [Authorize]
    [Route("api/banners")]
    public class BannersController : ControllerBase
    {
        private readonly IMediator _mediator;

        public BannersController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>Активні банери з пулом, шансами й прогресом гарантій.</summary>
        [HttpGet("{playerId:guid}")]
        [ProducesResponseType(typeof(IReadOnlyList<BannerView>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IReadOnlyList<BannerView>>> GetBanners(Guid playerId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetBannersQuery(playerId), cancellationToken));

        /// <summary>
        /// Серія роллів: count від 1 до 10 за один запит, одна транзакція.
        /// Ідемпотентна за заголовком Idempotency-Key.
        /// </summary>
        [HttpPost("{playerId:guid}/{bannerKey}/roll")]
        [ProducesResponseType(typeof(BannerRollResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<BannerRollResponse>> Roll(
            Guid playerId, string bannerKey, [FromQuery][Range(1, RollBannerCommand.MaxCount)] int count = 1,
            [FromQuery] BannerCurrency currency = BannerCurrency.Gems,
            CancellationToken cancellationToken = default)
            => Ok(await _mediator.Send(new RollBannerCommand(playerId, bannerKey, count, currency), cancellationToken));
    }
}
