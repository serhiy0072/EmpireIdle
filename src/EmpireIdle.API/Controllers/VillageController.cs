using EmpireIdle.API.DTOs;
using EmpireIdle.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace EmpireIdle.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VillageController : ControllerBase
    {
        private readonly GetVillageService _getVillageService;
        private readonly AddBuildingService _addBuildingService;
        private readonly UpgradeBuildingService _upgradeBuildingService;
        public VillageController(GetVillageService getVillageService, AddBuildingService addBuildingService, UpgradeBuildingService upgradeBuildingService)
        {
            _getVillageService = getVillageService;
            _addBuildingService = addBuildingService;
            _upgradeBuildingService = upgradeBuildingService;
        }
        /// <summary>
        /// Отримати стан села гравця з будівлями та ресурсами.
        /// </summary>
        [HttpGet("{playerId:guid}")]
        [ProducesResponseType(typeof(VillageResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetVillage(Guid playerId, CancellationToken cancellationToken)
        {
            var village = await _getVillageService.GetByPlayerIdAsync(playerId, cancellationToken);

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
            var buildingId = await _addBuildingService.AddAsync(playerId, request.BuildingType, cancellationToken);

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
            await _upgradeBuildingService.UpgradeAsync(playerId, request.BuildingId, cancellationToken);

            return NoContent();
        }

    }
}
