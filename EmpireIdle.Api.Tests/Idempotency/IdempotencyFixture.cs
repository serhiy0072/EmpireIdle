using EmpireIdle.Api.Tests.Infrastructure;
using EmpireIdle.API.DTOs;
using EmpireIdle.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Testcontainers.PostgreSql;

namespace EmpireIdle.Api.Tests.Idempotency;

/// <summary>
/// Контейнер, застосунок і один зареєстрований гравець — на весь клас.
/// TestApiFactory піднімається один раз, а не на кожен тест: інакше
/// одинадцять стартів хоста і одинадцять реєстрацій під лімітером "auth".
/// </summary>
public class IdempotencyFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public TestApiFactory Factory { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;
    public Guid PlayerId { get; private set; }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        Factory = new TestApiFactory(_container.GetConnectionString());
        await Factory.MigrateAsync();

        Client = Factory.CreateClient();

        var response = await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            UserName: $"idem{Guid.NewGuid():N}"[..12],
            Email: $"idem-{Guid.NewGuid():N}@test.local",
            Password: "Password123"));

        // Register повертає Created, не Ok
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "реєстрація має пройти, інакше решта класу безглузда");

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        auth.Should().NotBeNull();

        PlayerId = auth!.PlayerId;
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
        await _container.DisposeAsync();
    }

    /// <summary>Клієнт без токена — для перевірок автентифікації.</summary>
    public HttpClient CreateAnonymousClient() => Factory.CreateClient();
}

public class IdempotencyEndToEndTests : IClassFixture<IdempotencyFixture>
{
    private readonly IdempotencyFixture _fixture;
    private readonly HttpClient _client;
    private readonly Guid _playerId;

    public IdempotencyEndToEndTests(IdempotencyFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
        _playerId = fixture.PlayerId;
    }

    /// <summary>37 символів у наборі [A-Za-z0-9._-] — вкладається в 16–128.</summary>
    private static string NewKey() => $"test-{Guid.NewGuid():N}";

