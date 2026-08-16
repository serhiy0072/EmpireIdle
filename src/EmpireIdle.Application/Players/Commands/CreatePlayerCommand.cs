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
    public record CreatePlayerCommand(string UserName, string Email, string UserId) : IRequest<Guid>;


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
        private readonly GameCatalog _catalog;

        public CreatePlayerCommandHandler(
            IPlayerRepository playerRepository, 
            IVillageRepository villageRepository, 
            IPlayerWalletRepository walletRepository, 
            IGarrisonRepository garrisonRepository, 
            IUnitOfWork unitOfWork, 
            ILogger<CreatePlayerCommand> logger, 
            GameCatalog catalog,
            SettlementPlacer settlementPlacer,
            IMapRepository mapRepository)
        {
            _playerRepository = playerRepository;
            _villageRepository = villageRepository;
            _walletRepository = walletRepository;
            _garrisonRepository = garrisonRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _catalog = catalog;
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

            var player = new Player(playerId, request.UserName, email, request.UserId);
            var wallet = new PlayerWallet(Guid.NewGuid(), request.UserId);
            var (x, y) = await _settlementPlacer.FindSpotAsync(
                serverId: 1,
                isOccupied: (cx, cy) => _mapRepository.IsOccupiedAsync(1, cx, cy, cancellationToken),
                maxAttempts: 200);

            var village = new Village(Guid.NewGuid(), playerId, $"{request.UserName}'s Village",
                _catalog.Resources.Keys,
                x, y);

            village.GrantStartingResources(_catalog.Config.StartingResources);

            foreach (var buildingKey in _catalog.Config.StartingBuildings)
                village.AddBuilding(buildingKey, _catalog.Buildings);

            var garrison = new Garrison(Guid.NewGuid(), village.Id);
            await _garrisonRepository.AddAsync(garrison, cancellationToken);

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
