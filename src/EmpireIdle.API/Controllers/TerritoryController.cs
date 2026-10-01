using EmpireIdle.API.DTOs;
using EmpireIdle.Application.Territory.Commands;
using EmpireIdle.Application.Territory.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmpireIdle.API.Controllers
{
    /// <summary>Кланова територія: споруди на карті, очки вкладу й квести клану (GDD §7.2).</summary>
    [ApiController]
    [Authorize]
    [Route("api/territory")]
    public class TerritoryController : ControllerBase
    {
        private readonly IMediator _mediator;

        public TerritoryController(IMediator mediator) => _mediator = mediator;

        /// <summary>
        /// Територія клану гравця. Гравець без клану отримує 200 і null:
        /// це нормальний стан екрана, а не помилка.
        /// </summary>
        [HttpGet("{playerId:guid}")]
        [ProducesResponseType(typeof(ClanTerritoryResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Get(Guid playerId, CancellationToken cancellationToken)
        {
            var view = await _mediator.Send(new GetClanTerritoryQuery(playerId), cancellationToken);

            if (view is null)
                return Ok(null);

            return Ok(new ClanTerritoryResponse(
                view.Enabled, view.Points, view.StructureCost, view.Radius, view.BuildMinutes, view.GarrisonCapacity,
                view.SlotsOpen, view.SlotsMax, view.CanBuild,
                view.SlotUnlocks.Select(u => new ClanSlotUnlockResponse(u.MinMembers, u.QuestKey, u.Unlocked)).ToList(),
                view.Structures.Select(s => new ClanStructureResponse(s.Id, s.X, s.Y, s.ReadyAt, s.AcceleratedShare,
                    s.GarrisonUnits, s.MyUnits)).ToList(),
                view.Quests.Select(q => new ClanQuestResponse(q.Key, q.DisplayName, q.ObjectiveType, q.ObjectiveTarget,
                    q.Amount, q.Target, q.Completed, q.ClanPoints, q.OpensSlot)).ToList(),
                view.Contributors.Select(c => new ClanContributorResponse(c.PlayerId, c.Name, c.Contribution)).ToList()));
        }

        /// <summary>Закласти споруду на вільній клітині. Платить клан очками вкладу; повертає id споруди.</summary>
        [HttpPost("{playerId:guid}/structures")]
        [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Place(Guid playerId, [FromBody] PlaceClanStructureRequest request,
            CancellationToken cancellationToken)
        {
            var id = await _mediator.Send(new PlaceClanStructureCommand(playerId, request.X, request.Y), cancellationToken);

            return Created((string?)null, id);
        }

        /// <summary>Знести власну споруду клану: гарнізон іде додому, слот звільняється, очки не повертаються.</summary>
        [HttpPost("{playerId:guid}/structures/{structureId:guid}/demolish")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Demolish(Guid playerId, Guid structureId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new DemolishClanStructureCommand(playerId, structureId), cancellationToken);

            return NoContent();
        }

        /// <summary>Забрати своїх юнітів і героїв із гарнізону споруди. false — там нічого гравцевого немає.</summary>
        [HttpPost("{playerId:guid}/structures/{structureId:guid}/recall")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Recall(Guid playerId, Guid structureId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new RecallFromStructureCommand(playerId, structureId), cancellationToken));
    }
}
