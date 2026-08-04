using EmpireIdle.Application.Map.Commands;
using MediatR;

namespace EmpireIdle.API.Jobs
{
    /// <summary>Підтримує популяцію монстрів на карті.</summary>
    public class MonsterSpawnJob
    {
        private readonly IMediator _mediator;
        public MonsterSpawnJob(IMediator mediator) => _mediator = mediator;

        public Task RunAsync() => _mediator.Send(new SpawnMonstersCommand());
    }
}