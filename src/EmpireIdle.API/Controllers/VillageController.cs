using EmpireIdle.API.DTOs;
using EmpireIdle.Application.Villages.Commands;
using EmpireIdle.Application.Villages.Queries;
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
        [ProducesResponseType(typeof(VillageResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetVillage(Guid playerId, CancellationToken cancellationToken)
        {
            var village = await _mediator.Send(new GetVillageQuery(playerId), cancellationToken);

            var response = new VillageResponse(
                village.Id,
                village.Name,
                village.X,
                village.Y,
                village.Buildings.Select(b => new BuildingResponse(
                    b.Id, b.Type, b.Level, b.LastCollectedAt, b.StoredAmount, b.StorageCap,
                    b.ConstructionCompletesAt, b.IsUnderConstruction, b.SpeedUpCostGems, b.IsUnlocked,
                    b.DamageLevel, b.DamagedUntil)).ToList(),
                village.Resources.Select(r => new ResourceResponse(r.ResourceType, r.Amount, r.IsUnlocked)).ToList(),
                village.ShieldUntil,
                village.Damage is null
                    ? null
                    : new VillageDamageResponse(village.Damage.DefeatStreak, village.Damage.DefeatsToEvict,
                        village.Damage.RepairCost.Select(c => new RepairCostResponse(c.Resource, c.Amount)).ToList()));

            return Ok(response);
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
        /// Повний склад не зупиняє збір решти — він повертається у FullStorages.
        /// </summary>
        [HttpPost("{playerId:guid}/collect-all")]
        [ProducesResponseType(typeof(CollectAllResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> CollectAllBuildings(Guid playerId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new CollectAllBuildingsCommand(playerId), cancellationToken);

            return Ok(new CollectAllResponse(
                result.Collected.Select(c => new CollectedResourceResponse(c.ResourceType, c.Amount)).ToList(),
                result.FullStorages));
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
