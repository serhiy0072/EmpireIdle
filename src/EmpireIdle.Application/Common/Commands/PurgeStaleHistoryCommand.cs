using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Common.Commands
{
    /// <summary>
    /// Прибирає історію всіх світів, старшу за строки з GameConfig.Retention: звіти боїв і розвідки,
    /// завершені забіги, закриті лоти, вирішені заявки в клан. Системна команда джоба —
    /// гра пише ці рядки на кожну дію, а читає лише свіжі.
    /// </summary>
    public record PurgeStaleHistoryCommand : IRequest;

    public sealed class PurgeStaleHistoryCommandHandler : IRequestHandler<PurgeStaleHistoryCommand>
    {
        private readonly IBattleReportRepository _battleReports;
        private readonly IScoutReportRepository _scoutReports;
        private readonly IDungeonRepository _dungeons;
        private readonly IMarketRepository _market;
        private readonly IClanRequestRepository _clanRequests;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<PurgeStaleHistoryCommandHandler> _logger;

        public PurgeStaleHistoryCommandHandler(
            IBattleReportRepository battleReports,
            IScoutReportRepository scoutReports,
            IDungeonRepository dungeons,
            IMarketRepository market,
            IClanRequestRepository clanRequests,
            GameCatalog catalog,
            TimeProvider timeProvider,
            ILogger<PurgeStaleHistoryCommandHandler> logger)
        {
            _battleReports = battleReports;
            _scoutReports = scoutReports;
            _dungeons = dungeons;
            _market = market;
            _clanRequests = clanRequests;
            _catalog = catalog;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(PurgeStaleHistoryCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var retention = _catalog.Config.Retention;

            var battleReports = await _battleReports.DeleteBeforeAsync(now.AddDays(-retention.BattleReportDays), cancellationToken);
            var scoutReports = await _scoutReports.DeleteBeforeAsync(now.AddDays(-retention.ScoutReportDays), cancellationToken);
            var runs = await _dungeons.DeleteFinishedBeforeAsync(now.AddDays(-retention.FinishedDungeonRunDays), cancellationToken);
            var listings = await _market.DeleteClosedBeforeAsync(now.AddDays(-retention.ClosedMarketListingDays), cancellationToken);
            var clanRequests = await _clanRequests.DeleteStaleBeforeAsync(now.AddDays(-retention.ResolvedClanRequestDays), cancellationToken);

            _logger.LogInformation(
                "Purged history: {BattleReports} battle reports, {ScoutReports} scout reports, {Runs} dungeon runs, {Listings} market listings, {ClanRequests} clan requests",
                battleReports, scoutReports, runs, listings, clanRequests);
        }
    }
}
