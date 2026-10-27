using EmpireIdle.API.Services;
using Microsoft.AspNetCore.Http;

namespace EmpireIdle.Api.Tests.Middleware;

/// <summary>Мови з Accept-Language: від бажаної, лише основний код, без відкинутих (q=0) і «*».</summary>
public class RequestContextTests
{
    private static IReadOnlyList<string> Languages(string? header)
    {
        var context = new DefaultHttpContext();

        if (header is not null)
            context.Request.Headers.AcceptLanguage = header;

        return new RequestContext(new HttpContextAccessor { HttpContext = context }).AcceptedLanguages;
    }

    [Fact]
    public void AcceptedLanguages_ShouldOrderByQuality_AndKeepThePrimaryCode()
        => Assert.Equal(["en", "uk", "de"], Languages("uk;q=0.8, en-US, de;q=0.5, en;q=0.9"));

    [Fact]
    public void AcceptedLanguages_ShouldDropRejectedAndWildcard()
        => Assert.Equal(["en"], Languages("uk;q=0, *, en"));

    [Fact]
    public void AcceptedLanguages_ShouldBeEmpty_WithoutTheHeader()
        => Assert.Empty(Languages(null));

    [Fact]
    public void AcceptedLanguages_ShouldBeEmpty_OutsideAnHttpRequest()
        => Assert.Empty(new RequestContext(new HttpContextAccessor()).AcceptedLanguages);
}
