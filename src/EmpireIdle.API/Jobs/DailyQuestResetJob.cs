using EmpireIdle.Application.Quests.Commands;
using Hangfire;

namespace EmpireIdle.API.Jobs
{
    /// <summary>Скидає дейліки о 00:00 UTC — по кожному активному світу.</summary>
    public class DailyQuestResetJob
    {
        private readonly ServerJobRunner _runner;

        public DailyQuestResetJob(ServerJobRunner runner) => _runner = runner;

        [DisableConcurrentExecution(timeoutInSeconds: 600)]
        public Task RunAsync() => _runner.ForEachServerAsync(nameof(DailyQuestResetJob), (mediator, _) =>
            mediator.Send(new ResetDailyQuestsCommand()));
    }
}
