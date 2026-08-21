using EmpireIdle.Application.Effects.Commands;
using EmpireIdle.Application.Garrisons.Commands;
using EmpireIdle.Application.Garrisons.Queries;
using EmpireIdle.Application.Marches.Commands;
using EmpireIdle.Application.Villages.Commands;
using EmpireIdle.Application.Villages.Queries;
using Hangfire;

namespace EmpireIdle.API.Jobs
{
    public class TimerScanJob
    {
        private readonly ServerJobRunner _runner;
        public TimerScanJob(ServerJobRunner runner) => _runner = runner;

        /// <summary>Один прогін за раз: перетин дав би подвійне завершення таймерів.</summary>
        [DisableConcurrentExecution(timeoutInSeconds: 300)]
        public async Task RunAsync()
        {
            await _runner.ForEachItemAsync(nameof(CompleteVillageConstructionsCommand), mediator => mediator.Send(new GetVillageIdsWithDueConstructionsQuery()),
                (mediator, id) => mediator.Send(new CompleteVillageConstructionsCommand(id)));

            await _runner.ForEachItemAsync(nameof(CompleteGarrisonTrainingCommand), mediator => mediator.Send(new GetGarrisonIdsWithDueTrainingQuery()),
                (mediator, id) => mediator.Send(new CompleteGarrisonTrainingCommand(id)));

            await _runner.ForEachServerAsync(nameof(RemoveExpiredEffectsCommand), (mediator, _) => mediator.Send(new RemoveExpiredEffectsCommand()));

            await _runner.ForEachServerAsync(nameof(CompleteDueMarchesCommand), (mediator, _) => mediator.Send(new CompleteDueMarchesCommand()));

            await _runner.ForEachServerAsync(nameof(PurgeExpiredRecoverableCommand), (mediator, _) => mediator.Send(new PurgeExpiredRecoverableCommand()));
        }
    }
}
