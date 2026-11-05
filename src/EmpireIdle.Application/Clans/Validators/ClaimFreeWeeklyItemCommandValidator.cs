using EmpireIdle.Application.Clans.Commands;
using FluentValidation;

namespace EmpireIdle.Application.Clans.Validators
{
    public sealed class ClaimFreeWeeklyItemCommandValidator : AbstractValidator<ClaimFreeWeeklyItemCommand>
    {
        public ClaimFreeWeeklyItemCommandValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.ItemKey).NotEmpty().MaximumLength(50);
            RuleFor(x => x.Quantity).InclusiveBetween(1, 1000);
        }
    }
}
