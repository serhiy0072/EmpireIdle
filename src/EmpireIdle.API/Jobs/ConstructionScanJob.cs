using EmpireIdle.Application.Villages.Commands;
using MediatR;

namespace EmpireIdle.API.Jobs
{
    public class ConstructionScanJob
    {
        private readonly IMediator _mediator;
        public ConstructionScanJob(IMediator mediator) => _mediator = mediator;

        public Task RunAsync() => _mediator.Send(new CompleteConstructionsCommand());

    }
}
