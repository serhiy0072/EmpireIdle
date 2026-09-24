using EmpireIdle.Application.Inventory.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Inventory.Validators
{
    public sealed class GiftItemCommandValidator : AbstractValidator<GiftItemCommand>
    {
        public GiftItemCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.RecipientId).NotEmpty();
            RuleFor(x => x.ItemKey).NotEmpty().MaximumLength(50);
            RuleFor(x => x.Count).InclusiveBetween(1, 100);
        }
    }
}
