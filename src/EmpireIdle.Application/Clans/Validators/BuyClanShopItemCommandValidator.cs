using EmpireIdle.Application.Clans.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Clans.Validators
{
    public sealed class BuyClanShopItemCommandValidator : AbstractValidator<BuyClanShopItemCommand>
    {
        public BuyClanShopItemCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.Kind).IsInEnum();
            RuleFor(x => x.ItemKey).NotEmpty().MaximumLength(50);
            RuleFor(x => x.Quantity).InclusiveBetween(1, 1000);
            RuleFor(x => x.ExpectedPrice).GreaterThan(0);
        }
    }
}
