using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmpireIdle.Application.Services
{
    /// <summary>
    /// Реєстрація нового гравця — створює Player, Village з початковими ресурсами, і PlayerWallet.
    /// </summary>
    public class CreatePlayerService
    {
        private readonly IPlayerRepository _playerRepository;
        private readonly IVillageRepository _villageRepository;
        private readonly IPlayerWalletRepository _walletRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<CreatePlayerService> _logger;
        private readonly GameConfig _gameConfig;

        public CreatePlayerService(IPlayerRepository playerRepository, IVillageRepository villageRepository, IPlayerWalletRepository walletRepository,
            IUnitOfWork unitOfWork, ILogger<CreatePlayerService> logger, IOptions<GameConfig> gameConfig)
        {
            _playerRepository = playerRepository;
            _villageRepository = villageRepository;
            _walletRepository = walletRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _gameConfig = gameConfig.Value;
        }

        /// <summary>
        /// Створити нового гравця з селом і гаманцем.
        /// </summary>
        /// <returns>Id створеного гравця.</returns>
        public async Task<Guid> CreateAsync(string username, string email, CancellationToken cancellationToken = default)
        {
            var existing = await _playerRepository.GetByEmailAsync(email, cancellationToken);
            if (existing is not null)
                throw new InvalidOperationException($"Player with email '{email}' already exists.");

            var playerId = Guid.NewGuid();

            var player = new Player(playerId, username, email);
            var village = new Village(Guid.NewGuid(), playerId, $"{username}'s Village");
            var wallet = new PlayerWallet(Guid.NewGuid(), playerId);

            await _playerRepository.AddAsync(player, cancellationToken);
            await _villageRepository.AddAsync(village, cancellationToken);
            await _walletRepository.AddAsync(wallet, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} created: {Username} ({Email})", playerId, username, email);

            return playerId;
        }

    }
}