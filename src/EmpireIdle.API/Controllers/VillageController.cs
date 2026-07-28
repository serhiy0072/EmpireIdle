using EmpireIdle.API.DTOs;
using EmpireIdle.Application.Garrisons.Commands;
using EmpireIdle.Application.Villages.Commands;
using EmpireIdle.Application.Villages.Queries;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace EmpireIdle.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class VillageController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly GameConfig _gameConfig;

        public VillageController(IMediator mediator, IOptions<GameConfig> gameConfig)
        {
            _mediator = mediator;
            _gameConfig = gameConfig.Value;
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

            var buildingConfigMap = _gameConfig.Buildings.ToDictionary(bc => bc.Key);

            var response = new VillageResponse(
                village.Id,
                village.Name,
                village.LastTickAt,
                village.Buildings.Select(b =>
                {
                    var storageCap = buildingConfigMap.TryGetValue(b.Type, out var cfg)
                        ? b.GetStorageCap(cfg.BaseStorage, cfg.StorageGrowth)
                        : 0;
                    return new BuildingResponse(b.Id, b.Type, b.Level.Value, b.LastCollectedAt, b.StoredAmount, storageCap, b.ConstructionCompletesAt, b.IsUnderConstruction);
                }).ToList(),
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

        //[HttpPost("{playerId:guid}/buildings/{buildingId:guid}/speedup")]
        //[ProducesResponseType(StatusCodes.Status204NoContent)]
        //[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        //public async Task<IActionResult> SpeedUpConstruction(Guid playerId, Guid buildingId, CancellationToken cancellationToken)
        //{
        //    await _mediator.Send(new SpeedUpConstructionCommand(playerId, buildingId), cancellationToken);
        //    return NoContent();
        //}

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
    }
}