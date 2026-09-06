using EmpireIdle.API.DTOs;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Players.Commands;
using EmpireIdle.Domain.Services;
using EmpireIdle.Infrastructure.Auth;
using EmpireIdle.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.Data;
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
        private readonly IPlayerRepository _playerRepository;
        private readonly IMediator _mediator;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IServerContext _serverContext;
        private readonly GameCatalog _catalog;

        public AuthController(AuthService authService, IPlayerRepository playerRepository, IMediator mediator, IUnitOfWork unitOfWork, IServerContext serverContext, GameCatalog catalog)
        {
            _authService = authService;
            _playerRepository = playerRepository;
            _mediator = mediator;
            _unitOfWork = unitOfWork;
            _serverContext = serverContext;
            _catalog = catalog;
        }

        /// <summary>
        /// Зареєструвати нового гравця: Identity user + Player + Village + Wallet.
        /// </summary>
        [HttpPost("register")]
        [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register([FromBody] DTOs.RegisterRequest request, CancellationToken cancellationToken)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                // 1. Identity user (валідація пароля, унікальність email)
                var userId = await _authService.RegisterAsync(request.UserName, request.Email, request.Password);

                // Реєстрація анонімна — світ беремо з конфіга
                _serverContext.UseServer(_catalog.Config.DefaultServerId);

                // 2. Доменний Player + Village + Garrison + Wallet
                await _mediator.Send(new CreatePlayerCommand(request.UserName, request.Email, userId), cancellationToken);

                await _unitOfWork.CommitTransactionAsync(cancellationToken);
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw; // GlobalExceptionHandler перетворить на 400
            }

            // 3. Логін — уже поза транзакцією, дані закомічені
            var (accessToken, refreshToken, playerId) = await _authService.LoginAsync(request.Email, request.Password);

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
            var (accessToken, refreshToken, playerId) = await _authService.LoginAsync(request.Email, request.Password);
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
            var (accessToken, refreshToken, playerId) = await _authService.RefreshAsync(request.RefreshToken);
            return Ok(new AuthResponse(accessToken, refreshToken, playerId));
        }
    }
}
