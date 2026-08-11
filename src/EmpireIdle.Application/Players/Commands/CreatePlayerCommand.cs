using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmpireIdle.Application.Players.Commands
{
    /// <summary>
    /// Команда створення нового гравця: Player + Village + PlayerWallet.
    /// </summary>
    public record CreatePlayerCommand(string UserName, string Email) : IRequest<Guid>;

    /// <summary>
    /// Обробник команди CreatePlayerCommand. Повертає Id створеного гравця.
    /// </summary>
    public class CreatePlayerCommandHandler : IRequestHandler<CreatePlayerCommand, Guid>
    {
        private readonly IPlayerRepository _playerRepository;
        private readonly IVillageRepository _villageRepository;
        private readonly IPlayerWalletRepository _walletRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IMapRepository _mapRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<CreatePlayerCommand> _logger;
        private readonly SettlementPlacer _settlementPlacer;
        private readonly GameConfig _gameConfig;

        public CreatePlayerCommandHandler(
            IPlayerRepository playerRepository, 
            IVillageRepository villageRepository, 
            IPlayerWalletRepository walletRepository, 
            IGarrisonRepository garrisonRepository, 
            IUnitOfWork unitOfWork, 
            ILogger<CreatePlayerCommand> logger, 
            IOptions<GameConfig> gameConfig,
            SettlementPlacer settlementPlacer,
            IMapRepository mapRepository)
        {
            _playerRepository = playerRepository;
            _villageRepository = villageRepository;
            _walletRepository = walletRepository;
            _garrisonRepository = garrisonRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _gameConfig = gameConfig.Value;
            _settlementPlacer = settlementPlacer;
            _mapRepository = mapRepository;
        }

        public async Task<Guid> Handle(CreatePlayerCommand request, CancellationToken cancellationToken)
        {
            var email = request.Email.Trim().ToLowerInvariant();

            var existing = await _playerRepository.GetByEmailAsync(email, cancellationToken);
            if(existing is not null)
                throw new InvalidOperationException($"Player with email '{email}' already exists.");

            var playerId = Guid.NewGuid();

            var player = new Player(playerId, request.UserName, email);
            var wallet = new PlayerWallet(Guid.NewGuid(), playerId);
            var (x, y) = await _settlementPlacer.FindSpotAsync(
                serverId: 1,
                isOccupied: (cx, cy) => _mapRepository.IsOccupiedAsync(1, cx, cy, cancellationToken),
                maxAttempts: 200);

            var village = new Village(Guid.NewGuid(), playerId, $"{request.UserName}'s Village",
                _gameConfig.Resources.Select(r => r.Key),
                _gameConfig.Zones.Select(z => (z.Type, z.Slots)),
                x, y);

            var garrison = new Garrison(Guid.NewGuid(), village.Id);
            await _garrisonRepository.AddAsync(garrison, cancellationToken);

            var buildingConfigs = _gameConfig.Buildings.ToDictionary(b => b.Key, b => b);

            foreach (var buildingKey in _gameConfig.StartingBuildings)
                village.AddBuilding(buildingKey, buildingConfigs);

            await _playerRepository.AddAsync(player, cancellationToken);
            await _villageRepository.AddAsync(village, cancellationToken);
            await _walletRepository.AddAsync(wallet, cancellationToken);
            await _mapRepository.AddAsync(
                new MapCell(Guid.NewGuid(), 1, x, y, MapOccupantType.Village, village.Id),
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} created: {Username} ({Email})",
                playerId, request.UserName, request.Email);

            return playerId;
        }
    }
}
