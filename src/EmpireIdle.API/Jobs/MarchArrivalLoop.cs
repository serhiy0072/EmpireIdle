using EmpireIdle.Application.Marches.Commands;
using EmpireIdle.Application.Marches.Queries;

namespace EmpireIdle.API.Jobs
{
    /// <summary>
    /// Прибуття походів — окремим частим циклом, а не хвилинним timer-scan: розвідники доходять
    /// за секунди, і звіт має з'явитися, щойно вони дійшли, а не до хвилини потому. Те саме
    /// дістається й бою — він стається в момент прибуття.
    ///
    /// Не Hangfire: найменший крок його розкладу разом із опитуванням черги — десятки секунд.
    /// Два інстанси, що підхопили той самий похід, не зламають його: похід має токен паралелізму,
    /// і другий зберігач отримає конфлікт лише на цьому елементі.
    /// </summary>
    public sealed class MarchArrivalLoop : BackgroundService
    {
        private static readonly TimeSpan Period = TimeSpan.FromSeconds(2);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MarchArrivalLoop> _logger;

        public MarchArrivalLoop(IServiceScopeFactory scopeFactory, ILogger<MarchArrivalLoop> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Period);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var runner = scope.ServiceProvider.GetRequiredService<ServerJobRunner>();

                    // Кожен похід — у власному scope, тим самим шляхом, що й інші таймери.
                    // Упале прибуття відкладається, а не повторюється кожен тік
                    await runner.ForEachItemAsync(nameof(CompleteMarchCommand), (mediator, ct) => mediator.Send(new GetDueMarchIdsQuery(), ct),
                        (mediator, id, ct) => mediator.Send(new CompleteMarchCommand(id), ct), stoppingToken,
                        onFailure: (mediator, id, ct) => mediator.Send(new DeferMarchArrivalCommand(id), ct));
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    // Наступний тік підбере пропущене
                    _logger.LogError(ex, "March arrival tick failed.");
                }
            }
        }
    }
}
