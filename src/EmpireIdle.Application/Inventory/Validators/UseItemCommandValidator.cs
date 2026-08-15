using FluentValidation;

namespace EmpireIdle.Application.Inventory.Commands.Validators
{
    public sealed class UseItemCommandValidator : AbstractValidator<UseItemCommand>
    {
        public UseItemCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.ItemKey).NotEmpty().MaximumLength(50);
            RuleFor(x => x.Count).GreaterThan(0);
        }
    }
}
