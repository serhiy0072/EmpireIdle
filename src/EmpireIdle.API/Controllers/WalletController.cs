using EmpireIdle.API.DTOs;
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
        /// Отримати баланс gems гравця.
        /// </summary>
        [HttpGet("{playerId:guid}")]
        [ProducesResponseType(typeof(WalletResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetWallet(Guid playerId, CancellationToken cancellationToken)
        {
            var wallet = await _mediator.Send(new GetWalletQuery(playerId), cancellationToken);
            return Ok(new WalletResponse(wallet.GemBalance));
        }
    }
}
