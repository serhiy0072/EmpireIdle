using EmpireIdle.Application.Rating.Commands;
using Hangfire;

namespace EmpireIdle.API.Jobs
{
    /// <summary>
    /// Щогодини перераховує рейтинг усіх гравців світу.
    ///
    /// Джоб, а не подія: рейтинг читається в лідерборді, а не в бою, і година
    /// затримки непомітна. Натомість Power не мусить оголошувати про себе,
    /// а рейтинг сам ходить по дані — зв'язності немає жодної.
    /// </summary>
    public class RatingRecalculationJob
    {
        private readonly ServerJobRunner _runner;

        public RatingRecalculationJob(ServerJobRunner runner) => _runner = runner;

        [DisableConcurrentExecution(timeoutInSeconds: JobDefaults.LockWaitSeconds)]
        public Task RunAsync(CancellationToken cancellationToken) => _runner.ForEachServerAsync(
            nameof(RatingRecalculationJob),
            (mediator, serverId) => mediator.Send(new RecalculateAllRatingsCommand()), cancellationToken);
    }
}
