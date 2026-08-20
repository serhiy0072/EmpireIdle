using Testcontainers.PostgreSql;

namespace EmpireIdle.Api.Tests.Infrastructure;

/// <summary>
/// Реальний PostgreSQL у контейнері. xmin — системна колонка Postgres,
/// тож перевірити оптимістичне блокування на InMemory неможливо.
/// </summary>
public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition("postgres")]
public class PostgresCollection : ICollectionFixture<PostgresFixture>;
