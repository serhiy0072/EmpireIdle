using EmpireIdle.Application.Lootboxes.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Lootboxes.Validators
{
    public sealed class BuyLootboxCommandValidator : AbstractValidator<BuyLootboxCommand>
    {
        public BuyLootboxCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.ItemKey).NotEmpty().MaximumLength(50);
            // Стеля конкретної скрині — MaxPerPurchase у конфізі, тут лише межа проти сміття в запиті
            RuleFor(x => x.Count).InclusiveBetween(1, 100);
        }
    }
}
