using EmpireIdle.Application.Clans.Commands;
using EmpireIdle.Application.Effects.Commands;
using EmpireIdle.Application.Garrisons.Commands;
using EmpireIdle.Application.Garrisons.Queries;
using EmpireIdle.Application.Heroes.Commands;
using EmpireIdle.Application.Heroes.Queries;
using EmpireIdle.Application.Marches.Commands;
using EmpireIdle.Application.Marches.Queries;
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
        [DisableConcurrentExecution(timeoutInSeconds: JobDefaults.LockWaitSeconds)]
        public async Task RunAsync(CancellationToken cancellationToken)
        {
            await _runner.ForEachItemAsync(nameof(CompleteVillageConstructionsCommand), (mediator, ct) => mediator.Send(new GetVillageIdsWithDueConstructionsQuery(), ct),
                (mediator, id, ct) => mediator.Send(new CompleteVillageConstructionsCommand(id), ct), cancellationToken);

            await _runner.ForEachItemAsync(nameof(CompleteGarrisonTrainingCommand), (mediator, ct) => mediator.Send(new GetGarrisonIdsWithDueTrainingQuery(), ct),
                (mediator, id, ct) => mediator.Send(new CompleteGarrisonTrainingCommand(id), ct), cancellationToken);

            await _runner.ForEachItemAsync(nameof(CompleteGarrisonLevelUpsCommand), (mediator, ct) => mediator.Send(new GetGarrisonIdsWithDueLevelUpsQuery(), ct),
                (mediator, id, ct) => mediator.Send(new CompleteGarrisonLevelUpsCommand(id), ct), cancellationToken);


            // Ефекти не прив'язані до світу: одне видалення на тік, а не по одному на кожен світ
            await _runner.RunOnceAsync(nameof(RemoveExpiredEffectsCommand), (mediator, ct) => mediator.Send(new RemoveExpiredEffectsCommand(), ct), cancellationToken);

            await _runner.ForEachServerAsync(nameof(RemoveExpiredClanHelpCommand), (mediator, _, ct) => mediator.Send(new RemoveExpiredClanHelpCommand(), ct), cancellationToken);

            // Кожен похід — у власному scope, тим самим шляхом, що й інші таймери
            await _runner.ForEachItemAsync(nameof(CompleteMarchCommand), (mediator, ct) => mediator.Send(new GetDueMarchIdsQuery(), ct),
                (mediator, id, ct) => mediator.Send(new CompleteMarchCommand(id), ct), cancellationToken);

            await _runner.ForEachServerAsync(nameof(PurgeExpiredRecoverableCommand), (mediator, _, ct) => mediator.Send(new PurgeExpiredRecoverableCommand(), ct), cancellationToken);
        }
    }
}
