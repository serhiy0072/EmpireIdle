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

        [DisableConcurrentExecution(timeoutInSeconds: 300)]
        public Task RunAsync() => _runner.ForEachItemAsync(
            nameof(ExpireMarketListingCommand),
            mediator => mediator.Send(new GetMarketListingIdsDueToExpireQuery()),
            (mediator, listingId) => mediator.Send(new ExpireMarketListingCommand(listingId)));
    }
}
