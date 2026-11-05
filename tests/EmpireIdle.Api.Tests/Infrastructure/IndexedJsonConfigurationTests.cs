using System.Text.Json;
using EmpireIdle.API.Configuration;
using EmpireIdle.Domain.Services;
using Microsoft.Extensions.Configuration;

namespace EmpireIdle.Api.Tests.Infrastructure;

/// <summary>Індекс лише пришвидшує пошук дітей вузла: конфіг мусить вийти той самий, що й зі стандартним провайдером.</summary>
public class IndexedJsonConfigurationTests
{
    private static string ContentRoot => AppContext.BaseDirectory;

    private static IEnumerable<string> GameConfigFiles()
        => Directory.GetFiles(Path.Combine(ContentRoot, "Config"), "*.json")
            .Select(file => Path.GetRelativePath(ContentRoot, file))
            .Order(StringComparer.Ordinal);

    private static IConfigurationBuilder NewBuilder() => new ConfigurationBuilder().SetBasePath(ContentRoot);

    private static string Bound(IConfigurationBuilder builder)
        => JsonSerializer.Serialize(builder.Build().GetSection("GameConfig").Get<GameConfig>());

    /// <summary>Головна гарантія заміни: реальні файли гри дають побайтово той самий GameConfig.</summary>
    [Fact]
    public void ShippedConfig_ShouldBindToTheSameGameConfig_AsTheStandardProvider()
    {
        var standard = NewBuilder();
        var indexed = NewBuilder();

        foreach (var file in GameConfigFiles())
        {
            standard.AddJsonFile(file, optional: false, reloadOnChange: false);
            indexed.AddIndexedJsonFile(file);
        }

        var expected = Bound(standard);

        Assert.True(expected.Length > 100_000, "The config files were not found next to the tests.");
        Assert.Equal(expected, Bound(indexed));
    }

    private static string WriteTemp(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);

        return path;
    }

    private static IConfigurationRoot Indexed(params string[] jsons)
    {
        var builder = new ConfigurationBuilder();

        foreach (var json in jsons)
            builder.AddIndexedJsonFile(WriteTemp(json));

        return builder.Build();
    }

    /// <summary>Порядок дітей списку числовий, а не рядковий: «10» після «2». Інакше елементи переставляться.</summary>
    [Fact]
    public void Children_ShouldKeepNumericOrder_OfALongList()
    {
        var items = string.Join(",", Enumerable.Range(0, 12));

        var values = Indexed($$"""{ "List": [ {{items}} ] }""").GetSection("List").Get<List<int>>();

        Assert.Equal(Enumerable.Range(0, 12), values);
    }

    [Fact]
    public void Children_ShouldMergeAcrossFiles_WithTheLaterFileWinning()
    {
        var config = Indexed("""{ "A": { "One": 1, "Two": 2 } }""", """{ "A": { "Two": 22, "Three": 3 } }""");

        Assert.Equal(["One", "Three", "Two"], config.GetSection("A").GetChildren().Select(c => c.Key));
        Assert.Equal("22", config["A:Two"]);
    }

    [Fact]
    public void Children_ShouldIgnoreCase_OfTheParentPath()
        => Assert.Equal("Leaf", Assert.Single(Indexed("""{ "Root": { "Leaf": 1 } }""").GetSection("ROOT").GetChildren()).Key);

    [Fact]
    public void Children_ShouldBeEmpty_ForAnUnknownOrLeafPath()
    {
        var config = Indexed("""{ "Root": { "Leaf": 1 } }""");

        Assert.Empty(config.GetSection("Nothing").GetChildren());
        Assert.Empty(config.GetSection("Root:Leaf").GetChildren());
    }

    /// <summary>Значення, дописане після завантаження, мусить з'явитись серед дітей, а не загубитись у старому індексі.</summary>
    [Fact]
    public void Children_ShouldSeeAValueSetAfterLoading()
    {
        var config = Indexed("""{ "Root": { "Leaf": 1 } }""");
        _ = config.GetSection("Root").GetChildren().ToList();

        config["Root:Added"] = "2";

        Assert.Contains("Added", config.GetSection("Root").GetChildren().Select(c => c.Key));
    }

    [Fact]
    public void MissingFile_ShouldFailTheLoad()
        => Assert.Throws<FileNotFoundException>(() => new ConfigurationBuilder()
            .AddIndexedJsonFile(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json")).Build());
}
