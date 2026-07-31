using EmpireIdle.Application.Villages.Commands;
using MediatR;

namespace EmpireIdle.API.Jobs
{
    public class TimerScanJob
    {
        private readonly IMediator _mediator;
        public TimerScanJob(IMediator mediator) => _mediator = mediator;

        public Task RunAsync() => _mediator.Send(new CompleteDueTimersCommand());

    }
}
