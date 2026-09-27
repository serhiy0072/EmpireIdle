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
            await _runner.ForEachItemAsync(nameof(CompleteVillageConstructionsCommand), mediator => mediator.Send(new GetVillageIdsWithDueConstructionsQuery()),
                (mediator, id) => mediator.Send(new CompleteVillageConstructionsCommand(id)), cancellationToken);

            await _runner.ForEachItemAsync(nameof(CompleteGarrisonTrainingCommand), mediator => mediator.Send(new GetGarrisonIdsWithDueTrainingQuery()),
                (mediator, id) => mediator.Send(new CompleteGarrisonTrainingCommand(id)), cancellationToken);

            await _runner.ForEachItemAsync(nameof(CompleteGarrisonLevelUpsCommand), mediator => mediator.Send(new GetGarrisonIdsWithDueLevelUpsQuery()),
                (mediator, id) => mediator.Send(new CompleteGarrisonLevelUpsCommand(id)), cancellationToken);

            await _runner.ForEachItemAsync(nameof(CompleteHeroLevelUpCommand), mediator => mediator.Send(new GetHeroOrderIdsWithDueLevelUpQuery()),
                (mediator, id) => mediator.Send(new CompleteHeroLevelUpCommand(id)), cancellationToken);

            await _runner.ForEachServerAsync(nameof(RemoveExpiredEffectsCommand), (mediator, _) => mediator.Send(new RemoveExpiredEffectsCommand()), cancellationToken);

            await _runner.ForEachServerAsync(nameof(RemoveExpiredClanHelpCommand), (mediator, _) => mediator.Send(new RemoveExpiredClanHelpCommand()), cancellationToken);

            // Кожен похід — у власному scope, тим самим шляхом, що й інші таймери
            await _runner.ForEachItemAsync(nameof(CompleteMarchCommand), mediator => mediator.Send(new GetDueMarchIdsQuery()),
                (mediator, id) => mediator.Send(new CompleteMarchCommand(id)), cancellationToken);

            await _runner.ForEachServerAsync(nameof(PurgeExpiredRecoverableCommand), (mediator, _) => mediator.Send(new PurgeExpiredRecoverableCommand()), cancellationToken);
        }
    }
}
