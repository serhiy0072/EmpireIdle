using EmpireIdle.Application.Beasts.ReadModels;
using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Beasts.Commands
{
    /// <summary>
    /// Нагодувати звіра кормом (GDD §5.10). Корм веде рівень до стелі рангу;
    /// з'їдається лише потрібне, решта лишається в інвентарі.
    /// </summary>
    public record FeedBeastCommand(Guid PlayerId, string BeastKey, int Count)
        : IRequest<BeastFedView>, IPlayerScopedRequest, IIdempotentRequest;

    internal sealed class FeedBeastCommandHandler : IRequestHandler<FeedBeastCommand, BeastFedView>
    {
        private readonly IBeastPenRepository _pens;
        private readonly IInventoryRepository _inventory;
        private readonly IUnitOfWork _unitOfWork;
        private readonly BeastProgression _progression;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<FeedBeastCommandHandler> _logger;

        public FeedBeastCommandHandler(IBeastPenRepository pens, IInventoryRepository inventory, IUnitOfWork unitOfWork,
            BeastProgression progression, GameCatalog catalog, TimeProvider timeProvider, ILogger<FeedBeastCommandHandler> logger)
        {
            _pens = pens;
            _inventory = inventory;
            _unitOfWork = unitOfWork;
            _progression = progression;
            _catalog = catalog;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<BeastFedView> Handle(FeedBeastCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var settings = _catalog.Config.Beasts;

            var pen = await _pens.GetByPlayerAsync(request.PlayerId, cancellationToken)
                ?? throw new EntityNotFoundException("Beast", request.BeastKey);

            var feed = await _inventory.GetItemAsync(request.PlayerId, settings.FeedItemKey, cancellationToken);
            var have = feed?.Count ?? 0;

            if (have < request.Count)
                throw new RequirementNotMetException(RefusalReasons.BeastNotEnoughFeed,
                    $"Not enough feed: need {request.Count}, have {have}.", request.Count, have);

            var eaten = pen.Feed(request.BeastKey, request.Count, _progression.ExperienceToNext, settings.LevelsPerRank, now);

            var beast = pen.Beasts.Single(b => b.BeastKey == request.BeastKey);

            if (eaten == 0)
                throw new RequirementNotMetException(RefusalReasons.BeastLevelCapped,
                    $"Beast '{request.BeastKey}' is at its level cap for rank {beast.Rank}.", _progression.MaxLevel(beast.Rank));

            // Предмет списується в тій самій транзакції, що й рівень: інакше збій між ними роздав би рівні задарма
            feed!.Consume(eaten);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} fed {Eaten} to beast {BeastKey}, now level {Level}",
                request.PlayerId, eaten, request.BeastKey, beast.Level);

            return new BeastFedView(request.BeastKey, beast.Level, beast.Experience, eaten);
        }
    }
}
