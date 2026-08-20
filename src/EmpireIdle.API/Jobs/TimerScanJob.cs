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
        public async Task RunAsync()
        {
            await _runner.ForEachServerAsync(nameof(CompleteDueTimersCommand), (mediator, _) =>
                mediator.Send(new CompleteDueTimersCommand()));

            await _runner.ForEachServerAsync(nameof(CompleteDueMarchesCommand), (mediator, _) =>
                mediator.Send(new CompleteDueMarchesCommand()));

            await _runner.ForEachServerAsync(nameof(PurgeExpiredRecoverableCommand), (mediator, _) =>
                mediator.Send(new PurgeExpiredRecoverableCommand()));
        }
    }
}
