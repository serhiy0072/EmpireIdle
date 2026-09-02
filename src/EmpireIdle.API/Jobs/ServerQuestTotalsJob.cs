using EmpireIdle.Application.ServerQuests.Commands;
using Hangfire;

namespace EmpireIdle.API.Jobs
{
    /// <summary>
    /// Збирає внески серверних квестів у спільний підсумок.
    /// Щохвилини: лічильник на весь світ не потребує секундної точності,
    /// а частіший прогін ганяв би SUM по всій таблиці внесків.
    /// </summary>
    public class ServerQuestTotalsJob
    {
        private readonly ServerJobRunner _runner;

        public ServerQuestTotalsJob(ServerJobRunner runner) => _runner = runner;

        [DisableConcurrentExecution(timeoutInSeconds: 300)]
        public Task RunAsync() => _runner.ForEachServerAsync(
            nameof(ServerQuestTotalsJob),
            (mediator, serverId) => mediator.Send(new UpdateServerQuestTotalsCommand()));
    }
}
