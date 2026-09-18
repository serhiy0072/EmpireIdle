using EmpireIdle.Application.Banners.Commands;
using EmpireIdle.Application.Banners.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

        /// <summary>Один ролл. Ідемпотентний за заголовком Idempotency-Key.</summary>
        [HttpPost("{playerId:guid}/{bannerKey}/roll")]
        [ProducesResponseType(typeof(BannerRollResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<BannerRollResponse>> Roll(Guid playerId, string bannerKey, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new RollBannerCommand(playerId, bannerKey), cancellationToken));
    }
}
