using EmpireIdle.Application.Beasts.Commands;
using EmpireIdle.Application.Beasts.Queries;
using Hangfire;

namespace EmpireIdle.API.Jobs
{
    /// <summary>
    /// Годує звіринці (рішення 13.10.2026): кожні 10 хвилин списує їжу за час від минулого годування,
    /// а село, якому не вистачило, голодує до наступного вдалого годування.
    /// </summary>
    public class FoodUpkeepJob
    {
        private readonly ServerJobRunner _runner;

        public FoodUpkeepJob(ServerJobRunner runner) => _runner = runner;

        [DisableConcurrentExecution(timeoutInSeconds: JobDefaults.LockWaitSeconds)]
        public Task RunAsync(CancellationToken cancellationToken) => _runner.ForEachItemAsync(
            nameof(PayFoodUpkeepCommand),
            (mediator, ct) => mediator.Send(new GetVillageIdsDueFoodUpkeepQuery(), ct),
            (mediator, villageId, ct) => mediator.Send(new PayFoodUpkeepCommand(villageId), ct), cancellationToken);
    }
}
