using EmpireIdle.API.DTOs;
using EmpireIdle.Application.Garrisons.Commands;
using EmpireIdle.Application.Garrisons.Queries;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace EmpireIdle.API.Controllers
{
    /// <summary>
    /// Гарнізон гравця: юніти та черга тренування.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/garrisons")]
    public class GarrisonController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly GameConfig _gameConfig;

        public GarrisonController(IMediator mediator, IOptions<GameConfig> gameConfig)
        {
            _mediator = mediator;
            _gameConfig = gameConfig.Value;
        }

        /// <summary>
        /// Отримати гарнізон: юніти та активні замовлення тренування.
        /// </summary>
        [HttpGet("{playerId:guid}")]
        [ProducesResponseType(typeof(GarrisonResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<GarrisonResponse>> GetGarrison(Guid playerId, CancellationToken cancellationToken)
        {
            var garrison = await _mediator.Send(new GetGarrisonQuery(playerId), cancellationToken);

            var now = DateTime.UtcNow;

            var response = new GarrisonResponse(
                garrison.Id,
                garrison.VillageId,
                garrison.Units.Select(u => new UnitResponse(u.UnitType, u.Count)).ToList(),
                garrison.Wounded.Select(w => new UnitResponse(w.UnitType, w.Count)).ToList(),
                garrison.Recoverable
                    .Where(r => r.IsActive(now))
                    .OrderBy(r => r.ExpiresAt)
                    .Select(r => new RecoverableUnitResponse(r.UnitType, r.Count, r.ExpiresAt, RecoverCost(r.UnitType) * r.Count))
                    .ToList(),
                garrison.TrainingOrders.Select(o => new TrainingOrderResponse(o.Id, o.UnitType, o.Count, o.CompletesAt)).ToList());

            return Ok(response);
        }

        /// <summary>
        /// Замовити тренування партії юнітів (1–5).
        /// </summary>
        [HttpPost("{playerId:guid}/units/train")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> TrainUnits(Guid playerId, [FromBody] TrainUnitsRequest request, CancellationToken cancellationToken)
        {
            await _mediator.Send(new TrainUnitsCommand(playerId, request.UnitType, request.Count), cancellationToken);
            return NoContent();
        }

        /// <summary>Вилікувати поранених юнітів.</summary>
        [HttpPost("{playerId:guid}/units/heal")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> HealWounded(Guid playerId, [FromBody] HealWoundedRequest request, CancellationToken cancellationToken)
        {
            await _mediator.Send(new HealWoundedCommand(playerId, request.Units, request.Payment), cancellationToken);
            return NoContent();
        }

        /// <summary>Викупити відновлюваних юнітів за gems.</summary>
        [HttpPost("{playerId:guid}/units/recover")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RecoverUnits(Guid playerId, [FromBody] RecoverUnitsRequest request, CancellationToken cancellationToken)
        {
            await _mediator.Send(new RecoverUnitsCommand(playerId, request.Units), cancellationToken);
            return NoContent();
        }

        [HttpPost("{playerId:guid}/training/{orderId:guid}/speedup")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SpeedUpTraining(Guid playerId, Guid orderId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new SpeedUpTrainingCommand(playerId, orderId), cancellationToken);
            return NoContent();
        }

        /// <summary>Ціна викупу одного юніта в gems.</summary>
        private int RecoverCost(string unitType)
            => _gameConfig.Units.FirstOrDefault(u => u.Key == unitType)?.RecoverCostGems ?? 0;
    }
}
