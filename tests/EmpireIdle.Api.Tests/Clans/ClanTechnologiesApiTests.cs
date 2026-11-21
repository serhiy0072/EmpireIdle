using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EmpireIdle.Api.Tests.Infrastructure;
using EmpireIdle.API.DTOs;
using EmpireIdle.Application.Clans.ReadModels;
using EmpireIdle.Application.Clans.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EmpireIdle.Api.Tests.Clans;

/// <summary>Кланові технології наскрізь: HTTP, відмови за контрактом, справжня база й завантажений конфіг гри.</summary>
[Collection("postgres")]
public class ClanTechnologiesApiTests : IAsyncLifetime
{
    private const int ServerId = 1;
    private const string FreeTeleport = "teleport_random";
    private const string ClanTeleport = "teleport_clan";

    private readonly PostgresFixture _postgres;
    private TestApiFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _playerId;

    public ClanTechnologiesApiTests(PostgresFixture postgres) => _postgres = postgres;

    public async Task InitializeAsync()
    {
        _factory = new TestApiFactory(_postgres.ConnectionString);
        await _factory.MigrateAsync();

        _client = _factory.CreateClient();

        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            UserName: $"ct{Guid.NewGuid():N}"[..12],
            Email: $"ct-{Guid.NewGuid():N}@test.local",
            Password: "Password123"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        _playerId = auth.PlayerId;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private string Url(string path = "") => $"/api/clan-technologies/{_playerId}{path}";

    private Task<HttpResponseMessage> PostAsync<T>(string path, T body)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, Url(path)) { Content = JsonContent.Create(body) };
        message.Headers.Add("Idempotency-Key", $"ct-{Guid.NewGuid():N}");

