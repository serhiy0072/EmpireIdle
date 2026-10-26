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

    /// <summary>
    /// Імена — camelCase: саме так їх серіалізує протокол SignalR. Поруч — тип у нотації TypeScript
    /// з nullability: зміна int → string чи string → string? інакше пройшла б контракт непоміченою.
    /// </summary>
    private static SortedDictionary<string, SortedDictionary<string, string>> Contract()
    {
        var contract = new SortedDictionary<string, SortedDictionary<string, string>>(StringComparer.Ordinal);
        var nullability = new NullabilityInfoContext();

        foreach (var method in typeof(IGameClient).GetMethods())
        {
            var payload = method.GetParameters().Single().ParameterType;
            var fields = new SortedDictionary<string, string>(StringComparer.Ordinal);

            foreach (var property in payload.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var info = nullability.Create(property);
                fields[JsonNamingPolicy.CamelCase.ConvertName(property.Name)] = TypeName(info);
            }

            contract[method.Name] = fields;
        }

        return contract;
    }

    private static string TypeName(NullabilityInfo info)
    {
        var type = Nullable.GetUnderlyingType(info.Type) ?? info.Type;
        var name = BaseName(type, info);

        return info.ReadState == NullabilityState.Nullable ? $"{name} | null" : name;
    }

    private static string BaseName(Type type, NullabilityInfo info)
    {
        if (type == typeof(string) || type == typeof(Guid) || type == typeof(DateTime) || type == typeof(DateTimeOffset))
            return "string";

        if (type == typeof(bool))
            return "boolean";

        if (type.IsEnum)
            return $"number /* {type.Name} */";

        if (type.IsPrimitive || type == typeof(decimal))
            return "number";

        if (type.IsGenericType && info.GenericTypeArguments.Length == 2 && IsDictionary(type))
            return $"Record<string, {TypeName(info.GenericTypeArguments[1])}>";

        if (info.ElementType is { } element)
            return $"{TypeName(element)}[]";

        if (type.IsGenericType && info.GenericTypeArguments.Length == 1)
            return $"{TypeName(info.GenericTypeArguments[0])}[]";

        return type.Name;
    }

    private static bool IsDictionary(Type type)
        => type.GetInterfaces().Append(type).Any(i => i.IsGenericType
            && (i.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>) || i.GetGenericTypeDefinition() == typeof(IDictionary<,>)));

    // Без екранування < і >: файл читає людина, що звіряє його з events.ts
    private static string Serialize(object contract)
        => JsonSerializer.Serialize(contract, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

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
