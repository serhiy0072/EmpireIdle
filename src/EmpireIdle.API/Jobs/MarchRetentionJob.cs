using EmpireIdle.Application.Marches.Commands;
using Hangfire;

namespace EmpireIdle.API.Jobs
{
    /// <summary>Раз на добу прибирає завершені марші кожного світу, старші за добу.</summary>
    public class MarchRetentionJob
    {
        private readonly ServerJobRunner _runner;

        public MarchRetentionJob(ServerJobRunner runner) => _runner = runner;

        [DisableConcurrentExecution(timeoutInSeconds: JobDefaults.LockWaitSeconds)]
        public Task RunAsync(CancellationToken cancellationToken) => _runner.ForEachServerAsync(
            nameof(MarchRetentionJob),
            (mediator, _, ct) => mediator.Send(new DeleteCompletedMarchesCommand(), ct), cancellationToken);
    }
}
