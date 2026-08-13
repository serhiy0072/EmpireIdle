using EmpireIdle.Application.Map.Commands;
using Hangfire;
using MediatR;

namespace EmpireIdle.API.Jobs
{
    /// <summary>Підтримує популяцію монстрів на карті.</summary>
    public class MonsterSpawnJob
    {
        private readonly IMediator _mediator;
        public MonsterSpawnJob(IMediator mediator) => _mediator = mediator;

        /// <summary>Один прогін за раз: перетин дав би подвійне нарахування.</summary>
        [DisableConcurrentExecution(timeoutInSeconds: 300)]
        public Task RunAsync() => _mediator.Send(new SpawnMonstersCommand());
    }
}
