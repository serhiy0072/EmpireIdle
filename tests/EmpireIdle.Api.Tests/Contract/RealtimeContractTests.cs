using System.Reflection;
using System.Text;
using System.Text.Json;
using EmpireIdle.API.Hubs;

namespace EmpireIdle.Api.Tests.Contract;

/// <summary>
/// Контракт подій SignalR для клієнта. З OpenAPI він не видимий — це не HTTP,
/// тож джерелом для фронтенду є закомічений realtime/events.json.
///
/// Перегенерувати: UPDATE_REALTIME=1 dotnet test --filter RealtimeContractTests
/// </summary>
public class RealtimeContractTests
{
    private const string ContractPath = "realtime/events.json";

    [Fact]
    public async Task Events_ShouldMatchTheCommittedContract()
    {
        var live = Serialize(Contract());
        var file = Path.Combine(RepositoryRoot(), ContractPath);

        if (Environment.GetEnvironmentVariable("UPDATE_REALTIME") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            await File.WriteAllTextAsync(file, live, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return;
        }

        Assert.True(File.Exists(file), $"{ContractPath} is missing. Run the test with UPDATE_REALTIME=1 to create it.");

        Assert.Equal(Normalize(await File.ReadAllTextAsync(file)), Normalize(live));
    }

    /// <summary>
    /// Кожна подія — рівно один payload-параметр і Task на виході.
    /// Типізований проксі SignalR інакше падає вже в рантаймі, при першій відправці.
    /// </summary>
    [Fact]
    public void Events_ShouldBeShapedForTheTypedProxy()
    {
        var broken = typeof(IGameClient).GetMethods()
            .Where(method => method.ReturnType != typeof(Task) || method.GetParameters().Length != 1)
            .Select(method => method.Name)
            .ToList();

        Assert.Empty(broken);
    }

    /// <summary>Імена — camelCase: саме так їх серіалізує протокол SignalR.</summary>
    private static SortedDictionary<string, string[]> Contract()
    {
        var contract = new SortedDictionary<string, string[]>(StringComparer.Ordinal);

        foreach (var method in typeof(IGameClient).GetMethods())
        {
            var payload = method.GetParameters().Single().ParameterType;

            contract[method.Name] = payload
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => JsonNamingPolicy.CamelCase.ConvertName(property.Name))
                .Order(StringComparer.Ordinal)
                .ToArray();
        }

        return contract;
    }

    private static string Serialize(object contract)
        => JsonSerializer.Serialize(contract, new JsonSerializerOptions { WriteIndented = true });

    private static string Normalize(string json)
        => Serialize(JsonSerializer.Deserialize<JsonElement>(json));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EmpireIdle.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
