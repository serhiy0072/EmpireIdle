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
        private readonly GameCatalog _catalog;

        public ServerJobRunner(IServiceScopeFactory scopeFactory, GameCatalog catalog)
        {
            _scopeFactory = scopeFactory;
            _catalog = catalog;
        }

        public async Task ForEachServerAsync(Func<IMediator, int, Task> action)
        {
            foreach (var serverId in _catalog.Config.ActiveServerIds)
            {
                using var scope = _scopeFactory.CreateScope();
                scope.ServiceProvider.GetRequiredService<IServerContext>().UseServer(serverId);

                await action(scope.ServiceProvider.GetRequiredService<IMediator>(), serverId);
            }
        }
    }
}
