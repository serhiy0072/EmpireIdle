using EmpireIdle.Application.Clans.Commands;
using EmpireIdle.Application.Clans.Queries;
using Hangfire;
using MediatR;

namespace EmpireIdle.API.Jobs
{
    /// <summary>
    /// Знімає лідерів, які зникли надовше за LeaderInactivityDays.
    /// Раз на добу: правило рахує дні, частіше нема сенсу.
    /// </summary>
    public class ClanLeadershipJob
    {
        private readonly ServerJobRunner _runner;

        public ClanLeadershipJob(ServerJobRunner runner) => _runner = runner;

        [DisableConcurrentExecution(timeoutInSeconds: JobDefaults.LockWaitSeconds)]
        public Task RunAsync(CancellationToken cancellationToken) => _runner.ForEachItemAsync(
            nameof(TransferInactiveLeadershipCommand),
            (mediator, ct) => mediator.Send(new GetClansWithInactiveLeaderQuery(), ct),
            (mediator, clanId, ct) => mediator.Send(new TransferInactiveLeadershipCommand(clanId), ct), cancellationToken);
    }
}
