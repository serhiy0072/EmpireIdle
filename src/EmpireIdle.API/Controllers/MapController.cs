using EmpireIdle.Application.Map.Queries;
using EmpireIdle.Application.Map.ReadModels;
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
        [ProducesResponseType(typeof(MapAreaView), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<MapAreaView>> GetArea([FromQuery] int centerX, [FromQuery] int centerY, [FromQuery][Range(1, GetMapAreaQueryHandler.MaxRadius)] int radius, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetMapAreaQuery(centerX, centerY, radius), cancellationToken));

        /// <summary>
        /// Деталі клітини: місцевість і хто на ній стоїть.
        /// Для монстра показує склад загону — щоб напад був вибором, а не лотереєю.
        /// </summary>
        [HttpGet("cell/{x:int}/{y:int}")]
        [ProducesResponseType(typeof(MapCellView), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<MapCellView>> GetCell(int x, int y, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetMapCellQuery(x, y), cancellationToken));
    }
}
