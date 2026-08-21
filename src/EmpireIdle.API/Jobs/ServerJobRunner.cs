using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.API.Jobs
{
    /// <summary>
    /// Виконує дію для кожного активного світу — свій scope, свій DbContext,
    /// свій встановлений сервер. Без цього query-фільтри не мають що застосувати.
    /// </summary>
    public class ServerJobRunner
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ServerJobRunner> _logger;
        private readonly GameCatalog _catalog;

        public ServerJobRunner(IServiceScopeFactory scopeFactory, ILogger<ServerJobRunner> logger, GameCatalog catalog)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _catalog = catalog;
        }

        // <summary>
        /// Виконує дію для кожного активного світу — свій scope, свій DbContext,
        /// свій встановлений сервер.
        /// Помилка одного світу логується й не зупиняє решту, тому метод
        /// завершується успішно навіть тоді, коли жоден світ не обробився.
        /// Пропущене підбирає наступний тік — не покладайся на завершення як на факт.
        /// </summary>
        /// <param name="jobName">Ім'я джоба для логів: через раннер ходять кілька.</param>
        /// <param name="action">Дія у контексті світу; отримує <c>IMediator</c> зі свого scope.</param>
        public async Task ForEachServerAsync(string jobName, Func<IMediator, int, Task> action)
        {
            foreach (var serverId in _catalog.Config.ActiveServerIds)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    scope.ServiceProvider.GetRequiredService<IServerContext>().UseServer(serverId);

                    await action(scope.ServiceProvider.GetRequiredService<IMediator>(), serverId);
                }
                catch (Exception ex)
                {
                    // Один світ не зупиняє решту — наступний тік підбере пропущене
                    _logger.LogError(ex, "{Job} failed for server {ServerId}; continuing.", jobName, serverId);
                }
            }
        }
    }
}
