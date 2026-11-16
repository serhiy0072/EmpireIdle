using EmpireIdle.Application.Power.Queries;
using EmpireIdle.Application.Power.ReadModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmpireIdle.API.Controllers
{
    /// <summary>Бойова сила гравця.</summary>
    [ApiController]
    [Route("api/power")]
    [Authorize]
    public class PowerController : ControllerBase
    {
        private readonly IMediator _mediator;

        public PowerController(IMediator mediator) => _mediator = mediator;

        /// <summary>
        /// Сила гравця з розкладкою по джерелах. Оновлюється подіями,
        /// що змінюють армію — тренуванням, боєм, поверненням походу.
        /// </summary>
        [HttpGet("{playerId:guid}")]
        [ProducesResponseType(typeof(PlayerPowerView), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetPower(Guid playerId, CancellationToken cancellationToken)
        {
            return Ok(await _mediator.Send(new GetPlayerPowerQuery(playerId), cancellationToken));
        }
    }
}
