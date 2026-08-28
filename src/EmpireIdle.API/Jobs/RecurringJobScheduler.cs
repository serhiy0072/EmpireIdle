using Hangfire;

namespace EmpireIdle.API.Jobs
{
    /// <summary>
    /// Реєструє розклад фонових задач при старті.
    ///
    /// Саме IHostedService, а не код у Program: реєстрація бере розподілений лок
    /// у сховищі Hangfire, і в тестах кілька паралельних інстансів застосунку
    /// б'ються за той самий лок. Тести знімають усі IHostedService — так
    /// розклад автоматично потрапляє під ту саму політику.
    ///
    /// Побічний виграш: старт більше не блокується запитом до БД.
    /// </summary>
    public sealed class RecurringJobScheduler : IHostedService
    {
        private readonly IRecurringJobManager _manager;

        public RecurringJobScheduler(IRecurringJobManager manager) => _manager = manager;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _manager.AddOrUpdate<TimerScanJob>("timer-scan", job => job.RunAsync(), Cron.Minutely);
            _manager.AddOrUpdate<MonsterSpawnJob>("monster-spawn", job => job.RunAsync(), "*/5 * * * *");
            _manager.AddOrUpdate<OutboxMaintenanceJob>("outbox-maintenance", job => job.RunAsync(), Cron.Hourly);
            _manager.AddOrUpdate<DailyQuestResetJob>("daily-quest-reset", job => job.RunAsync(), Cron.Daily);
            _manager.AddOrUpdate<ServerEvolutionJob>("server-evolution", job => job.RunAsync(), Cron.Daily);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
