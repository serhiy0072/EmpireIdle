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

        public MonsterSpawnJob(IMediator mediator, GameCatalog catalog)
        {
            _mediator = mediator;
            _catalog = catalog;
        }

        /// <summary>Один прогін за раз: перетин дав би подвійне нарахування.</summary>
        [DisableConcurrentExecution(timeoutInSeconds: 300)]
        public async Task RunAsync()
        {
            foreach (var serverId in _catalog.Config.ActiveServerIds)
                await _mediator.Send(new SpawnMonstersCommand(serverId));
        }
    }
}
