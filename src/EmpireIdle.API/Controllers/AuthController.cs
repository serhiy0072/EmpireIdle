using EmpireIdle.API.DTOs;
using EmpireIdle.Application.Players.Commands;
using EmpireIdle.Infrastructure.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EmpireIdle.API.Controllers
{
    /// <summary>Реєстрація, вхід і ротація токенів. Єдиний анонімний контролер.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly IMediator _mediator;

        public AuthController(AuthService authService, IMediator mediator)
        {
            _authService = authService;
            _mediator = mediator;
        }

        /// <summary>
        /// Зареєструвати нового гравця: акаунт + Player + Village + Wallet, потім вхід.
        /// </summary>
        [HttpPost("register")]
        [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register([FromBody] DTOs.RegisterRequest request, CancellationToken cancellationToken)
        {
            await _mediator.Send(new RegisterPlayerCommand(request.UserName, request.Email, request.Password), cancellationToken);

            // Логін — уже поза транзакцією, дані закомічені
            var (accessToken, refreshToken, playerId) = await _authService.LoginAsync(request.Email, request.Password, cancellationToken);

            return Created((string?)null, new AuthResponse(accessToken, refreshToken, playerId));
        }

        /// <summary>
        /// Залогінитись і отримати JWT токени.
        /// </summary>
        [HttpPost("login")]
        [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Login([FromBody] DTOs.LoginRequest request, CancellationToken cancellationToken)
        {
            var (accessToken, refreshToken, playerId) = await _authService.LoginAsync(request.Email, request.Password, cancellationToken);
            return Ok(new AuthResponse(accessToken, refreshToken, playerId));
        }

        /// <summary>
        /// Оновити access token за refresh token (з ротацією).
        /// </summary>
        [HttpPost("refresh")]
        [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Refresh([FromBody] DTOs.RefreshRequest request, CancellationToken cancellationToken)
        {
            var (accessToken, refreshToken, playerId) = await _authService.RefreshAsync(request.RefreshToken, cancellationToken);
            return Ok(new AuthResponse(accessToken, refreshToken, playerId));
        }
    }
}
