using EmpireIdle.Application.Market.Commands;
using Hangfire;

namespace EmpireIdle.API.Jobs
{
    /// <summary>
    /// Щогодини перераховує знімки медіани ринку кожного світу (GDD §8.8).
    /// Година, а не хвилина: коридор має рухатись повільніше за серію
    /// змовлених угод, інакше його розганяли б за один вечір.
    /// </summary>
    public class MarketPricesJob
    {
        private readonly ServerJobRunner _runner;

        public MarketPricesJob(ServerJobRunner runner) => _runner = runner;

        [DisableConcurrentExecution(timeoutInSeconds: 600)]
        public Task RunAsync() => _runner.ForEachServerAsync(
            nameof(MarketPricesJob),
            (mediator, serverId) => mediator.Send(new RecalculateMarketPricesCommand(serverId)));
    }
}
