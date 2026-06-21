using EmpireIdle.API.DTOs;
using EmpireIdle.Application.Villages.Commands;
using EmpireIdle.Application.Villages.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmpireIdle.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class VillageController : ControllerBase
    {
        private readonly IMediator _mediator;
        public VillageController(IMediator mediator)
        {
            _mediator = mediator;
        }
        /// <summary>
        /// Отримати стан села гравця з будівлями та ресурсами.
        /// </summary>
        [HttpGet("{playerId:guid}")]
        [ProducesResponseType(typeof(VillageResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetVillage(Guid playerId, CancellationToken cancellationToken)
        {
            var village = await _mediator.Send(new GetVillageQuery(playerId), cancellationToken);

            var response = new VillageResponse(
                village.Id,
                village.Name,
                village.LastTickAt,
                village.Buildings.Select(b => new BuildingResponse(b.Id, b.Type, b.Level.Value, b.LastCollectedAt)).ToList(),
                village.Resources.Select(r => new ResourceResponse(r.ResourceType, r.Amount)).ToList());

            return Ok(response);
        }

        /// <summary>
        /// Побудувати нову будівлю в селі гравця.
        /// </summary>
        [HttpPost("{playerId:guid}/buildings")]
        [ProducesResponseType(typeof(PlayerResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> AddBuilding(Guid playerId, [FromBody] AddBuildingRequest request, CancellationToken cancellationToken)
        {
            var buildingId = await _mediator.Send(new AddBuildingCommand(playerId, request.BuildingType), cancellationToken);

            return CreatedAtAction(nameof(GetVillage), new { playerId }, new PlayerResponse(buildingId));
        }

        /// <summary>
        /// Апгрейдити будівлю в селі гравця.
        /// </summary>
        [HttpPost("{playerId:guid}/buildings/upgrade")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpgradeBuilding(Guid playerId, [FromBody] UpgradeBuildingRequest request, CancellationToken cancellationToken)
        {
            await _mediator.Send(new UpgradeBuildingCommand(playerId, request.BuildingId), cancellationToken);
            return NoContent();
        }

    }
}
