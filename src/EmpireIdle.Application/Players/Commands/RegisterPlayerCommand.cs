using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmpireIdle.Application.Players.Commands
{
    /// <summary>
    /// Реєстрація: акаунт входу + Player + Village + Garrison + PlayerWallet + клітина карти.
    /// </summary>
    public record RegisterPlayerCommand(string UserName, string Email, string Password) : IRequest<Guid>
    {
        // Пароль не має потрапити в лог через ToString запису
        public override string ToString() => $"{nameof(RegisterPlayerCommand)} {{ UserName = {UserName} }}";
    }

    /// <summary>
    /// Обробник реєстрації. Акаунт і світ гравця створюються в одній транзакції:
    /// або гравець є повністю, або немає нічого — і акаунта без села теж.
    /// </summary>
    /// <returns>Id створеного гравця.</returns>
    public sealed class RegisterPlayerCommandHandler : IRequestHandler<RegisterPlayerCommand, Guid>
    {
        private readonly IUserAccounts _accounts;
        private readonly IServerContext _serverContext;
        private readonly IPlayerRepository _playerRepository;
        private readonly IVillageRepository _villageRepository;
        private readonly IPlayerWalletRepository _walletRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IMapRepository _mapRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IServerRepository _serverRepository;
        private readonly ILogger<RegisterPlayerCommandHandler> _logger;
        private readonly TimeProvider _timeProvider;
        private readonly SettlementPlacer _settlementPlacer;
        private readonly GameCatalog _catalog;

        public RegisterPlayerCommandHandler(
            IUserAccounts accounts,
            IServerContext serverContext,
            IPlayerRepository playerRepository,
            IVillageRepository villageRepository,
            IPlayerWalletRepository walletRepository,
            IGarrisonRepository garrisonRepository,
            IUnitOfWork unitOfWork,
            IServerRepository serverRepository,
            ILogger<RegisterPlayerCommandHandler> logger,
            TimeProvider timeProvider,
            GameCatalog catalog,
            SettlementPlacer settlementPlacer,
            IMapRepository mapRepository)
        {
            _accounts = accounts;
            _serverContext = serverContext;
            _playerRepository = playerRepository;
            _villageRepository = villageRepository;
            _walletRepository = walletRepository;
            _garrisonRepository = garrisonRepository;
            _unitOfWork = unitOfWork;
            _serverRepository = serverRepository;
            _logger = logger;
            _catalog = catalog;
            _timeProvider = timeProvider;
            _settlementPlacer = settlementPlacer;
            _mapRepository = mapRepository;
        }

        public async Task<Guid> Handle(RegisterPlayerCommand request, CancellationToken cancellationToken)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                var userId = await _accounts.CreateAsync(request.UserName, request.Email, request.Password, cancellationToken);
                var playerId = await CreatePlayerAsync(request, userId, cancellationToken);

                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                return playerId;
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
                throw;
            }
        }

        private async Task<Guid> CreatePlayerAsync(RegisterPlayerCommand request, string userId, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var email = request.Email.Trim().ToLowerInvariant();

            // Реєстрація анонімна — світ береться з конфіга, а не з токена
            var serverId = _catalog.Config.DefaultServerId;
            _serverContext.UseServer(serverId);

            var playerId = Guid.NewGuid();

            var player = new Player(playerId, request.UserName, email, userId, now, serverId);
            var wallet = new PlayerWallet(Guid.NewGuid(), userId);

            var (x, y) = await _settlementPlacer.FindSpotAsync(
                serverId: serverId,
                serverLevel: await _serverRepository.GetLevelAsync(serverId, cancellationToken),
                isOccupied: (cx, cy) => _mapRepository.IsOccupiedAsync(serverId, cx, cy, cancellationToken),
                maxAttempts: 200);

            var village = new Village(Guid.NewGuid(), playerId, $"{request.UserName}'s Village",
                _catalog.Resources.Keys,
                x, y, serverId);

            village.GrantStartingResources(_catalog.Config.StartingResources, now);

            // Селище створюється повним: усі будівлі 1 рівня, недоступні під туманом
            foreach (var buildingKey in _catalog.Buildings.Keys)
                village.AddBuilding(buildingKey, _catalog.Buildings, now);

            var garrison = new Garrison(Guid.NewGuid(), village.Id, village.ServerId);
            await _garrisonRepository.AddAsync(garrison, cancellationToken);

            await _playerRepository.AddAsync(player, cancellationToken);
            await _villageRepository.AddAsync(village, cancellationToken);
            await _walletRepository.AddAsync(wallet, cancellationToken);
            await _mapRepository.AddAsync(
                new MapCell(Guid.NewGuid(), serverId, x, y, MapOccupantType.Village, village.Id),
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} created: {Username} ({Email})",
                playerId, request.UserName, request.Email);

            return playerId;
        }
    }
}
