using EmpireIdle.Domain.Services;

namespace EmpireIdle.API.Services
{
    /// <summary>
    /// Будує GameCatalog на старті, а з ним — міжсекційну валідацію конфіга (GameConfigValidator).
    ///
    /// Без цього каталог створювала б лінива фабрика на першому запиті: застосунок із битим
    /// конфігом стартував би, проходив health-check і на кожен запит віддавав 500. Хостед-сервіси
    /// стартують після ValidateOnStart, тож межі окремих полів перевіряються першими.
    /// </summary>
    public sealed class GameCatalogStartupCheck : IHostedService
    {
        private readonly IServiceProvider _services;

        public GameCatalogStartupCheck(IServiceProvider services) => _services = services;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _services.GetRequiredService<GameCatalog>();

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
