using EmpireIdle.Application.Villages.Commands;
using Hangfire;
using MediatR;

namespace EmpireIdle.API.Jobs
{
    /// <summary>
    /// Обгортка для Hangfire recurring job.
    /// Hangfire резолвить цей клас зі scope і викликає MediatR.
    /// </summary>
    public class ResourceTickJob
    {
        private readonly ServerJobRunner _runner;
        public ResourceTickJob(ServerJobRunner runner) => _runner = runner;

        [DisableConcurrentExecution(timeoutInSeconds: 300)]
        public Task RunAsync() => _runner.ForEachServerAsync((mediator, _) =>
            mediator.Send(new TickAllVillagesCommand()));
    }
}
