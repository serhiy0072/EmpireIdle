using EmpireIdle.Api.Tests.Infrastructure;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EmpireIdle.Api.Tests.Concurrency;

/// <summary>
/// Гонки ринку на справжній базі: один лот не купується двічі, один
/// предмет не виставляється двічі. Обидва правила тримає база — підміни
/// в юніт-тестах їх не бачать.
/// </summary>
[Collection("postgres")]
public class MarketConcurrencyTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private TestApiFactory _factory = null!;

    public MarketConcurrencyTests(PostgresFixture postgres) => _postgres = postgres;

    public async Task InitializeAsync()
    {
        _factory = new TestApiFactory(_postgres.ConnectionString);
        await _factory.MigrateAsync();
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    /// <summary>Два покупці прочитали той самий активний лот — другий впирається в xmin.</summary>
    [Fact]
    public async Task Listing_ShouldNotBeBoughtTwice()
    {
        var listingId = await SeedListingAsync(equipmentId: Guid.NewGuid());

        await using var contextA = CreateContext();
        await using var contextB = CreateContext();

        var listingA = await contextA.MarketListings.FirstAsync(l => l.Id == listingId);
        var listingB = await contextB.MarketListings.FirstAsync(l => l.Id == listingId);

        listingA.Buy(Guid.NewGuid(), DateTime.UtcNow);
        listingB.Buy(Guid.NewGuid(), DateTime.UtcNow);

        await contextA.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => contextB.SaveChangesAsync());
    }

    /// <summary>Два активні лоти на той самий меч відкидає частковий унікальний індекс.</summary>
    [Fact]
    public async Task Equipment_ShouldNotHaveTwoActiveListings()
    {
        var swordId = Guid.NewGuid();
        await SeedListingAsync(swordId);

        await Assert.ThrowsAsync<DbUpdateException>(() => SeedListingAsync(swordId));
    }

    /// <summary>Закритий лот індексу не заважає: той самий меч можна виставити знову.</summary>
    [Fact]
    public async Task Equipment_ShouldBeListableAgain_AfterTheListingCloses()
    {
        var swordId = Guid.NewGuid();
        var first = await SeedListingAsync(swordId);

        await using (var context = CreateContext())
        {
            var listing = await context.MarketListings.FirstAsync(l => l.Id == first);
            listing.Cancel(DateTime.UtcNow);
            await context.SaveChangesAsync();
        }

        await SeedListingAsync(swordId);
    }

    private AppDbContext CreateContext()
    {
        var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Фонового HTTP-контексту немає — світ ставимо явно
        scope.ServiceProvider.GetRequiredService<IServerContext>().UseServer(1);

        return context;
    }

    private async Task<Guid> SeedListingAsync(Guid equipmentId)
    {
        await using var context = CreateContext();

        var listing = new MarketListing(Guid.NewGuid(), 1, Guid.NewGuid(), MarketListingKind.Equipment, equipmentId, null,
            "sword_iron", 1, 10, "weapon", 100, 5, DateTime.UtcNow, TimeSpan.FromHours(48));

        context.MarketListings.Add(listing);
        await context.SaveChangesAsync();

        return listing.Id;
    }
}
