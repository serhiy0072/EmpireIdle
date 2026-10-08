using EmpireIdle.Application.Market.Queries;
using EmpireIdle.Application.Market.Validators;
using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Application.Tests.Market;

/// <summary>Невідомий вид товару — 400 від валідатора, а не 500 з оцінки товару.</summary>
public class GetMarketQuoteQueryValidatorTests
{
    private static readonly GetMarketQuoteQueryValidator Validator = new();

    [Fact]
    public void Validate_ShouldReject_UnknownKind()
    {
        var result = Validator.Validate(new GetMarketQuoteQuery(Guid.NewGuid(), (MarketListingKind)99, null, null, 1));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(GetMarketQuoteQuery.Kind));
    }

    [Fact]
    public void Validate_ShouldReject_BatchAboveTheListingCap()
    {
        var result = Validator.Validate(new GetMarketQuoteQuery(Guid.NewGuid(), MarketListingKind.Item, null, "potion",
            ListOnMarketCommandValidator.MaxQuantity + 1));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(GetMarketQuoteQuery.Quantity));
    }

    [Fact]
    public void Validate_ShouldAccept_AnUnfinishedItemForm()
    {
        var result = Validator.Validate(new GetMarketQuoteQuery(Guid.NewGuid(), MarketListingKind.Item, null, "potion", 0));

        Assert.True(result.IsValid);
    }
}
