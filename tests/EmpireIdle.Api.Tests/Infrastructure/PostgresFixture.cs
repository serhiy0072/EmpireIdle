using Testcontainers.PostgreSql;

namespace EmpireIdle.Api.Tests.Infrastructure;

/// <summary>
/// Реальний PostgreSQL у контейнері. xmin — системна колонка Postgres,
/// тож перевірити оптимістичне блокування на InMemory неможливо.
///
/// Разом із базою колекція ділить і один хост застосунку з уже накоченою схемою:
/// побудова хоста (читання й валідація всього конфігу гри) на кожен тест
/// коштувала більше, ніж самі тести.
/// </summary>
public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public string ConnectionString => _container.GetConnectionString();

    /// <summary>
    /// Спільний хост колекції, схема вже накочена. Тест, якому потрібні власні налаштування
    /// хоста чи окрема база, будує свій TestApiFactory від <see cref="ConnectionString"/>.
    /// </summary>
    public TestApiFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        Factory = new TestApiFactory(ConnectionString);
        await Factory.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _container.DisposeAsync();
    }
}

[CollectionDefinition("postgres")]
public class PostgresCollection : ICollectionFixture<PostgresFixture>;

/// <summary>
/// Окремий сервер для тестів data-міграцій: вони катають схему вперед-назад у власних базах
/// і спільного хоста не потребують, тож ідуть паралельно з рештою, а не в черзі за нею.
/// </summary>
public class MigrationPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition("postgres-migrations")]
public class MigrationPostgresCollection : ICollectionFixture<MigrationPostgresFixture>;
