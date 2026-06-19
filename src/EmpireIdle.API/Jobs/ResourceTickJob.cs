using EmpireIdle.Application.Villages.Commands;
using MediatR;
using Microsoft.EntityFrameworkCore.Metadata;

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

        public async Task RunAsync() => await _mediator.Send(new TickAllVillagesCommand());
    }
}
