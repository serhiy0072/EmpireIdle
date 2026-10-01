using EmpireIdle.Application.Map.Commands;
using Hangfire;
using MediatR;

namespace EmpireIdle.API.Jobs
{
    /// <summary>Підтримує популяцію монстрів на карті.</summary>
    public class MonsterSpawnJob
    {
        private readonly ServerJobRunner _runner;

        public MonsterSpawnJob(ServerJobRunner runner)
        {
            _runner = runner;
        }

        /// <summary>Один прогін за раз: перетин дав би подвійне нарахування.</summary>
        [DisableConcurrentExecution(timeoutInSeconds: JobDefaults.LockWaitSeconds)]
        public Task RunAsync(CancellationToken cancellationToken) => _runner.ForEachServerAsync(nameof(MonsterSpawnJob), (mediator, serverId, ct) =>
            mediator.Send(new SpawnMonstersCommand(serverId), ct), cancellationToken);
    }
}
