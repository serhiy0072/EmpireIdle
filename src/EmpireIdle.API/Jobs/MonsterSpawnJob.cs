using EmpireIdle.Application.Map.Commands;
using EmpireIdle.Domain.Services;
using Hangfire;
using MediatR;

namespace EmpireIdle.API.Jobs
{
    /// <summary>Підтримує популяцію монстрів на карті.</summary>
    public class MonsterSpawnJob
    {
        private readonly IMediator _mediator;
        private readonly GameCatalog _catalog;
        private readonly ServerJobRunner _runner;

        public MonsterSpawnJob(IMediator mediator, GameCatalog catalog, ServerJobRunner runner)
        {
            _mediator = mediator;
            _catalog = catalog;
            _runner = runner;
        }

        /// <summary>Один прогін за раз: перетин дав би подвійне нарахування.</summary>
        [DisableConcurrentExecution(timeoutInSeconds: 300)]
        public Task RunAsync() => _runner.ForEachServerAsync(nameof(MonsterSpawnJob), (mediator, serverId) =>
            mediator.Send(new SpawnMonstersCommand(serverId)));
    }
}