    private HttpRequestMessage Collect(Guid buildingId, string idempotencyKey, Guid? asPlayer = null)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/village/{asPlayer ?? _playerId}/buildings/{buildingId}/collect");

        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return request;
    }

    private async Task<Guid> FirstBuildingIdAsync()
    {
        var village = await _client.GetFromJsonAsync<VillageResponse>($"/api/village/{_playerId}");

        village.Should().NotBeNull();
        village!.Buildings.Should().NotBeEmpty("нове село отримує StartingBuildings із конфіга");

        return village.Buildings[0].Id;
    }

    // ---------- Happy path ----------

    [Fact]
    public async Task First_call_succeeds()
    {
        var buildingId = await FirstBuildingIdAsync();

        var response = await _client.SendAsync(Collect(buildingId, NewKey()));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Second_call_replays_the_first_response()
    {
        var buildingId = await FirstBuildingIdAsync();
        var key = NewKey();

        var first = await _client.SendAsync(Collect(buildingId, key));
        var second = await _client.SendAsync(Collect(buildingId, key));

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "повтор із тим самим ключем віддає збережений результат, а не помилку");
    }

    /// <summary>
    /// РЕГРЕСІЯ B3 — падає на поточному main.
    ///
    /// TryReserveAsync додає record у контекст із фабрики і диспозить його.
    /// Далі behavior робить SetResponse + UnitOfWork.SaveChanges на scoped-контексті,
    /// де цей запис не трекається — ResponseJson лишається null.
    ///
    /// Через HTTP баг невидимий: CollectBuildingCommand це IRequest без результату,
    /// і Replay мовчки віддає Unit. Тому дивимось прямо в рядок БД.
    /// </summary>
    [Fact]
    public async Task Completed_record_stores_its_response()
    {
        var buildingId = await FirstBuildingIdAsync();
        var key = NewKey();

        await _client.SendAsync(Collect(buildingId, key));

        using var scope = _fixture.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var record = await context.IdempotencyRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Key == key);

        record.Should().NotBeNull("резерв лишається після успішної операції");
        record!.RequestType.Should().Be("CollectBuildingCommand");
        record.ResponseJson.Should().NotBeNull(
            "без записаної відповіді ретрай ніколи не відтвориться");
    }

    // ---------- Валідація ключа ----------

    /// <summary>
    /// БАГ B5 — падає на поточному main з 500 замість 400.
    ///
    /// IdempotencyBehavior кидає System.ComponentModel.DataAnnotations.ValidationException,
    /// а GlobalExceptionHandler ловить FluentValidation.ValidationException — різні типи.
    /// Виняток провалюється у гілку `_ => 500`.
    /// </summary>
    [Fact]
    public async Task Missing_header_is_rejected_with_400()
    {
        var buildingId = await FirstBuildingIdAsync();

        var response = await _client.PostAsync(
            $"/api/village/{_playerId}/buildings/{buildingId}/collect", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "відсутній Idempotency-Key це помилка клієнта, а не збій сервера");
    }

    [Theory]
    [InlineData("short")]                              // < 16 символів
    [InlineData("has spaces in the key value")]        // пробіли поза набором
    [InlineData("slash/is/not/allowed/in/the/key")]    // слеш поза набором
    [InlineData("ключ-достатньої-довжини-кирилицею")]  // не ASCII
    public async Task Malformed_key_is_rejected_with_400(string key)
    {
        var buildingId = await FirstBuildingIdAsync();

        var response = await _client.SendAsync(Collect(buildingId, key));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Key_at_minimum_length_is_accepted()
    {
        var buildingId = await FirstBuildingIdAsync();

        var response = await _client.SendAsync(Collect(buildingId, new string('a', 16)));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Same_key_on_a_different_operation_is_rejected()
    {
        var buildingId = await FirstBuildingIdAsync();
        var key = NewKey();

        await _client.SendAsync(Collect(buildingId, key));

        var upgrade = new HttpRequestMessage(
            HttpMethod.Post, $"/api/village/{_playerId}/buildings/{buildingId}/upgrade");
        upgrade.Headers.Add("Idempotency-Key", key);

        var response = await _client.SendAsync(upgrade);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "один ключ не можна перевикористати для іншої команди");
    }

    // ---------- Авторизація ----------

    [Fact]
    public async Task Unauthenticated_call_is_rejected()
    {
        using var anonymous = _fixture.CreateAnonymousClient();

        var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/village/{Guid.NewGuid()}/buildings/{Guid.NewGuid()}/collect");
        request.Headers.Add("Idempotency-Key", NewKey());

        var response = await anonymous.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Collecting_another_players_building_is_forbidden()
    {
        // IDOR: токен наш, PlayerId у шляху чужий.
        // PlayerScopeBehavior кидає UnauthorizedAccessException → 403.
        var buildingId = await FirstBuildingIdAsync();

        var response = await _client.SendAsync(
            Collect(buildingId, NewKey(), asPlayer: Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------- Паралелізм ----------

    [Fact]
    public async Task Parallel_calls_with_the_same_key_create_one_record()
    {
        // Гонку вирішує унікальний індекс (PlayerId, Key), а не перевірка в коді.
        // Обмеження: WebApplicationFactory крутить обидва запити в одному процесі —
        // це доводить, що індекс тримає, і не доводить поведінку під навантаженням.
        var buildingId = await FirstBuildingIdAsync();
        var key = NewKey();

        var responses = await Task.WhenAll(
            _client.SendAsync(Collect(buildingId, key)),
            _client.SendAsync(Collect(buildingId, key)));

        using var scope = _fixture.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var records = await context.IdempotencyRecords
            .AsNoTracking()
            .CountAsync(r => r.Key == key);

        records.Should().Be(1, "два паралельні запити не мають створити два записи");
        responses.Should().Contain(r => r.StatusCode == HttpStatusCode.NoContent);
    }
}
