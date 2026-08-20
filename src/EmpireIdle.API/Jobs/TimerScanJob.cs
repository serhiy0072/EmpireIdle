using EmpireIdle.Application.Garrisons.Commands;
using EmpireIdle.Application.Marches.Commands;
using EmpireIdle.Application.Villages.Commands;
using Hangfire;

namespace EmpireIdle.API.Jobs
{
    public class TimerScanJob
    {
        private readonly ServerJobRunner _runner;
        public TimerScanJob(ServerJobRunner runner) => _runner = runner;

        [DisableConcurrentExecution(timeoutInSeconds: 300)]
        public Task RunAsync() => _runner.ForEachServerAsync(nameof(DailyQuestResetJob), async (mediator, _) =>
        {
            await mediator.Send(new CompleteDueTimersCommand());
            await mediator.Send(new CompleteDueMarchesCommand());
            await mediator.Send(new PurgeExpiredRecoverableCommand());
        });
    }
}
