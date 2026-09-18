using System.Text;
using System.Text.Json;
using EmpireIdle.Api.Tests.Infrastructure;

namespace EmpireIdle.Api.Tests.Contract;

/// <summary>
/// Закомічена специфікація збігається з живою.
///
/// З openapi/v1.json генеруються типи фронтенду. Якщо DTO змінили, а файл
/// не перегенерували, фронт читатиме поле, якого вже немає — тож розходження
/// має ловити білд, а не браузер.
///
/// Перегенерувати: UPDATE_OPENAPI=1 dotnet test --filter OpenApiContractTests
/// </summary>
[Collection("postgres")]
public class OpenApiContractTests : IAsyncLifetime
{
    private const string SpecPath = "openapi/v1.json";

    private readonly PostgresFixture _postgres;
    private TestApiFactory _factory = null!;

    public OpenApiContractTests(PostgresFixture postgres) => _postgres = postgres;

    public Task InitializeAsync()
    {
        _factory = new TestApiFactory(_postgres.ConnectionString);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    [Fact]
    public async Task Spec_ShouldMatchTheCommittedFile()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();

        var live = Normalize(await response.Content.ReadAsStringAsync());
        var file = Path.Combine(RepositoryRoot(), SpecPath);

        if (Environment.GetEnvironmentVariable("UPDATE_OPENAPI") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            await File.WriteAllTextAsync(file, live, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return;
        }

        Assert.True(File.Exists(file), $"{SpecPath} is missing. Run the test with UPDATE_OPENAPI=1 to create it.");

        var committed = Normalize(await File.ReadAllTextAsync(file));

        Assert.Equal(committed, live);
    }

    /// <summary>Кожна операція має operationId — інакше генератор назве метод сам і назва попливе.</summary>
    [Fact]
    public async Task Spec_ShouldNameEveryOperation()
    {
        var client = _factory.CreateClient();

        using var document = JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"));

        var nameless = document.RootElement.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject()
                .Where(operation => !operation.Value.TryGetProperty("operationId", out var id)
                    || string.IsNullOrWhiteSpace(id.GetString()))
                .Select(operation => $"{operation.Name.ToUpperInvariant()} {path.Name}"))
            .ToList();

        Assert.Empty(nameless);
    }

    /// <summary>Порядок ключів у Swashbuckle стабільний, різняться лише переноси рядків.</summary>
    private static string Normalize(string json)
        => JsonSerializer.Serialize(
            JsonSerializer.Deserialize<JsonElement>(json),
            new JsonSerializerOptions { WriteIndented = true });

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EmpireIdle.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
