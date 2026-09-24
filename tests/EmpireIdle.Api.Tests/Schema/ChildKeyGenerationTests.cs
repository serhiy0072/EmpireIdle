using EmpireIdle.Api.Tests.Infrastructure;
using EmpireIdle.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace EmpireIdle.Api.Tests.Schema;

/// <summary>
/// Дочірній рядок, доданий у колекцію вже завантаженого агрегату, EF
/// розпізнає як новий лише тоді, коли його ключ не генерується. Інакше
/// заповнений доменом Guid читається як «рядок уже є» — і замість INSERT
/// іде UPDATE на нуль рядків. Так ламався вступ у клан.
/// </summary>
[Collection("postgres")]
public class ChildKeyGenerationTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private TestApiFactory _factory = null!;

    public ChildKeyGenerationTests(PostgresFixture postgres) => _postgres = postgres;

    public Task InitializeAsync()
    {
        _factory = new TestApiFactory(_postgres.ConnectionString);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    [Fact]
    public void CollectionChildren_ShouldNotGenerateGuidKeys()
    {
        using var scope = _factory.Services.CreateScope();
        var model = scope.ServiceProvider.GetRequiredService<AppDbContext>().Model;

        var generated = model.GetEntityTypes()
            .SelectMany(entity => entity.GetNavigations())
            .Where(navigation => navigation.IsCollection)
            .Select(navigation => navigation.TargetEntityType)
            .Distinct()
            .Select(child => child.FindPrimaryKey()!.Properties)
            .Where(key => key.Count == 1 && key[0].ClrType == typeof(Guid) && key[0].ValueGenerated != ValueGenerated.Never)
            .Select(key => key[0].DeclaringType.DisplayName())
            .Order()
            .ToList();

        Assert.Empty(generated);
    }
}
