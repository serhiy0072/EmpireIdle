using EmpireIdle.Application.Shop.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Shop.Validators
{
    public sealed class BuyShopItemCommandValidator : AbstractValidator<BuyShopItemCommand>
    {
        public BuyShopItemCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.ItemKey).NotEmpty().MaximumLength(50);
            // Стеля конкретного товару — в конфізі, тут лише межа проти сміття в запиті
            RuleFor(x => x.Count).InclusiveBetween(1, 100);
        }
    }
}
