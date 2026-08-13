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
        private readonly IMediator _mediator;

        public ResourceTickJob(IMediator mediator)
        {
            _mediator = mediator;
        }

        // <summary>Один прогін за раз: перетин дав би подвійне нарахування.</summary>
        [DisableConcurrentExecution(timeoutInSeconds: 300)]
        public Task RunAsync() => _mediator.Send(new TickAllVillagesCommand());
    }
}
