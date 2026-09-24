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

    [Fact]
    public void Validate_ShouldRejectADefaultOutsideTheList()
        => Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(Config(l =>
        {
            l.DefaultLanguage = "de";
            l.Languages = ["uk", "en"];
        })));
}
