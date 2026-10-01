using EmpireIdle.Application.Quests.Commands;
using EmpireIdle.Application.Quests.Queries;
using Hangfire;

namespace EmpireIdle.API.Jobs
{
    /// <summary>Скидає дейліки о 00:00 UTC — по кожному активному світу.</summary>
    public class DailyQuestResetJob
    {
        private readonly ServerJobRunner _runner;

        public DailyQuestResetJob(ServerJobRunner runner) => _runner = runner;

        [DisableConcurrentExecution(timeoutInSeconds: JobDefaults.LockWaitSeconds)]
        public Task RunAsync(CancellationToken cancellationToken) => _runner.ForEachItemAsync(nameof(ResetPlayerDailyQuestsCommand),
            (mediator, ct) => mediator.Send(new GetPlayerIdsWithStaleDailyQuestsQuery(), ct),
            (mediator, playerId, ct) => mediator.Send(new ResetPlayerDailyQuestsCommand(playerId), ct), cancellationToken);
    }
}
