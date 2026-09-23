using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EmpireIdle.Domain.Exceptions;

namespace EmpireIdle.Api.Tests.Contract;

/// <summary>
/// Контракт причин відмов для клієнта. У ProblemDetails причина — довільний
/// рядок, тож OpenAPI її не бачить; джерелом для фронтенду є закомічений
/// refusals/reasons.json. Клієнт виводить із нього тип ключів і не збирається,
/// доки кожен ключ не має тексту.
///
/// Перегенерувати: UPDATE_REFUSALS=1 dotnet test --filter RefusalContractTests
/// </summary>
public class RefusalContractTests
{
    private const string ContractPath = "refusals/reasons.json";

    [Fact]
    public async Task Reasons_ShouldMatchTheCommittedContract()
    {
        var live = Serialize(Contract());
        var file = Path.Combine(RepositoryRoot(), ContractPath);

        if (Environment.GetEnvironmentVariable("UPDATE_REFUSALS") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            await File.WriteAllTextAsync(file, live, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return;
        }

        Assert.True(File.Exists(file), $"{ContractPath} is missing. Run the test with UPDATE_REFUSALS=1 to create it.");

        Assert.Equal(Normalize(await File.ReadAllTextAsync(file)), Normalize(live));
    }

    /// <summary>Два поля з тим самим ключем — клієнт показав би одній відмові текст іншої.</summary>
    [Fact]
    public void Reasons_ShouldHaveUniqueKeys()
    {
        var duplicates = Reasons()
            .GroupBy(reason => reason.Key)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        Assert.Empty(duplicates);
    }

    /// <summary>Ключ «модуль.суть», параметри camelCase — так їх пише JSON і читає клієнт.</summary>
    [Fact]
    public void Reasons_ShouldBeWellFormed()
    {
        var key = new Regex("^[a-z]+(\\.[a-z][a-zA-Z]*)+$");
        var arg = new Regex("^[a-z][a-zA-Z]*$");

        var broken = Reasons()
            .Where(reason => !key.IsMatch(reason.Key) || reason.ArgNames.Any(name => !arg.IsMatch(name)))
            .Select(reason => reason.Key)
            .ToList();

        Assert.Empty(broken);
    }

    private static IEnumerable<RefusalReason> Reasons()
        => typeof(RefusalReasons)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(RefusalReason))
            .Select(field => (RefusalReason)field.GetValue(null)!);

    private static SortedDictionary<string, string[]> Contract()
    {
        var contract = new SortedDictionary<string, string[]>(StringComparer.Ordinal);

        foreach (var reason in Reasons())
            contract[reason.Key] = [.. reason.ArgNames];

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
