using EmpireIdle.API.DTOs;
using EmpireIdle.Application.Garrisons.Commands;
using EmpireIdle.Application.Garrisons.Queries;
using EmpireIdle.Domain.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmpireIdle.API.Controllers
{
    /// <summary>
    /// Гарнізон гравця: юніти та черги тренування й прокачки.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/garrisons")]
    public class GarrisonController : ControllerBase
    {
        private readonly IMediator _mediator;

        public GarrisonController(IMediator mediator) => _mediator = mediator;

        /// <summary>
        /// Отримати гарнізон: юніти та активні замовлення тренування й прокачки.
        /// </summary>
        [HttpGet("{playerId:guid}")]
        [ProducesResponseType(typeof(GarrisonResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<GarrisonResponse>> GetGarrison(Guid playerId, CancellationToken cancellationToken)
        {
            var garrison = await _mediator.Send(new GetGarrisonQuery(playerId), cancellationToken);

            var response = new GarrisonResponse(
                garrison.Id,
                garrison.VillageId,
                garrison.Units.Select(u => new UnitResponse(u.UnitType, u.Level, u.Count)).ToList(),
                garrison.Wounded.Select(w => new UnitResponse(w.UnitType, w.Level, w.Count)).ToList(),
                garrison.Recoverable.Select(r => new RecoverableUnitResponse(
                    r.UnitType, r.Level, r.Count, r.ExpiresAt, r.CostGems)).ToList(),
                garrison.TrainingOrders.Select(o => new TrainingOrderResponse(
                    o.Id, o.UnitType, o.Level, o.Count, o.CompletesAt, o.SpeedUpCostGems)).ToList(),
                garrison.LevelUpOrders.Select(o => new LevelUpOrderResponse(
                    o.Id, o.UnitType, o.FromLevel, o.ToLevel, o.Count, o.CompletesAt, o.SpeedUpCostGems)).ToList());

            return Ok(response);
        }

        /// <summary>
        /// Замовити тренування партії юнітів з нуля на обраному рівні (1–5).
        /// </summary>
        [HttpPost("{playerId:guid}/units/train")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> TrainUnits(Guid playerId, [FromBody] TrainUnitsRequest request, CancellationToken cancellationToken)
        {
            await _mediator.Send(new TrainUnitsCommand(playerId, request.UnitType, request.Level, request.Count), cancellationToken);
            return NoContent();
        }

        /// <summary>Прокачати партію вже навчених юнітів на вищий рівень.</summary>
        [HttpPost("{playerId:guid}/units/levelup")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> LevelUpUnits(Guid playerId, [FromBody] LevelUpUnitsRequest request, CancellationToken cancellationToken)
        {
            await _mediator.Send(new LevelUpUnitsCommand(
                playerId, request.UnitType, request.FromLevel, request.ToLevel, request.Count), cancellationToken);
            return NoContent();
        }

        /// <summary>Вилікувати поранених юнітів.</summary>
        [HttpPost("{playerId:guid}/units/heal")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> HealWounded(Guid playerId, [FromBody] HealWoundedRequest request, CancellationToken cancellationToken)
        {
            var units = request.Units.ToDictionary(kv => UnitStackKey.Parse(kv.Key), kv => kv.Value);

            await _mediator.Send(new HealWoundedCommand(playerId, units, request.Payment), cancellationToken);
            return NoContent();
        }

        /// <summary>Викупити відновлюваних юнітів за gems.</summary>
        [HttpPost("{playerId:guid}/units/recover")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RecoverUnits(Guid playerId, [FromBody] RecoverUnitsRequest request, CancellationToken cancellationToken)
        {
            var units = request.Units.ToDictionary(kv => UnitStackKey.Parse(kv.Key), kv => kv.Value);

            await _mediator.Send(new RecoverUnitsCommand(playerId, units), cancellationToken);
            return NoContent();
        }

        /// <summary>Миттєво завершити тренування партії юнітів за gems.</summary>
        [HttpPost("{playerId:guid}/training/{orderId:guid}/speedup")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SpeedUpTraining(Guid playerId, Guid orderId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new SpeedUpTrainingCommand(playerId, orderId), cancellationToken);
            return NoContent();
        }

        /// <summary>Миттєво завершити прокачку партії юнітів за gems.</summary>
        [HttpPost("{playerId:guid}/levelup/{orderId:guid}/speedup")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SpeedUpLevelUp(Guid playerId, Guid orderId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new SpeedUpLevelUpCommand(playerId, orderId), cancellationToken);
            return NoContent();
        }
    }
}
