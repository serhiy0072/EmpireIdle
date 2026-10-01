using EmpireIdle.Application.Servers.Commands;
using Hangfire;

namespace EmpireIdle.API.Jobs
{
    /// <summary>
    /// Раз на добу перевіряє кожен світ на зрілість і заповненість.
    /// Щодня, бо реальні пороги — місяці: частіше не має сенсу,
    /// рідше — розмиває дату переходу.
    /// </summary>
    public class ServerEvolutionJob
    {
        private readonly ServerJobRunner _runner;

        public ServerEvolutionJob(ServerJobRunner runner) => _runner = runner;

        [DisableConcurrentExecution(timeoutInSeconds: JobDefaults.LockWaitSeconds)]
        public Task RunAsync(CancellationToken cancellationToken) => _runner.ForEachServerAsync(
            nameof(ServerEvolutionJob),
            (mediator, serverId, ct) => mediator.Send(new EvolveServerCommand(serverId), ct), cancellationToken);
    }
}
