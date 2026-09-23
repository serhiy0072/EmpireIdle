using EmpireIdle.API.DTOs;
using EmpireIdle.Application.Heroes.Commands;
using EmpireIdle.Application.Heroes.Contracts;
using EmpireIdle.Application.Heroes.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmpireIdle.API.Controllers
{
    /// <summary>
    /// Герої гравця: ростер, призов за уламки, прокачка та еволюція тіру.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/heroes")]
    public class HeroesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public HeroesController(IMediator mediator) => _mediator = mediator;

        /// <summary>
        /// Ростер із поточною стелею рівня, уламками та активною чергою.
        /// </summary>
        [HttpGet("{playerId:guid}")]
        [ProducesResponseType(typeof(HeroesOverview), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<HeroesOverview>> GetOverview(Guid playerId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetHeroesOverviewQuery(playerId), cancellationToken));

        /// <summary>Купити уламки звичайного героя за золото.</summary>
        [HttpPost("{playerId:guid}/shards/buy")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> BuyShards(Guid playerId, [FromBody] BuyHeroShardsRequest request, CancellationToken cancellationToken)
        {
            await _mediator.Send(new BuyHeroShardsCommand(playerId, request.HeroKey, request.Count), cancellationToken);
            return NoContent();
        }

        /// <summary>Призвати героя за накопичені уламки.</summary>
        [HttpPost("{playerId:guid}/summon")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Summon(Guid playerId, [FromBody] SummonHeroRequest request, CancellationToken cancellationToken)
        {
            await _mediator.Send(new SummonHeroCommand(playerId, request.HeroKey), cancellationToken);
            return NoContent();
        }

        /// <summary>Поставити героя в чергу на підняття рівня.</summary>
        [HttpPost("{playerId:guid}/{heroId:guid}/level-up")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> LevelUp(Guid playerId, Guid heroId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new StartHeroLevelUpCommand(playerId, heroId), cancellationToken);
            return NoContent();
        }

        /// <summary>Миттєво завершити прокачку героя за gems. Новий рівень видно одразу.</summary>
        [HttpPost("{playerId:guid}/level-up/{orderId:guid}/speedup")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SpeedUpLevelUp(Guid playerId, Guid orderId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new SpeedUpHeroLevelUpCommand(playerId, orderId), cancellationToken);
            return NoContent();
        }

        /// <summary>Підняти тір героя за предмет еволюції.</summary>
        [HttpPost("{playerId:guid}/{heroId:guid}/evolve")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Evolve(Guid playerId, Guid heroId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new EvolveHeroTierCommand(playerId, heroId), cancellationToken);
            return NoContent();
        }

        /// <summary>Призначити героя лідером гарнізону, у якому він стоїть.</summary>
        [HttpPost("{playerId:guid}/{heroId:guid}/appoint-leader")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> AppointLeader(Guid playerId, Guid heroId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new AppointGarrisonLeaderCommand(playerId, heroId), cancellationToken);
            return NoContent();
        }

        /// <summary>Вилікувати пораненого героя за ресурси.</summary>
        [HttpPost("{playerId:guid}/{heroId:guid}/heal")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Heal(Guid playerId, Guid heroId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new HealHeroCommand(playerId, heroId), cancellationToken);
            return NoContent();
        }

        /// <summary>Вдягнути спорядження на героя.</summary>
        [HttpPost("{playerId:guid}/{heroId:guid}/equipment/{equipmentId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Equip(Guid playerId, Guid heroId, Guid equipmentId,
            CancellationToken cancellationToken)
        {
            await _mediator.Send(new EquipHeroItemCommand(playerId, heroId, equipmentId), cancellationToken);
            return NoContent();
        }

        /// <summary>Зняти спорядження в інвентар.</summary>
        [HttpDelete("{playerId:guid}/equipment/{equipmentId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Unequip(Guid playerId, Guid equipmentId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new UnequipHeroItemCommand(playerId, equipmentId), cancellationToken);
            return NoContent();
        }
    }
}