        return _client.SendAsync(message);
    }

    private static async Task<string?> ReasonAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return body.RootElement.TryGetProperty("reason", out var reason) ? reason.GetString() : null;
    }

    private async Task<ClanTechnologiesView> GetViewAsync()
        => (await _client.GetFromJsonAsync<ClanTechnologiesView>(Url()))!;

    /// <summary>Робить гравця лідером нового клану: шлях створення через будівлю тут не перевіряється.</summary>
    private async Task JoinClanAsync()
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IServerContext>().UseServer(ServerId);
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var suffix = Guid.NewGuid().ToString("N")[..4];
        context.Clans.Add(new Clan(Guid.NewGuid(), ServerId, $"Test {suffix}", suffix, _playerId, DateTime.UtcNow));

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Get_ShouldBeEmpty_WithoutAClan()
    {
        var response = await _client.GetAsync(Url());

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains(await response.Content.ReadAsStringAsync(), new[] { "", "null" });
    }

    [Fact]
    public async Task Get_ShouldForbidAnotherPlayersState()
    {
        var response = await _client.GetAsync($"/api/clan-technologies/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Безкоштовна вітрина працює й без клану, ліміт видно на наступному запиті, а понад нього — відмова.</summary>
    [Fact]
    public async Task ClaimFree_ShouldCountAgainstTheWeeklyLimit_WithoutAClan()
    {
        var before = (await _client.GetFromJsonAsync<ClanShopView>(Url("/shop")))!;
        Assert.False(before.InClan);
        var limit = before.Free.Items.Single(i => i.ItemKey == FreeTeleport).Limit;

        var claimed = await PostAsync("/shop/claim-free", new ClaimClanFreeItemRequest(FreeTeleport, 1));
        Assert.Equal(HttpStatusCode.NoContent, claimed.StatusCode);

        var after = (await _client.GetFromJsonAsync<ClanShopView>(Url("/shop")))!;
        Assert.Equal(1, after.Free.Items.Single(i => i.ItemKey == FreeTeleport).Claimed);

        var overLimit = await PostAsync("/shop/claim-free", new ClaimClanFreeItemRequest(FreeTeleport, limit));
        Assert.Equal(HttpStatusCode.BadRequest, overLimit.StatusCode);
        Assert.Equal(RefusalReasons.ClanTechShopLimit.Key, await ReasonAsync(overLimit));
    }

    [Fact]
    public async Task ClaimFree_ShouldRefuseAClanTeleport_WithoutAClan()
    {
        var response = await PostAsync("/shop/claim-free", new ClaimClanFreeItemRequest(ClanTeleport, 1));

        Assert.False(response.IsSuccessStatusCode);
        Assert.Equal(RefusalReasons.ClanNotMember.Key, await ReasonAsync(response));
    }

    /// <summary>Повний цикл: екран показує ресурс і ціну вузла, підношення списує саме їх і рухає цей вузол.</summary>
    [Fact]
    public async Task OfferResources_ShouldMoveTheChosenNodeForward()
    {
        await JoinClanAsync();

        var fresh = await GetViewAsync();
        Assert.Equal(fresh.Offering.MaxAttempts, fresh.Offering.Attempts);
        Assert.Empty(fresh.Contributors);

        var node = fresh.Nodes.First(n => n.Tier == 1 && n.Availability == "Available");
        Assert.NotNull(node.Resource);
        Assert.True(node.AttemptCost > 0);
        await GrantResourceAsync(node.Resource!, node.AttemptCost!.Value);

        var offered = await PostAsync("/offer-resources", new OfferClanResourcesRequest(1, node.Node));
        Assert.Equal(HttpStatusCode.OK, offered.StatusCode);

        var result = (await offered.Content.ReadFromJsonAsync<ClanOfferingResult>())!;
        Assert.True(result.Coins > 0);
        Assert.Equal(fresh.Offering.PointsPerAttempt, result.Progress);

        var after = await GetViewAsync();
        Assert.Equal(result.TotalCoins, after.Coins);
        Assert.Equal(fresh.Offering.MaxAttempts - 1, after.Offering.Attempts);
        Assert.NotNull(after.Offering.NextAttemptAt);
        Assert.Equal(result.Progress, after.Nodes.Single(n => n.Tier == 1 && n.Node == node.Node).Progress);

        var me = Assert.Single(after.Contributors);
        Assert.Equal(_playerId, me.PlayerId);
        Assert.Equal(fresh.Offering.PointsPerAttempt, me.Today);
    }

    /// <summary>У закритий вузол підносити не можна: відмова за контрактом, а не списання.</summary>
    [Fact]
    public async Task OfferResources_ShouldRefuse_ANodeWhosePrerequisitesAreMissing()
    {
        await JoinClanAsync();

        var view = await GetViewAsync();
        var closed = view.Nodes.First(n => n.Tier == 1 && n.Availability == "PrerequisitesMissing");

        var response = await PostAsync("/offer-resources", new OfferClanResourcesRequest(1, closed.Node));

        Assert.False(response.IsSuccessStatusCode);
        Assert.Equal(RefusalReasons.ResearchPrerequisites.Key, await ReasonAsync(response));
    }

    /// <summary>Лідер ставить мітку, екран її показує, а внесок у позначений вузол приходить із надбавкою.</summary>
    [Fact]
    public async Task Mark_ShouldShowOnTheScreen_AndBoostTheOffering()
    {
        await JoinClanAsync();

        var node = (await GetViewAsync()).Nodes.First(n => n.Tier == 1 && n.Availability == "Available");
        await GrantResourceAsync(node.Resource!, node.AttemptCost!.Value);

        var marked = await PostAsync("/mark", new MarkClanTechnologyRequest(1, node.Node));
        Assert.Equal(HttpStatusCode.NoContent, marked.StatusCode);
        Assert.Equal($"1:{node.Node}", (await GetViewAsync()).MarkedTechnology);

        var offered = await PostAsync("/offer-resources", new OfferClanResourcesRequest(1, node.Node));
        Assert.True((await offered.Content.ReadFromJsonAsync<ClanOfferingResult>())!.Marked);

        var cleared = await PostAsync("/mark", new MarkClanTechnologyRequest(null, null));
        Assert.Equal(HttpStatusCode.NoContent, cleared.StatusCode);
        Assert.Null((await GetViewAsync()).MarkedTechnology);
    }

    [Fact]
    public async Task Mark_ShouldRefuse_ATierWithoutANode()
    {
        await JoinClanAsync();

        var response = await PostAsync("/mark", new MarkClanTechnologyRequest(1, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OfferResources_ShouldNotFind_AnUnknownTechnology()
    {
        await JoinClanAsync();

        var response = await PostAsync("/offer-resources", new OfferClanResourcesRequest(1, "nope"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task GrantResourceAsync(string resource, int amount)
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IServerContext>().UseServer(ServerId);
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var village = await context.Villages.IgnoreQueryFilters().Include(v => v.Resources).FirstAsync(v => v.PlayerId == _playerId);
        village.GrantResource(resource, amount, DateTime.UtcNow);

        await context.SaveChangesAsync();
    }
}
