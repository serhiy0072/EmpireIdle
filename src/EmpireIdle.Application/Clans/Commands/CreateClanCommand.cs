using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Clans.Commands
{
    /// <summary>
    /// Створює клан. Засновник стає лідером, клан отримує стандартний
    /// набір ролей — далі лідер їх правитиме під себе.
    /// </summary>
    public record CreateClanCommand(Guid PlayerId, string Name, string Tag)
        : IRequest<Guid>, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class CreateClanCommandHandler : IRequestHandler<CreateClanCommand, Guid>
    {
        private readonly IClanRepository _clanRepository;
        private readonly IPlayerRepository _playerRepository;
        private readonly IVillageRepository _villageRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IServerContext _serverContext;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<CreateClanCommandHandler> _logger;

        public CreateClanCommandHandler(
            IClanRepository clanRepository,
            IPlayerRepository playerRepository,
            IVillageRepository villageRepository,
            IUnitOfWork unitOfWork,
            IServerContext serverContext,
            GameCatalog catalog,
            TimeProvider timeProvider,
            ILogger<CreateClanCommandHandler> logger)
        {
            _clanRepository = clanRepository;
            _playerRepository = playerRepository;
            _villageRepository = villageRepository;
            _unitOfWork = unitOfWork;
            _serverContext = serverContext;
            _catalog = catalog;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<Guid> Handle(CreateClanCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var player = await _playerRepository.GetByIdAsync(request.PlayerId, cancellationToken)
                ?? throw new EntityNotFoundException("Player", request.PlayerId);

            if (player.ClanId is not null)
                throw new InvalidStateException("Leave your current clan before founding a new one.");

            await EnsureEmbassyAsync(player.Id, cancellationToken);

            var name = request.Name.Trim();
            var tag = request.Tag.Trim().ToUpperInvariant();

            // Перевірка тут, а унікальні індекси в базі — арбітр гонки:
            // між перевіркою і вставкою хтось інший може взяти ту саму назву
            if (await _clanRepository.ExistsAsync(name, tag, cancellationToken))
                throw new AlreadyExistsException("Clan", $"{name} [{tag}]");

            var clan = new Clan(Guid.NewGuid(), _serverContext.ServerId, name, tag, player.Id, now);

            await _clanRepository.AddAsync(clan, cancellationToken);
            player.JoinClan(clan.Id);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Clan {ClanId} '{Name}' [{Tag}] founded by player {PlayerId}",
                clan.Id, name, tag, player.Id);

            return clan.Id;
        }

        /// <summary>
        /// Клан потребує посольства — тієї самої будівлі, що приймає підкріплення.
        /// Інакше засновник створив би клан, у який фізично не може приймати допомогу.
        /// </summary>
        private async Task EnsureEmbassyAsync(Guid playerId, CancellationToken cancellationToken)
        {
            var village = await _villageRepository.GetByPlayerIdAsync(playerId, cancellationToken)
                ?? throw new EntityNotFoundException("Village for player", playerId);

            var embassyKey = _catalog.Buildings.Values
                .FirstOrDefault(b => b.ReinforcementSlotsPerLevel > 0)?.Key;

            if (embassyKey is null)
                return;

            if (!village.IsUnlocked(embassyKey, _catalog.Buildings, _catalog.MainBuildingKey))
                throw new RequirementNotMetException($"Founding a clan requires the '{embassyKey}'.");
        }
    }
}
