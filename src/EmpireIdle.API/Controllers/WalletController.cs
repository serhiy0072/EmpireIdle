using EmpireIdle.Application.Wallets.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmpireIdle.API.Controllers
{
    /// <summary>Гаманець гравця: баланс gems.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class WalletController : ControllerBase
    {
        private readonly IMediator _mediator;

        public WalletController(IMediator mediator) => _mediator = mediator;

        /// <summary>
        /// Отримати баланс gems гравця і його VIP-рівень.
        /// </summary>
        [HttpGet("{playerId:guid}")]
        [ProducesResponseType(typeof(WalletView), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetWallet(Guid playerId, CancellationToken cancellationToken)
        {
            return Ok(await _mediator.Send(new GetWalletQuery(playerId), cancellationToken));
        }
    }
}
