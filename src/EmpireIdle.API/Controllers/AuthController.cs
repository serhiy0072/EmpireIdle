using EmpireIdle.API.DTOs;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Players.Commands;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Infrastructure.Auth;
using MediatR;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using System.Security.Principal;

namespace EmpireIdle.API.Controller
{
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly IPlayerRepository _playerRepository;
        private readonly IMediator _mediator;

        public AuthController(AuthService authService, IPlayerRepository playerRepository, IMediator mediator)
        {
            _authService = authService;
            _playerRepository = playerRepository;
            _mediator = mediator;
        }

        /// <summary>
        /// Зареєструвати нового гравця: Identity user + Player + Village + Wallet.
        /// </summary>
        [HttpPost("register")]
        [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register([FromBody] DTOs.RegisterRequest request, CancellationToken cancellationToken)
        {
            // 1. Створити Identity user (валідація пароля, унікальність email)
            await _authService.RegisterAsync(request.UserName, request.Email, request.Password);

            // 2. Створити доменного Player + Village + Wallet
            var playerId = await _mediator.Send(new CreatePlayerCommand(request.UserName, request.Email), cancellationToken);

            // 3. Одразу залогінити
            var (accessToken, refreshToken) = await _authService.LoginAsync(request.Email, request.Password);

            var response = new AuthResponse(accessToken, refreshToken, playerId);
            return Created((string?)null, response);
        }

        /// <summary>
        /// Залогінитись і отримати JWT токени.д
        /// </summary>
        [HttpPost("login")]
        [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Login([FromBody] DTOs.LoginRequest request, CancellationToken cancellationToken)
        {
            var (accessToken, refreshToken) = await _authService.LoginAsync(request.Email, request.Password);

            var player = await _playerRepository.GetByEmailAsync(request.Email, cancellationToken)
                    ?? throw new InvalidOperationException("Player not found for this account.");

            return Ok(new AuthResponse(accessToken, refreshToken, player.Id));
        }

        /// <summary>
        /// Оновити access token за refresh token (з ротацією).
        /// </summary>
        [HttpPost("refresh")]
        [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Refresh([FromBody] DTOs.RefreshRequest request, CancellationToken cancellationToken)
        {
            var (accessToken, refreshToken) = await _authService.RefreshAsync(request.RefreshToken);

            return Ok(new AuthResponse(accessToken, refreshToken, Guid.Empty));
        }
    }
}
