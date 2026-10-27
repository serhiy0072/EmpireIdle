using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>Мови в конфігу: є хоч одна, мова за замовчуванням серед них, російської немає.</summary>
public class LocalizationValidationTests
{
    private static GameConfig Config(Action<LocalizationConfig> tune)
    {
        var config = new GameConfigBuilder().WithBuildings().Build();
        tune(config.Localization);
        return config;
    }

    [Fact]
    public void Validate_ShouldAcceptUkrainianAndEnglish()
        => Assert.Null(Record.Exception(() => GameConfigValidator.Validate(Config(l =>
        {
            l.DefaultLanguage = "uk";
            l.Languages = ["uk", "en"];
        }))));

    [Theory]
    [InlineData("ru")]
    [InlineData("RU")]
    public void Validate_ShouldRejectRussian(string russian)
        => Assert.Contains("Russian", Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(Config(l =>
            l.Languages = ["uk", russian]))).Message);

    /// <summary>Переклад неіснуючого ключа тихо нічого б не перекладав — приховав би друкарську помилку.</summary>
    [Fact]
    public void Validate_ShouldRejectALocaleNameForAnUnknownKey()
    {
        var config = Config(l => l.Languages = ["uk", "en"]);
        config.Locales["en"] = new LocaleConfig { Texts = new Dictionary<string, string> { ["building.nowhere"] = "Nowhere" } };

        Assert.Contains("building.nowhere", Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config)).Message);
    }

    /// <summary>Поле, якого в записі не буває (опис будівлі), — така ж друкарська помилка, як невідомий ключ.</summary>
    [Fact]
    public void Validate_ShouldRejectAFieldTheSectionDoesNotHave()
    {
        var config = Config(l => l.Languages = ["uk", "en"]);
        config.Locales["en"] = new LocaleConfig
        {
            Texts = new Dictionary<string, string> { [$"building.{TestKeys.Townhall}.lore"] = "Old stones" }
        };

        Assert.Contains("lore", Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config)).Message);
    }

    [Fact]
    public void Validate_ShouldAcceptANameAndAFieldOfAKnownEntry()
    {
        var config = new GameConfigBuilder().WithBuildings().WithHeroes().Build();
        config.Localization.Languages = ["uk", "en"];
        var hero = config.Heroes[0].Key;
        config.Locales["en"] = new LocaleConfig
        {
            Texts = new Dictionary<string, string>
            {
                [$"building.{TestKeys.Townhall}"] = "Town Hall",
                [$"hero.{hero}.lore"] = "Born in the hills"
            }
        };

        Assert.Null(Record.Exception(() => GameConfigValidator.Validate(config)));
    }

    [Fact]
    public void Validate_ShouldRejectALocaleForAnUnsupportedLanguage()
    {
        var config = Config(l => l.Languages = ["uk"]);
        config.Locales["de"] = new LocaleConfig();

        Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
    }

    [Fact]
    public void Validate_ShouldRejectADefaultOutsideTheList()
        => Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(Config(l =>
        {
            l.DefaultLanguage = "de";
            l.Languages = ["uk", "en"];
        })));
}
