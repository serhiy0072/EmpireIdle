using EmpireIdle.API.DTOs;
using EmpireIdle.Application.Villages.Commands;
using EmpireIdle.Application.Villages.Queries;
using EmpireIdle.Application.Villages.ReadModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmpireIdle.API.Controllers
{
    /// <summary>Село гравця: стан, апгрейди будівель, збір виробітку.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class VillageController : ControllerBase
    {
        private readonly IMediator _mediator;

        public VillageController(IMediator mediator) => _mediator = mediator;

        /// <summary>
        /// Отримати стан села гравця з будівлями та ресурсами.
        /// </summary>
        [HttpGet("{playerId:guid}")]
        [ProducesResponseType(typeof(VillageView), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetVillage(Guid playerId, CancellationToken cancellationToken)
        {
            return Ok(await _mediator.Send(new GetVillageQuery(playerId), cancellationToken));
        }

        /// <summary>
        /// Переставити будівлю на плані села: вузол сітки всередині муру, без накладання на інші будівлі.
        /// </summary>
        [HttpPost("{playerId:guid}/buildings/{buildingId:guid}/move")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> MoveBuilding(Guid playerId, Guid buildingId, [FromBody] MoveBuildingRequest request,
            CancellationToken cancellationToken)
        {
            await _mediator.Send(new MoveBuildingCommand(playerId, buildingId, request.X, request.Y), cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Покращити будівлю в селі гравця.
        /// </summary>
        [HttpPost("{playerId:guid}/buildings/{buildingId:guid}/upgrade")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpgradeBuilding(Guid playerId, Guid buildingId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new UpgradeBuildingCommand(playerId, buildingId), cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Зібрати накопичені ресурси з буфера будівлі.
        /// </summary>
        [HttpPost("{playerId:guid}/buildings/{buildingId:guid}/collect")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CollectBuilding(Guid playerId, Guid buildingId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new CollectBuildingCommand(playerId, buildingId), cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Зібрати накопичені ресурси з усіх будівель села разом.
        /// </summary>
        [HttpPost("{playerId:guid}/collect-all")]
        [ProducesResponseType(typeof(CollectAllView), StatusCodes.Status200OK)]
        public async Task<IActionResult> CollectAllBuildings(Guid playerId, CancellationToken cancellationToken)
        {
            return Ok(await _mediator.Send(new CollectAllBuildingsCommand(playerId), cancellationToken));
        }

        /// <summary>
        /// Миттєво завершити будівництво за gems. Ціна залежить від часу,
        /// що лишився; прострочений таймер сканер закриє сам.
        /// </summary>
        [HttpPost("{playerId:guid}/buildings/{buildingId:guid}/speedup")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SpeedUpConstruction(Guid playerId, Guid buildingId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new SpeedUpConstructionCommand(playerId, buildingId), cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Миттєво відремонтувати всі пошкоджені будівлі за ресурси. Лише всі
        /// разом: серію поразок обнуляє тільки повне відновлення (GDD §2.6).
        /// </summary>
        [HttpPost("{playerId:guid}/repair")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Repair(Guid playerId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new RepairVillageCommand(playerId), cancellationToken);
            return NoContent();
        }

    }
}
