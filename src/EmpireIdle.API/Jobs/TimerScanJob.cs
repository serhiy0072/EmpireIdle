using EmpireIdle.Application.Garrisons.Commands;
using EmpireIdle.Application.Marches.Commands;
using EmpireIdle.Application.Villages.Commands;
using MediatR;

namespace EmpireIdle.API.Jobs
{
    public class TimerScanJob
    {
        private readonly IMediator _mediator;
        public TimerScanJob(IMediator mediator) => _mediator = mediator;

        public async Task RunAsync()
        {
            await _mediator.Send(new CompleteDueTimersCommand());
            await _mediator.Send(new CompleteDueMarchesCommand());
            await _mediator.Send(new PurgeExpiredRecoverableCommand());
        }
    }
}
