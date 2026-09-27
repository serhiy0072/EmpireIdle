using EmpireIdle.API.DTOs;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Map.Queries;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
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
        private readonly IServerContext _serverContext;
        private readonly TerrainGenerator _terrain;

        public MapController(IMediator mediator, IServerContext serverContext, TerrainGenerator terrain)
        {
            _mediator = mediator;
            _serverContext = serverContext;
            _terrain = terrain;
        }

        /// <summary>
        /// Ділянка карти навколо точки: місцевість (обчислюється) + окупанти й табори (з БД).
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(MapAreaResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<MapAreaResponse>> GetArea([FromQuery] int centerX, [FromQuery] int centerY, [FromQuery][Range(1, GetMapAreaQueryHandler.MaxRadius)] int radius, CancellationToken cancellationToken)
        {
            var area = await _mediator.Send(new GetMapAreaQuery(_serverContext.ServerId, centerX, centerY, radius), cancellationToken);

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
            if (!_terrain.IsInBounds(x, y))
                throw new RequirementNotMetException($"Cell ({x},{y}) is outside the map.");

            var cell = _terrain.GetTerrain(_serverContext.ServerId, x, y);
            var details = await _mediator.Send(new GetMapCellQuery(_serverContext.ServerId, x, y), cancellationToken);

            return Ok(new MapCellDetailsResponse(
                x, y,
                cell.Type, cell.Passable, cell.Habitable, cell.MoveCost,
                details?.OccupantType, details?.OccupantId, details?.OccupantName,
                details?.MonsterLevel, details?.MonsterUnits, details?.ShieldUntil));
        }
    }
}
