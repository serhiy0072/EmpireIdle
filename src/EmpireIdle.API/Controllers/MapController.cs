using EmpireIdle.API.DTOs;
using EmpireIdle.Application.Map.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace EmpireIdle.API.Controllers
{
    /// <summary>Карта світу: місцевість і зайняті клітини.</summary>
    [ApiController]
    [Authorize]
    [Route("api/map")]
    public class MapController : ControllerBase
    {
        private readonly IMediator _mediator;

        public MapController(IMediator mediator) => _mediator = mediator;

        /// <summary>
        /// Ділянка карти навколо точки: місцевість (обчислюється) + окупанти й табори (з БД).
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(MapAreaResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<MapAreaResponse>> GetArea([FromQuery] int centerX, [FromQuery] int centerY, [FromQuery][Range(1, GetMapAreaQueryHandler.MaxRadius)] int radius, CancellationToken cancellationToken)
        {
            var area = await _mediator.Send(new GetMapAreaQuery(centerX, centerY, radius), cancellationToken);

            var terrain = area.Terrain
                .Select(t => new MapTerrainCell(t.X, t.Y, t.Type, t.Passable, t.Habitable))
                .ToList();

            var occupants = area.Occupants
                .Select(c => new MapOccupantCell(c.X, c.Y, c.OccupantType.ToString(), c.OccupantId, c.Name,
                    c.MonsterType, c.MonsterLevel, c.ClanId, c.ReadyAt))
                .ToList();

            var camps = (await _mediator.Send(new GetCampsInAreaQuery(centerX, centerY, radius), cancellationToken))
                .Select(c => new MapCampResponse(c.MarchId, c.X, c.Y, c.OwnerPlayerId, c.OwnerName, c.OwnerClanId, c.OwnerClanTag))
                .ToList();

            return Ok(new MapAreaResponse(area.MinX, area.MinY, area.MaxX, area.MaxY, terrain, occupants, camps));

        }
        /// <summary>
        /// Деталі клітини: місцевість і хто на ній стоїть.
        /// Для монстра показує склад загону — щоб напад був вибором, а не лотереєю.
        /// </summary>
        [HttpGet("cell/{x:int}/{y:int}")]
        [ProducesResponseType(typeof(MapCellDetailsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<MapCellDetailsResponse>> GetCell(int x, int y, CancellationToken cancellationToken)
        {
            var cell = await _mediator.Send(new GetMapCellQuery(x, y), cancellationToken);
            var occupant = cell.Occupant;

            return Ok(new MapCellDetailsResponse(
                cell.X, cell.Y,
                cell.TerrainType, cell.Passable, cell.Habitable, cell.MoveCost,
                occupant?.OccupantType, occupant?.OccupantId, occupant?.OccupantName,
                occupant?.MonsterLevel, occupant?.MonsterUnits, occupant?.ShieldUntil, occupant?.MonsterType));
        }
    }
}
