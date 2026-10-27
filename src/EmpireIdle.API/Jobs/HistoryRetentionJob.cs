using EmpireIdle.Application.Common.Commands;
using Hangfire;

namespace EmpireIdle.API.Jobs
{
    /// <summary>
    /// Раз на добу прибирає стару історію — звіти, завершені забіги, закриті лоти, вирішені заявки.
    /// Раз на всі світи: строки однакові, і прогін на кожен світ робив би ту саму роботу N разів.
    /// </summary>
    public class HistoryRetentionJob
    {
        private readonly ServerJobRunner _runner;

        public HistoryRetentionJob(ServerJobRunner runner) => _runner = runner;

        [DisableConcurrentExecution(timeoutInSeconds: JobDefaults.LockWaitSeconds)]
        public Task RunAsync(CancellationToken cancellationToken) => _runner.RunOnceAsync(
            nameof(HistoryRetentionJob),
            (mediator, ct) => mediator.Send(new PurgeStaleHistoryCommand(), ct), cancellationToken);
    }
}
