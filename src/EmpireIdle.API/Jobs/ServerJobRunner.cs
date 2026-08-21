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

        /// <summary>
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
                    await InScopeAsync(serverId, mediator => action(mediator, serverId));
                }
                catch (Exception ex)
                {
                    // Один світ не зупиняє решту — наступний тік підбере пропущене
                    _logger.LogError(ex, "{Job} failed for server {ServerId}; continuing.", jobName, serverId);
                }
            }
        }

        /// <summary>
        /// Читає перелік у своєму scope, далі обробляє кожен елемент у власному.
        /// Конфлікт паралелізму на одному елементі коштує лише його — решта проходить,
        /// на відміну від партії з одним SaveChanges.
        /// Помилки логуються й прогін не зупиняють.
        /// </summary>
        /// <param name="jobName">Ім'я джоба для логів.</param>
        /// <param name="load">Повертає ідентифікатори до обробки. Не сутності: завантажене в одному scope не зберегти в іншому.</param>
        /// <param name="process">Обробка одного елемента у власному scope.</param>
        public async Task ForEachItemAsync<TItem>(
            string jobName,
            Func<IMediator, Task<IReadOnlyList<TItem>>> load,
            Func<IMediator, TItem, Task> process)
        {
            foreach (var serverId in _catalog.Config.ActiveServerIds)
            {
                IReadOnlyList<TItem> items;

                try
                {
                    items = await InScopeAsync(serverId, load);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{Job}: load failed for server {ServerId}; continuing.", jobName, serverId);
                    continue;
                }

                foreach (var item in items)
                {
                    try
                    {
                        await InScopeAsync(serverId, mediator => process(mediator, item));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "{Job}: item {Item} failed on server {ServerId}; continuing.",
                            jobName, item, serverId);
                    }
                }
            }
        }

        private async Task InScopeAsync(int serverId, Func<IMediator, Task> action)
        {
            using var scope = _scopeFactory.CreateScope();
            scope.ServiceProvider.GetRequiredService<IServerContext>().UseServer(serverId);

            await action(scope.ServiceProvider.GetRequiredService<IMediator>());
        }

        private async Task<TResult> InScopeAsync<TResult>(int serverId, Func<IMediator, Task<TResult>> action)
        {
            using var scope = _scopeFactory.CreateScope();
            scope.ServiceProvider.GetRequiredService<IServerContext>().UseServer(serverId);

            return await action(scope.ServiceProvider.GetRequiredService<IMediator>());
        }
    }
}
