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

        public MapController(IMediator mediator, IServerContext serverContext,TerrainGenerator terrain)
        {
            _mediator = mediator;
            _serverContext = serverContext;
            _terrain = terrain;
        }

        /// <summary>
        /// Ділянка карти навколо точки: місцевість (обчислюється) + окупанти (з БД).
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(MapAreaResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<MapAreaResponse>> GetArea([FromQuery] int centerX, [FromQuery] int centerY, [FromQuery][Range(1, 30)] int radius, CancellationToken cancellationToken)
        {
            var occupiedCells = await _mediator.Send(new GetMapAreaQuery(_serverContext.ServerId, centerX, centerY, radius), cancellationToken);

            var minX = centerX - radius;
            var minY = centerY - radius;
            var maxX = centerX + radius;
            var maxY = centerY + radius;

            var terrain = new List<MapTerrainCell>();
            for (var x = minX; x <= maxX; x++)
                for (var y = minY; y <= maxY; y++)
                {
                    if (!_terrain.IsInBounds(x, y))
                        continue;

                    var cell = _terrain.GetTerrain(_serverContext.ServerId, x, y);
                    terrain.Add(new MapTerrainCell(x, y, cell.Type, cell.Passable, cell.Habitable));
                }

            var occupants = occupiedCells.Select(c => new MapOccupantCell(c.X, c.Y, c.OccupantType.ToString(), c.OccupantId, null)).ToList();

            return Ok(new MapAreaResponse(minX, minY, maxX, maxY, terrain, occupants));

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
                details?.MonsterLevel, details?.MonsterUnits));
        }
    }
}
