using EmpireIdle.Application.Mail.Commands;
using Hangfire;

namespace EmpireIdle.API.Jobs
{
    /// <summary>Раз на добу прибирає протерміновані листи й оголошення кожного світу.</summary>
    public class MailRetentionJob
    {
        private readonly ServerJobRunner _runner;

        public MailRetentionJob(ServerJobRunner runner) => _runner = runner;

        [DisableConcurrentExecution(timeoutInSeconds: JobDefaults.LockWaitSeconds)]
        public Task RunAsync(CancellationToken cancellationToken) => _runner.ForEachServerAsync(
            nameof(MailRetentionJob),
            (mediator, _, ct) => mediator.Send(new DeleteExpiredMailCommand(), ct), cancellationToken);
    }
}
