using EmpireIdle.Application.Market.Commands;
using EmpireIdle.Application.Market.Queries;
using Hangfire;

namespace EmpireIdle.API.Jobs
{
    /// <summary>
    /// Закриває лоти ринку, чий строк минув, і повертає товар продавцям.
    /// Щохвилини: купити прострочений лот однаково не можна, тож затримка
    /// коштує гравцеві лише хвилину очікування свого предмета.
    /// </summary>
    public class MarketExpiryJob
    {
        private readonly ServerJobRunner _runner;

        public MarketExpiryJob(ServerJobRunner runner) => _runner = runner;

        [DisableConcurrentExecution(timeoutInSeconds: JobDefaults.LockWaitSeconds)]
        public Task RunAsync(CancellationToken cancellationToken) => _runner.ForEachItemAsync(
            nameof(ExpireMarketListingCommand),
            (mediator, ct) => mediator.Send(new GetMarketListingIdsDueToExpireQuery(), ct),
            (mediator, listingId, ct) => mediator.Send(new ExpireMarketListingCommand(listingId), ct), cancellationToken);
    }
}
