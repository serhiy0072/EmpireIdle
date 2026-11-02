using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>Мова відповіді: перша підтримувана з бажаних, інакше мова за замовчуванням.</summary>
public class LocalizationConfigTests
{
    private static readonly LocalizationConfig Localization = new() { DefaultLanguage = "uk", Languages = ["uk", "en"] };

    [Fact]
    public void Resolve_ShouldPickTheFirstSupportedLanguage()
        => Assert.Equal("en", Localization.Resolve(["de", "en", "uk"]));

    [Theory]
    [InlineData("de")]
    [InlineData(null)]
    public void Resolve_ShouldFallBackToTheDefault_WhenNothingIsSupported(string? language)
        => Assert.Equal("uk", Localization.Resolve([language]));

    [Fact]
    public void Resolve_ShouldFallBackToTheDefault_WhenNothingIsPreferred()
        => Assert.Equal("uk", Localization.Resolve([]));
}
