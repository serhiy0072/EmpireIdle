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
        /// <param name="action">Дія у контексті світу; отримує <c>IMediator</c> зі свого scope і токен зупинки — його треба передати в Send.</param>
        public async Task ForEachServerAsync(string jobName, Func<IMediator, int, CancellationToken, Task> action,
            CancellationToken cancellationToken = default)
        {
            foreach (var serverId in _catalog.Config.ActiveServerIds)
            {
                // Зупинка сервера (деплой) — виходимо між світами, не посеред обробки
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await InScopeAsync(serverId, mediator => action(mediator, serverId, cancellationToken));
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
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
            Func<IMediator, CancellationToken, Task<IReadOnlyList<TItem>>> load,
            Func<IMediator, TItem, CancellationToken, Task> process,
            CancellationToken cancellationToken = default)
        {
            foreach (var serverId in _catalog.Config.ActiveServerIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                IReadOnlyList<TItem> items;

                try
                {
                    items = await InScopeAsync(serverId, mediator => load(mediator, cancellationToken));
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "{Job}: load failed for server {ServerId}; continuing.", jobName, serverId);
                    continue;
                }

                foreach (var item in items)
                {
                    // Між елементами: кожен уже в своїй транзакції, тож перерваний прогін нічого не лишає навпіл
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        await InScopeAsync(serverId, mediator => process(mediator, item, cancellationToken));
                    }
                    catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogError(ex, "{Job}: item {Item} failed on server {ServerId}; continuing.",
                            jobName, item, serverId);
                    }
                }
            }
        }

        /// <summary>
        /// Виконує дію один раз у scope без світу — для даних, не прив'язаних до світу
        /// (на кшталт активних ефектів). Прогін на кожен світ робив би ту саму роботу N разів.
        /// Помилка логується й не кидається, як і в інших методах раннера.
        /// </summary>
        /// <param name="jobName">Ім'я джоба для логів.</param>
        /// <param name="action">Дія; отримує <c>IMediator</c> зі свого scope і токен зупинки.</param>
        public async Task RunOnceAsync(string jobName, Func<IMediator, CancellationToken, Task> action,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                await action(scope.ServiceProvider.GetRequiredService<IMediator>(), cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "{Job} failed; the next tick retries.", jobName);
            }
        }

        // Асинхронний scope: DbContext і частина сервісів звільняються асинхронно,
        // і синхронний Dispose блокував би потік воркера Hangfire
        private async Task InScopeAsync(int serverId, Func<IMediator, Task> action)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<IServerContext>().UseServer(serverId);

            await action(scope.ServiceProvider.GetRequiredService<IMediator>());
        }

        private async Task<TResult> InScopeAsync<TResult>(int serverId, Func<IMediator, Task<TResult>> action)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<IServerContext>().UseServer(serverId);

            return await action(scope.ServiceProvider.GetRequiredService<IMediator>());
        }
    }
}
