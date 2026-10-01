using EmpireIdle.Application.Market.Queries;
using FluentValidation;

namespace EmpireIdle.Application.Market.Validators
{
    /// <summary>
    /// Котирування м'якше за виставлення: форма ще не заповнена до кінця. Але невідомий вид
    /// чи завелика пачка — помилка клієнта, а не баг сервера, що впав би 500 в оцінці товару.
    /// </summary>
    public sealed class GetMarketQuoteQueryValidator : AbstractValidator<GetMarketQuoteQuery>
    {
        public GetMarketQuoteQueryValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.Kind).IsInEnum();
            RuleFor(x => x.ItemKey).MaximumLength(50);
            RuleFor(x => x.Quantity).LessThanOrEqualTo(ListOnMarketCommandValidator.MaxQuantity);
        }
    }
}
