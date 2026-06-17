using EmpireIdle.API.DTOs;
using EmpireIdle.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace EmpireIdle.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PlayerController : ControllerBase
    {
        private readonly CreatePlayerService _createPlayerService;

        public PlayerController(CreatePlayerService createPlayerService)
        {
            _createPlayerService = createPlayerService;
        }

        /// <summary>
        /// Зареєструвати нового гравця. Створює Player, Village та PlayerWallet.
        /// </summary>
        [HttpPost("register")]
        [ProducesResponseType(typeof(PlayerResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register([FromBody] CreatePlayerRequest request, CancellationToken cancellationToken)
        {
            var playerId = await _createPlayerService.CreateAsync(request.Username, request.Email, cancellationToken);

            return CreatedAtAction(null, new PlayerResponse(playerId));

        }
    }
}
