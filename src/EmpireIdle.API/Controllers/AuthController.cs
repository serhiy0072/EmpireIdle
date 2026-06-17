using EmpireIdle.API.DTOs;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Services;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;
using System.Security.Principal;

namespace EmpireIdle.API.Controller
{
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly CreatePlayerService _createPlayerService;
        private readonly IPlayerRepository _playerRepository;

        public AuthController(AuthService authService, CreatePlayerService createPlayerService, IPlayerRepository playerRepository)
        {
            _authService = authService;
            _createPlayerService = createPlayerService;
            _playerRepository = playerRepository;
        }

        /// <summary>
        /// Зареєструвати нового гравця: Identity user + Player + Village + Wallet.
        /// </summary>
        [HttpPost("register")]
        [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
        {
            // 1. Створити Identity user (валідація пароля, унікальність email)
            await _authService.RegisterAsync(request.UserName, request.Email, request.Password);

            // 2. Створити доменного Player + Village + Wallet
            var playerId = await _createPlayerService.CreateAsync(request.UserName, request.Email, cancellationToken);

            // 3. Одразу залогінити
            var (accessToken, refreshToken) = await _authService.LoginAsync(request.Email, request.Password);

            return CreatedAtAction(null, new AuthResponse(accessToken, refreshToken, playerId));
        }

        /// <summary>
        /// Залогінитись і отримати JWT токени.д
        /// </summary>
        [HttpPost("login")]
        [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
        {
            var (accessToken, refreshToken) = await _authService.LoginAsync(request.Email, request.Password);

            var player = await _playerRepository.GetByEmailAsync(request.Email, cancellationToken)
                    ?? throw new InvalidOperationException("Player not found for this account.");

            return Ok(new AuthResponse(accessToken, refreshToken, player.Id));
        }
    }
}
